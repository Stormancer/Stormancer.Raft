using Stormancer.Raft.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    public class CreateSnapshotContext<TLogEntry>
    {

    }
    public struct GetEntriesResult<TLogEntry> : IDisposable where TLogEntry : IReplicatedLogEntry<TLogEntry>
    {
        private readonly IDisposable _disposable;

        internal GetEntriesResult(ulong PrevLogEntryId, ulong PrevLogEntryTerm, ulong FirstEntryId, ulong LastEntryId, IEnumerable<TLogEntry> entries, IDisposable disposable)
        {
            this.PrevLogEntryId = PrevLogEntryId;
            this.PrevLogEntryTerm = PrevLogEntryTerm;
            this.FirstEntryId = FirstEntryId;
            this.LastEntryId = LastEntryId;
            Entries = entries;
            _disposable = disposable;
        }

        public ulong PrevLogEntryId { get; }
        public ulong PrevLogEntryTerm { get; }
        public ulong FirstEntryId { get; }
        public ulong LastEntryId { get; }
        public IEnumerable<TLogEntry> Entries { get; }

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
    public class LogOptions
    {
        /// <summary>
        /// Maximum number of records in a segment.
        /// </summary>
        public uint MaxSegmentRecordsCount { get; set; } = 100_000;
        public uint MaxRecordSize { get; set; } = 1024 * 1024;
        public uint SegmentSize { get; set; } = 16 * 1024 * 1024;
        public uint PageSize { get; set; } = 8 * 1024;
        public required IWALStorageProvider Storage { get; set; }

    }
    internal class WriteAheadLog<TMetadataContent> : IAsyncDisposable
        where TMetadataContent : IRecord<TMetadataContent>
    {
        private object _lock = new object();
        private readonly string _category;
        private readonly LogOptions _options;
        private IWALSegment _currentSegment;
        private readonly LogMetadata<TMetadataContent> _metadata;

        private IWALStorageProvider _segmentProvider;
        private Dictionary<int, IWALSegment> _openedSegments = new Dictionary<int, IWALSegment>();

        public WriteAheadLog(string category, LogOptions options)
        {
            if (options.Storage == null)
            {
                throw new ArgumentNullException("LogOptions.Storage cannot be null");
            }
            _category = category;
            _options = options;
            _metadata = LoadMetadata();
            _segmentProvider = _options.Storage;
            _currentSegment = GetOrLoadSegment(_metadata.CurrentSegmentId);
        }


        public void UpdateMetadata(TMetadataContent metadataContent)
        {
            _metadata.Content = metadataContent;
            SaveMetadata();
        }

        public TMetadataContent? Metadata => _metadata.Content;

        public LogEntryHeader GetLastEntryHeader()
        {
            return _currentSegment.GetLastEntryHeader();
        }

        public bool TryGetEntryTerm(ulong entryId, out ulong term)
        {
            return _metadata.TryGetTerm(entryId, out term);
        }

        public void TruncateAfter(ulong entryId)
        {


            lock (_lock)
            {
                foreach (var segment in _metadata.RemoveAfter(entryId))
                {
                    if (_currentSegment.SegmentId == segment)
                    {
                        _currentSegment = GetOrLoadSegment(segment - 1);
                    }
                    DeleteSegment(segment);
                }

                _currentSegment.TryTruncateEnd(entryId);

                SaveMetadata();
            }
        }

        private void DeleteSegment(int segmentId)
        {
            if (_currentSegment.SegmentId == segmentId)
            {
                throw new InvalidOperationException("Cannot delete current segment.");
            }
            if (_openedSegments.Remove(segmentId, out var segment))
            {
                segment.Delete();
            }
        }

        /// <summary>
        /// Deletes content, ensuring that records after entryId included are kept alive.
        /// </summary>
        /// <param name="entryId"></param>
        /// <remarks>Content before entry id might not be deleted, the algorithm only </remarks>
        /// <returns></returns>
        public void TruncateBefore(ulong entryId)
        {
            lock (_lock)
            {
                foreach (var segment in _metadata.RemoveBefore(entryId))
                {
                    DeleteSegment(segment);
                }

                SaveMetadata();
            }
        }

        /// <summary>
        /// Creates a snapshot representing the state of the system until entryId, using the provided snapshot generation algorithm
        /// </summary>
        /// <param name="entryId"></param>
        /// <param name="snapshotGenerator"></param>
        /// <returns></returns>
        public ValueTask CreateSnapshot<TLogEntry>(ulong entryId, Func<CreateSnapshotContext<TLogEntry>, ValueTask> snapshotGenerator)
        {
            throw new NotImplementedException("Snapshot");
        }

        public async ValueTask<GetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntryId, ulong lastEntryId) where TLogEntry : IReplicatedLogEntry<TLogEntry>
        {
            if (firstEntryId < 1)
            {
                firstEntryId = 1;
            }
            if (lastEntryId < firstEntryId)
            {
                lastEntryId = firstEntryId;
            }

            ulong prevLogEntryId;
            ulong prevLogEntryTerm;
            if (firstEntryId > 1)
            {
                if (!_metadata.TryGetSegment(firstEntryId - 1, out var prevEntrySegmentId))
                {
                    throw new NotImplementedException("Snapshot");
                }
                var prevEntrySegment = GetOrLoadSegment(prevEntrySegmentId);
                var header = await prevEntrySegment.GetEntryHeader(firstEntryId - 1);
                prevLogEntryId = header.EntryId;
                prevLogEntryTerm = header.Term;

            }
            else
            {
                prevLogEntryId = 0;
                prevLogEntryTerm = 0;
            }

            if (!_metadata.TryGetSegment(firstEntryId, out var firstEntrySegmentId))
            {
                throw new NotImplementedException("Snapshot");
            }
            var segment = GetOrLoadSegment(firstEntrySegmentId);

            var result = await segment.GetEntries<TLogEntry>(firstEntryId, lastEntryId);

            return new GetEntriesResult<TLogEntry>(prevLogEntryId, prevLogEntryTerm, result.FirstEntryId, result.LastEntryId, result.Entries, result);



        }

        public void AppendEntries<TLogEntry>(IEnumerable<TLogEntry> logEntries) where TLogEntry : IReplicatedLogEntry<TLogEntry>
        {
            var segment = GetCurrentSegment();
            foreach (var entry in logEntries)
            {
                while (!TryAppendEntry(segment, entry))
                {
                    segment = CreateNewSegmentIfRequired(segment);
                }
            }
        }



        private bool TryAppendEntry<TLogEntry>(IWALSegment segment, TLogEntry entry) where TLogEntry : IReplicatedLogEntry<TLogEntry>
        {

            if (segment.TryAppendEntry(entry))
            {
                if (_metadata.TryAddEntry(segment, entry.Id, entry.Term))
                {
                    SaveMetadata();
                }
                return true;
            }
            else
            {
                return false;
            }



        }

        private IWALSegment CreateNewSegmentIfRequired(IWALSegment expectedSegment)
        {

            if (_currentSegment != expectedSegment)
            {
                return _currentSegment;
            }

            lock (_lock)
            {
                if (_currentSegment != expectedSegment)
                {
                    return _currentSegment;
                }
                else
                {

                    var segment = _currentSegment;
                    segment.SetReadOnly();

                    _currentSegment = GetOrLoadSegment(_metadata.CurrentSegmentId);

                    return _currentSegment;
                }

            }

        }

        private IWALSegment GetCurrentSegment()
        {
            return _currentSegment;
        }



        private void SaveMetadata()
        {
            lock (_lock)
            {

                _segmentProvider.SaveMetadata(_metadata);
            }

        }

        private LogMetadata<TMetadataContent> LoadMetadata()
        {
            if (_segmentProvider.TryReadMetadata<TMetadataContent>(out var metadata))
            {
                return metadata;
            }
            else
            {
                return new LogMetadata<TMetadataContent>();
            }
        }

        private IWALSegment GetOrLoadSegment(int segmentId)
        {
            if (_openedSegments.TryGetValue(segmentId, out var segment))
            {
                return segment;
            }
            else
            {
                segment = _segmentProvider.GetOrCreateSegment(_category, segmentId);

                _openedSegments.Add(segmentId, segment);
                return segment;
            }
        }


        public async ValueTask DisposeAsync()
        {
            foreach (var (id, segment) in _openedSegments)
            {
                await segment.DisposeAsync();
            }

            await _currentSegment.DisposeAsync();
        }
    }



    public class WALSegmentState
    {
        internal int UseCounter;
    }

    public struct WalSegmentGetEntriesResult<TLogEntry> : IDisposable
    {
        public WalSegmentGetEntriesResult(IEnumerable<TLogEntry> entries, ulong firstEntryId, ulong lastEntryId, WALSegmentState state)
        {
            Entries = entries;
            FirstEntryId = firstEntryId;
            LastEntryId = lastEntryId;
            _state = state;

            Interlocked.Increment(ref _state.UseCounter);
        }

        private readonly WALSegmentState _state;

        public IEnumerable<TLogEntry> Entries { get; }
        public ulong FirstEntryId { get; }
        public ulong LastEntryId { get; }

        public void Dispose()
        {
            Interlocked.Decrement(ref _state.UseCounter);
        }
    }

    public interface IWALSegment : IAsyncDisposable
    {
        int SegmentId { get; }

        string Category { get; }

        ValueTask<WalSegmentGetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntry, ulong lastEntry) where TLogEntry : IReplicatedLogEntry<TLogEntry>;

        bool TryAppendEntry<TLogEntry>(TLogEntry logEntry) where TLogEntry : IReplicatedLogEntry<TLogEntry>;

        ValueTask<LogEntryHeader> GetEntryHeader(ulong firstEntry);

        bool TryTruncateEnd(ulong newLastEntryId);
        void SetReadOnly();
        LogEntryHeader GetLastEntryHeader();
        void Delete();

        bool IsEmpty { get; }
    }

    public struct LogEntryHeader
    {
        public static LogEntryHeader NotFound => new LogEntryHeader { EntryId = 0, Length = 0, Term = 0 };
        public bool Found => Term != 0;
        public required ulong Term { get; init; }
        public required ulong EntryId { get; init; }

        public required int Length { get; init; }
    }
}
