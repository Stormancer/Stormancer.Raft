using Stormancer.Raft.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    public struct GetEntriesResult<TLogEntry> : IDisposable where TLogEntry : IReplicatedLogEntry
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
        public IStorageProvider? Storage { get; set; }
    }
    internal class Log : IAsyncDisposable
    {
        private object _lock = new object();
        private readonly string _category;
        private readonly LogOptions _options;
        private IWALSegment _currentSegment;
        private readonly LogMetadata _metadata;
       
        private IStorageProvider _storageProvider;
        private Dictionary<int, IWALSegment> _openedSegments = new Dictionary<int, IWALSegment>();

        public Log(string category, LogOptions options, LogMetadata metadata)
        {
            if (options.Storage == null)
            {
                throw new ArgumentNullException("LogOptions.Storage cannot be null");
            }
            _category = category;
            _options = options;
            _metadata = metadata;
            _storageProvider = _options.Storage;
        }





        public async ValueTask<GetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntryId, ulong lastEntryId) where TLogEntry : IReplicatedLogEntry
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

            TLogEntry? lastEntry;
            var result = await segment.GetEntries<TLogEntry>(firstEntryId, lastEntryId);

            return new GetEntriesResult<TLogEntry>(prevLogEntryId, prevLogEntryTerm, result.FirstEntryId, result.LastEntryId, result.Entries, result);



        }

        public void AppendEntries(IEnumerable<IReplicatedLogEntry> logEntries)
        {
            var segment = GetCurrentSegment();
            foreach (var entry in logEntries)
            {
                while (!segment.TryAppendEntry(entry))
                {
                    segment = CreateNewSegmentIfFull();
                }
            }
        }

        private IWALSegment CreateNewSegmentIfFull()
        {
            lock (_lock)
            {
                if (!_currentSegment.IsFull)
                {
                    return _currentSegment;
                }
                else
                {
                    var segment = _currentSegment;
                    _currentSegment = new WalSegment(_category, segment.SegmentId + 1,_currentSegment.CurrentEntryId+1, _options);
                    _openedSegments.Add(segment.SegmentId, segment);
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
            return _currentSegmentId;
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
            var segment = new WalSegment(_category, segmentId, _options);
            segment.Open();
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

    internal interface IWALSegment : IAsyncDisposable
    {
        int SegmentId { get; }
        bool IsFull { get; }

        ValueTask<WalSegmentGetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntry, ulong lastEntry) where TLogEntry : IReplicatedLogEntry;

        bool TryAppendEntry(IReplicatedLogEntry logEntry);

        ValueTask<LogEntryHeader> GetEntryHeader(ulong firstEntry);

        bool TryTruncateEnd(ulong newLastEntryId);
    }

    public struct LogEntryHeader
    {
        public bool Found => Term != 0;
        public ulong Term { get; set; }
        public ulong EntryId { get; set; }
    }
    internal class WalSegment : IWALSegment
    {
        private readonly LogOptions _options;

        public WalSegment(string category, int segmentId,ulong startEntryId, LogOptions options)
        {
            Category = category;
            SegmentId = segmentId;
            _options = options;
        }

        private int headerOffset;
        private int contentOffset;

        public string Category { get; }
        public int SegmentId { get; }

        public bool IsFull => throw new NotImplementedException();

        public bool TryAppendEntry(IReplicatedLogEntry logEntry)
        {

        }

        public ValueTask<WalSegmentGetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntry, ulong lastEntry) where TLogEntry : IReplicatedLogEntry
        {
            throw new NotImplementedException();
        }

        public ValueTask<LogEntryHeader> GetEntryHeader(ulong firstEntry)
        {
            throw new NotImplementedException();
        }

        public bool TryTruncateEnd(ulong newLastEntryId)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public void Open()
        { }
    }

}
