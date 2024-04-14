using System;
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
        public int PageSize { get; set; } = 4096;
        public int PagesPerSegment { get; set; } = 256;

        public MemoryPool<byte> MemoryPool { get; set; } = MemoryPool<byte>.Shared;
    }

    public class MemoryWALSegmentProvider : IWALSegmentProvider
    {
        private readonly MemoryPool<byte> _memoryPool;


        public MemoryWALSegmentProvider(MemoryPool<byte> memoryPool)
        {
            _memoryPool = memoryPool;
        }
        public IWALSegment GetOrCreateSegment(string category, int currentSegmentId)
        {
            throw new NotImplementedException();
        }

        private class MemoryPage : IDisposable, IBufferWriter<byte>
        {

            private readonly IMemoryOwner<byte> _pageBufferOwner;

            public byte Type { get; }

            public MemoryPage(byte type, int id, IMemoryOwner<byte> pageBufferOwner)
            {
                Id = id;
                _pageBufferOwner = pageBufferOwner;
                Type = type;

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

                if (Offset + sizeHint > Size || sizeHint<=0)
                {
                    sizeHint = Size - Offset;

                }

                return _pageBufferOwner.Memory.Slice(Offset,sizeHint);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                if (Offset + sizeHint > Size || sizeHint <= 0)
                {
                    sizeHint = Size - Offset;

                }
                return _pageBufferOwner.Memory.Span.Slice(Offset, sizeHint);
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

            private bool _readOnly = false;
            private List<MemoryPage> _indexPages = new List<MemoryPage>();
            private List<MemoryPage> _contentPages = new List<MemoryPage>();
            private MemoryPage _currentIndexPage;
            private MemoryPage _currentContentPage;
            private readonly MemoryWALSegmentOptions _options;

            public MemoryWALSegment(int segmentId,MemoryWALSegmentOptions options)
            {
                SegmentId = segmentId;
                _options = options;

                CreateNewIndexPage();
                CreateNewContentPage();
            }

            [MemberNotNull(nameof(_currentIndexPage))]
            private void CreateNewIndexPage()
            {
                var id = _currentIndexPage !=null? _currentIndexPage.Id+1 : 1;
                _currentIndexPage = new MemoryPage(0, id, _options.MemoryPool.Rent(_options.PageSize));
                _indexPages.Add(_currentIndexPage);
            }

            [MemberNotNull(nameof(_currentContentPage))]
            private void CreateNewContentPage()
            {
                var id = _currentContentPage != null ? _currentContentPage.Id + 1 : 1;
                _currentContentPage = new MemoryPage(0, id, _options.MemoryPool.Rent(_options.PageSize));
                _indexPages.Add(_currentContentPage);
            }
            public int PagesCount => _indexPages.Count+_contentPages.Count;

            public int SegmentId { get; }


            public ValueTask DisposeAsync()
            {
                throw new NotImplementedException();
            }

            public ValueTask<WalSegmentGetEntriesResult<TLogEntry>> GetEntries<TLogEntry>(ulong firstEntry, ulong lastEntry) where TLogEntry : IReplicatedLogEntry
            {
                throw new NotImplementedException();
            }

            public ValueTask<LogEntryHeader> GetEntryHeader(ulong firstEntry)
            {
                throw new NotImplementedException();
            }

            public bool TryAppendEntry(IReplicatedLogEntry logEntry)
            {
                throw new NotImplementedException();
            }

            public bool TryTruncateEnd(ulong newLastEntryId)
            {
                throw new NotImplementedException();
            }

            public void SetReadOnly()
            {
                _readOnly = true;   
            }
        }
    }


}
