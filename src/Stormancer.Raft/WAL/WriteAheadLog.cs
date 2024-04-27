using Stormancer.Raft.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
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
        public required IWALSegmentProvider Storage { get; set; }
    }
    internal class WriteAheadLog : IAsyncDisposable
    {
        private object _lock = new object();
        private readonly string _category;
        private readonly LogOptions _options;
        private IWALSegment _currentSegment;
        private readonly LogMetadata _metadata;
       
        private IWALSegmentProvider _segmentProvider;
        private Dictionary<int, IWALSegment> _openedSegments = new Dictionary<int, IWALSegment>();

        public WriteAheadLog(string category, LogOptions options, LogMetadata metadata)
        {
            if (options.Storage == null)
            {
                throw new ArgumentNullException("LogOptions.Storage cannot be null");
            }
            _category = category;
            _options = options;
            _metadata = metadata;
            _segmentProvider = _options.Storage;
            _currentSegment = LoadSegment(_metadata.CurrentSegmentId);
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
                var prevEntrySegment = GetSegment(firstEntryId - 1);
                var header = await prevEntrySegment.GetEntryHeader(firstEntryId - 1);
                prevLogEntryId = header.EntryId;
                prevLogEntryTerm = header.Term;

            }
            else
            {
                prevLogEntryId = 0;
                prevLogEntryTerm = 0;
            }

            var segment = GetSegment(firstEntryId);

            var result = await segment.GetEntries<TLogEntry>(firstEntryId, lastEntryId);

            return new GetEntriesResult<TLogEntry>(prevLogEntryId, prevLogEntryTerm, result.FirstEntryId, result.LastEntryId, result.Entries, result);



        }

        public void AppendEntries<TLogEntry>(IEnumerable<TLogEntry> logEntries) where TLogEntry: IReplicatedLogEntry<TLogEntry> 
        {
            var segment = GetCurrentSegment();
            foreach (var entry in logEntries)
            {
                while (!segment.TryAppendEntry(entry))
                {
                    segment = CreateNewSegmentIfRequired(segment);
                }
            }
        }

        private IWALSegment CreateNewSegmentIfRequired(IWALSegment expectedSegment)
        {
            
            if(_currentSegment != expectedSegment)
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
                    _metadata.CurrentSegmentId++;
                    _currentSegment = LoadSegment(_metadata.CurrentSegmentId);
                    SaveMetadata();
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
            _metadata.CurrentSegmentId = _currentSegment.SegmentId; 
        }


        private int GetSegmentId(ulong entryId)
        {
            for (int i = 0; i < _metadata.SegmentsStarts.Count; i++)
            {
                if (entryId < _metadata.SegmentsStarts[i])
                {

                    return i - 1;
                }
            }
            return _metadata.CurrentSegmentId;
        }


        private IWALSegment GetSegment(ulong entryId)
        {
            var segmentId = GetSegmentId(entryId);
            var currentSegment = _currentSegment;
            if (currentSegment.SegmentId == segmentId)
            {
                return currentSegment;
            }
            else
            {
                lock (_lock)
                {
                    if (_openedSegments.TryGetValue(segmentId, out var segment))
                    {
                        return segment;
                    }
                    else
                    {
                        segment = LoadSegment(segmentId);
                        return segment;
                    }

                }
            }

        }

        private IWALSegment LoadSegment(int segmentId)
        {
            var segment = _segmentProvider.GetOrCreateSegment(_category, segmentId);
           
            _openedSegments.Add(segmentId, segment);
            return segment;
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
    }

    public struct LogEntryHeader
    {
        public bool Found => Term != 0;
        public required ulong Term { get; init; }
        public required ulong EntryId { get; init; }

        public required int Length { get; init; }
    }
}
