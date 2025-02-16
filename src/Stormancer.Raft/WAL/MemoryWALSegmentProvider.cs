﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    public class MemoryWALSegmentOptions
    {
      
        public int PageSize { get; init; } = 1024;
        public int PagesPerSegment { get; init; } = 256;

        public MemoryPool<byte> MemoryPool { get; init; } = MemoryPool<byte>.Shared;

        public required ILogEntryReaderWriter ReaderWriter { get; init; }
    }

    public class MemoryWALSegmentProvider : IWALStorageProvider
    {
        private readonly MemoryWALSegmentOptions _options;

        private object _lock = new object();

        private IMemoryOwner<byte>? _metadataMemOwner;
        private Memory<byte>? _metadataBuffer;

        public MemoryWALSegmentProvider(MemoryWALSegmentOptions options)
        {
            _options = options;
        }
        public IWALSegment GetOrCreateSegment(string category, int segmentId)
        {
            return new MemoryWALSegment(category, segmentId, _options);
        }

        public bool TryReadMetadata<TMetadataContent>([NotNullWhen(true)] out LogMetadata<TMetadataContent>? metadata) where TMetadataContent : IRecord<TMetadataContent>
        {
            lock(_lock)
            {

                if(_metadataBuffer !=null)
                {
                    return LogMetadata<TMetadataContent>.TryRead(new ReadOnlySequence<byte>(_metadataBuffer.Value), out metadata, out var _);
                }
                else
                {
                    metadata = null;
                    return false;
                }
            }
        }

        public void SaveMetadata<TMetadataContent>(LogMetadata<TMetadataContent> metadata) where TMetadataContent : IRecord<TMetadataContent>
        {
           lock(_lock)
            {
                if(_metadataMemOwner != null)
                {
                    _metadataMemOwner.Dispose();
                }
                _metadataMemOwner= _options.MemoryPool.Rent(metadata.GetLength());

                var buffer = _metadataMemOwner.Memory.Span;

                if(metadata.TryWrite(ref buffer, out var length))
                {
                    _metadataBuffer = _metadataMemOwner.Memory.Slice(0, length);
                }
                else
                {
                    _metadataMemOwner.Dispose();
                    throw new InvalidOperationException("Failed to write metadata.");
                }

            }
        }

        private class MemoryPage : IDisposable, IBufferWriter<byte>
        {

            private readonly IMemoryOwner<byte> _pageBufferOwner;

          

            public MemoryPage(int id, IMemoryOwner<byte> pageBufferOwner)
            {
                Id = id;
                _pageBufferOwner = pageBufferOwner;
                

            }

            public int Size => _pageBufferOwner.Memory.Length;
            public int Offset { get; private set; } = 0;
            public int Remaining => Size - Offset;

            public int Id { get; }

            public void Advance(int count)
            {
                if (Offset + count > Size)
                {
                    throw new ArgumentException("Cannot advance past the end of the page.");
                }
                Offset += count;
            }

            public void Dispose()
            {
                _pageBufferOwner.Dispose();
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {

                if (Offset + sizeHint > Size)
                {
                    throw new ArgumentException("sizeHint too large.");
                }
                if (sizeHint <= 0)
                {
                    sizeHint = Size - Offset;

                }

                return _pageBufferOwner.Memory.Slice(Offset, sizeHint);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                if (Offset + sizeHint > Size)
                {
                    throw new ArgumentException("sizeHint too large.");
                }

                if (Offset + sizeHint > Size || sizeHint <= 0)
                {
                    sizeHint = Size - Offset;

                }
                return _pageBufferOwner.Memory.Span.Slice(Offset, sizeHint);
            }

            public ReadOnlySpan<byte> GetContent(int offset, int length)
            {
                if (offset + length > Offset)
                {
                    throw new ArgumentException("offset+length> Offset");
                }
                var span = _pageBufferOwner.Memory.Span.Slice(offset, length);
                return span;
            }

            public bool CanAllocate(int length)
            {
                return Offset + length <= Size;
            }
        }
        private class MemoryWALSegment : IWALSegment
        {
            private struct IndexRecord
            {
                public required ulong Term { get; set; }
                public required ulong EntryId { get; set; }
                public required int ContentPageId { get; set; }
                public required int ContentOffset { get; set; }
                public required int ContentLength { get; set; }

                public static int Length => 8 + 8 + 4 + 4 + 4;

                public static bool TryRead(ref ReadOnlySpan<byte> buffer, out IndexRecord value)
                {
                    if (buffer.Length < Length)
                    {
                        value = default;
                        return false;
                    }

                    value = new IndexRecord()
                    {
                        Term = BinaryPrimitives.ReadUInt64BigEndian(buffer[0..8]),
                        EntryId = BinaryPrimitives.ReadUInt64BigEndian(buffer[8..16]),
                        ContentPageId = BinaryPrimitives.ReadInt32BigEndian(buffer[16..20]),
                        ContentOffset = BinaryPrimitives.ReadInt32BigEndian(buffer[20..24]),
                        ContentLength = BinaryPrimitives.ReadInt32BigEndian(buffer[24..28])
                    };
                    return true;
                }

                public bool TryWrite(Span<byte> span)
                {
                    if (span.Length < Length)
                    {
                        return false;
                    }

                    BinaryPrimitives.WriteUInt64BigEndian(span[0..8], Term);
                    BinaryPrimitives.WriteUInt64BigEndian(span[8..16], EntryId);
                    BinaryPrimitives.WriteInt32BigEndian(span[16..20], ContentPageId);
                    BinaryPrimitives.WriteInt32BigEndian(span[20..24], ContentOffset);
                    BinaryPrimitives.WriteInt32BigEndian(span[24..28], ContentLength);
                    return true;
                }
            }

            [MemberNotNullWhen(true, nameof(_firstRecord), nameof(_lastRecord))]
            private bool HasRecords()
            {
                return _firstRecord != null;
            }

            private object _syncRoot = new object();

            private IndexRecord? _firstRecord;
            private IndexRecord? _lastRecord;

            private WALSegmentState _segmentState = new WALSegmentState();
            private bool _readOnly;
            private List<MemoryPage> _indexPages = new List<MemoryPage>();
            private List<MemoryPage> _contentPages = new List<MemoryPage>();
            private MemoryPage _currentIndexPage;
            private MemoryPage _currentContentPage;


            private readonly MemoryWALSegmentOptions _options;

            public MemoryWALSegment(string category, int segmentId, MemoryWALSegmentOptions options)
            {
                Category = category;
                SegmentId = segmentId;
                _options = options;

                TryCreateNewContentPage();
                TryCreateNewIndexPage();
            }

            [MemberNotNull(nameof(_currentIndexPage))]
            private bool TryCreateNewIndexPage()
            {
                var id = _currentIndexPage != null ? _currentIndexPage.Id + 1 : 0;
                _currentIndexPage = new MemoryPage( id, _options.MemoryPool.Rent(_options.PageSize));
                _indexPages.Add(_currentIndexPage);
                return true;
            }

            [MemberNotNull(nameof(_currentContentPage))]
            private bool TryCreateNewContentPage()
            {
                var id = _currentContentPage != null ? _currentContentPage.Id + 1 : 0;
                _currentContentPage = new MemoryPage( id, _options.MemoryPool.Rent(_options.PageSize));
                _contentPages.Add(_currentContentPage);
                return true;
            }
            public int PagesCount => _indexPages.Count + _contentPages.Count;

            public int SegmentId { get; }

            public string Category { get; }

            public bool IsEmpty => throw new NotImplementedException();

            public ValueTask DisposeAsync()
            {
                Delete();
                
                return ValueTask.CompletedTask;
            }

            public ValueTask<WalSegmentGetEntriesResult> GetEntries(ulong firstEntryId, ulong lastEntryId) 
            {
                if (!HasRecords())
                {
                    return ValueTask.FromResult(new WalSegmentGetEntriesResult(Enumerable.Empty<LogEntry>(), 0, 0, _segmentState));
                }

                if (firstEntryId < _firstRecord.Value.EntryId)
                {
                    firstEntryId = _firstRecord.Value.EntryId;
                }
                if (lastEntryId > _lastRecord.Value.EntryId)
                {
                    lastEntryId = _lastRecord.Value.EntryId;
                }

                if (firstEntryId > lastEntryId)
                {
                    return ValueTask.FromResult(new WalSegmentGetEntriesResult(Enumerable.Empty<LogEntry>(), 0, 0, _segmentState));
                }

                if (!TryGetIndexPosition(firstEntryId, out var firstEntryPageId, out var firstEntryOffset))
                {
                    return ValueTask.FromResult(new WalSegmentGetEntriesResult(Enumerable.Empty<LogEntry>(), 0, 0, _segmentState));
                }

                if (!TryGetIndexPosition(lastEntryId, out var lastEntryPageId, out var lastEntryOffset))
                {
                    return ValueTask.FromResult(new WalSegmentGetEntriesResult(Enumerable.Empty<LogEntry>(), 0, 0, _segmentState));
                }




                IEnumerable<LogEntry> EnumerateEntries(ulong firstEntryId, ulong lastEntryId)
                {
                    for (var index = firstEntryId; index <= lastEntryId; index++)
                    {
                        if (!TryGetEntryHeader(index, out var header))
                        {
                            lastEntryId = index - 1;
                            yield break;
                        }
                        else
                        {
                            yield return GetLogEntry(header);
                        }
                    }
                }

                return ValueTask.FromResult(new WalSegmentGetEntriesResult(EnumerateEntries(firstEntryId, lastEntryId), firstEntryId, lastEntryId, _segmentState));

            }

            private LogEntry GetLogEntry(IndexRecord header)
            {
                var contentPage = _contentPages[header.ContentPageId];

                var content = contentPage.GetContent(header.ContentOffset, header.ContentLength);

                if (_options.ReaderWriter.TryRead(header.EntryId, header.Term,ref content, out var entry, out var length))
                {
                    return entry;
                }
                else
                {
                    throw new InvalidOperationException("Failed to read entry.");
                }

            }


            /// <summary>
            /// Tries to get the pageId and offset to read to get an entry in the index.
            /// </summary>
            /// <param name="entryId"></param>
            /// <param name="pageId"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            private bool TryGetIndexPosition(ulong entryId, out int pageId, out int offset)
            {
                if (_firstRecord == null)
                {
                    pageId = 0;
                    offset = 0;
                    return false;
                }
                var length = IndexRecord.Length;
                var entriesPerPage = _options.PageSize / length;
                var firstEntryInSegment = _firstRecord.Value;

                var delta = (int)(entryId - firstEntryInSegment.EntryId);

                pageId = delta / entriesPerPage;

                if (_indexPages.Count <= pageId)
                {
                    pageId = 0;
                    offset = 0;
                    return false;
                }

                var indexInPage = delta - pageId * entriesPerPage;

                offset = indexInPage * length;
                return true;

            }
            private bool TryGetEntryHeader(ulong entryId, out IndexRecord header)
            {
                if (TryGetIndexPosition(entryId, out var entryIdPage, out var entryIdOffset) && entryIdPage < _indexPages.Count)
                {

                    var span = _indexPages[entryIdPage].GetContent(entryIdOffset, IndexRecord.Length);
                    if (IndexRecord.TryRead(ref span, out var record))
                    {
                        header = record;
                        return true;
                    }
                }

                header = default;
                return false;

            }
            public ValueTask<LogEntryHeader> GetEntryHeader(ulong entryId)
            {
                if (TryGetEntryHeader(entryId, out var header))
                {
                    return ValueTask.FromResult(new LogEntryHeader { EntryId = header.EntryId, Term = header.Term, Length = header.ContentLength });
                }
                else
                {
                    return ValueTask.FromException<LogEntryHeader>(new InvalidOperationException($"entry {entryId} not found"));
                }
            }

            public bool TryAppendEntry(LogEntry logEntry)
            {
                var writer = _options.ReaderWriter;
                var length =writer.GetContentLength(logEntry);
                if (length > _options.PageSize)
                {
                    return false;
                }

                lock (_syncRoot)
                {
                    if (_readOnly)
                    {
                        return false;
                    }
                   
                    

                    while (!_currentContentPage.CanAllocate(length))
                    {
                        if (!this.TryCreateNewContentPage())
                        {
                            return false;
                        }

                    }

                    while (!_currentIndexPage.CanAllocate(IndexRecord.Length))
                    {
                        if (!TryCreateNewIndexPage())
                        {
                            return false;
                        }

                    }
                    var offset = _currentContentPage.Offset;
                    var pageId = _currentContentPage.Id;
                    var span = _currentContentPage.GetSpan(length);

                    
                    writer.TryWriteContent(ref span,logEntry, out _);

                    
                    _currentContentPage.Advance(length);
                    var indexRecord = new IndexRecord { Term = logEntry.Term, EntryId = logEntry.Id, ContentLength = length, ContentOffset = offset, ContentPageId = pageId };


                    span = _currentIndexPage.GetSpan(IndexRecord.Length);
                    indexRecord.TryWrite(span);
                    _currentIndexPage.Advance(IndexRecord.Length);

                    if(_firstRecord == null)
                    {
                        _firstRecord = indexRecord;
                    }
                    _lastRecord = indexRecord;
                    return true;

                }
            }

          

            public bool TryTruncateEnd(ulong newLastEntryId)
            {
                lock (_syncRoot)
                {

                    if (!HasRecords())
                    {
                        return true;
                    }

                    if (newLastEntryId >= _lastRecord.Value.EntryId)
                    {
                        return true;
                    }

                    if (newLastEntryId < _firstRecord.Value.EntryId)
                    {
                        _currentContentPage = _contentPages[0];
                        _currentIndexPage = _indexPages[0];
                        
                        _firstRecord = null;
                        _lastRecord = null;
                        _readOnly = false;
                        return true;
                    }

                    if (TryGetEntryHeader(newLastEntryId, out var header))
                    {
                        _lastRecord = header;
                        this.TryGetIndexPosition(header.EntryId, out var pageId, out _);

                        _currentIndexPage = _indexPages[pageId];
                        _currentContentPage = _contentPages[header.ContentPageId];
                            

                        _readOnly = false;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

            }

            public void SetReadOnly()
            {
                _readOnly = true;
            }

            public LogEntryHeader GetLastEntryHeader()
            {;
                return _lastRecord != null ? new LogEntryHeader { EntryId = _lastRecord.Value.EntryId, Term = _lastRecord.Value.Term, Length = _lastRecord.Value.ContentLength } :  LogEntryHeader.NotFound;
            }

            public void Delete()
            {
                foreach (var page in _indexPages)
                {
                    page.Dispose();
                }


                foreach (var page in _contentPages)
                {
                    page.Dispose();
                }
            }
        }
    }


}
