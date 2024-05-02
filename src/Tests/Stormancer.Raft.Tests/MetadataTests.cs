using Stormancer.Raft.WAL;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.Raft.Tests
{
    public class MockRecord : IRecord<MockRecord>
    {
        public static bool TryRead(ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out MockRecord? record, out int length)
        {
            length = 10;
            if (buffer.Length >= 10)
            {
                record = new MockRecord();
                return true;
            }
            else
            {
                record = null;
                return false;
            }
        }

        public int GetLength()
        {
            return 10;
        }

        public bool TryWrite(Span<byte> buffer)
        {
            return buffer.Length >= 10;
        }
    }

    public class MetadataTests
    {
        
        [Fact]
        public void MetadataAddEntry()
        {
            var metadata = new LogMetadata<MockRecord>();
            Assert.True(metadata.TryAddEntry(0, 1, 1));
            Assert.False(metadata.TryAddEntry(0, 2, 1));
            Assert.True(metadata.TryAddEntry(0, 3, 2));
            Assert.True(metadata.TryAddEntry(1, 4, 3));
        }

        [Fact]
        public void TryGetTerm()
        {
            var metadata = new LogMetadata<MockRecord>();
            Assert.True(metadata.TryAddEntry(0, 1, 1));
            Assert.False(metadata.TryAddEntry(0, 2, 1));
            Assert.True(metadata.TryAddEntry(0, 3, 2));
            Assert.True(metadata.TryAddEntry(1, 4, 3));

            Assert.True(metadata.TryGetTerm(2, out var term)&& 1 == term);
            
            Assert.True(metadata.TryGetTerm(10, out term) && term == 3);
            Assert.True(metadata.TryGetTerm(3, out term) && term == 2);
        }

        [Fact]
        public void SerializeMetadataEmptyContent()
        {
            var metadata = new LogMetadata<MockRecord>();

            var pool = MemoryPool<byte>.Shared;

            using var mem = pool.Rent(metadata.GetLength());
            var span = mem.Memory.Span;
            Assert.True(metadata.TryWrite(ref span, out var length));
            Assert.True(length == metadata.GetLength());

            Assert.True(LogMetadata<MockRecord>.TryRead(new ReadOnlySequence<byte>(mem.Memory), out var metadata2, out length));


        }

        [Fact]
        public void SerializeMetadataWithContent()
        {
            var metadata = new LogMetadata<MockRecord>();
            metadata.Content = new MockRecord();
            var pool = MemoryPool<byte>.Shared;

            using var mem = pool.Rent(metadata.GetLength());
            var span = mem.Memory.Span;
            Assert.True(metadata.TryWrite(ref span, out var length));
            Assert.True(length == metadata.GetLength());

            Assert.True(LogMetadata<MockRecord>.TryRead(new ReadOnlySequence<byte>(mem.Memory), out var metadata2, out length));
            Assert.NotNull(metadata.Content);

        }

        [Fact]
        public void SerializeMetadataWithEntries()
        {
            var metadata = new LogMetadata<MockRecord>();
            metadata.TryAddEntry(0, 1, 1);
            metadata.TryAddEntry(0, 2, 1);
            metadata.TryAddEntry(0, 3, 2);
            metadata.TryAddEntry(1, 4, 3);

            var pool = MemoryPool<byte>.Shared;

            using var mem = pool.Rent(metadata.GetLength());
            var span = mem.Memory.Span;
            Assert.True(metadata.TryWrite(ref span, out var length));
            Assert.True(length == metadata.GetLength());

            Assert.True(LogMetadata<MockRecord>.TryRead(new ReadOnlySequence<byte>(mem.Memory), out var metadata2, out length));

            Assert.True(metadata.TryGetTerm(2, out var term) && 1 == term);

            Assert.True(metadata.TryGetTerm(10, out term) && term == 3);
            Assert.True(metadata.TryGetTerm(3, out term) && term == 2);
            Assert.True(metadata.TryGetSegment(4,out var segmentId) && segmentId == 1);
            Assert.True(metadata.TryGetSegment(3, out segmentId) && segmentId == 0);
        }
    }
}