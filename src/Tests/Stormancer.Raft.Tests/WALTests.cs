using Stormancer.Raft.WAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.Tests
{
    public class MockLogEntry : IReplicatedLogEntry<MockLogEntry>
    {
        public MockLogEntry(ulong id, ulong term, ReplicatedLogEntryType type)
        {
            Id = id;
            Term = term;
            Type = type;
        }
        public ulong Id { get; }

        public ulong Term { get; }

        public ReplicatedLogEntryType Type { get; }

        public static MockLogEntry CreateSystem<TContent>(ulong id, ulong term, ReplicatedLogEntryType type, IRecord content)
        {
            throw new NotImplementedException();
        }

        public static bool TryRead(ulong id, ulong term, ReadOnlySpan<byte> content, [NotNullWhen(true)] out MockLogEntry? value)
        {
            if (content.Length < 10)
            {
                value = null;
                return false;
            }
            else
            {
                value = new MockLogEntry(id, term, (ReplicatedLogEntryType)content[0]);
                return true;
            }
        }

        public TContent? As<TContent>() where TContent : IRecord<TContent>
        {
            throw new NotImplementedException();
        }

        public int GetLength()
        {
            return 10;
        }

        public bool TryWrite(Span<byte> buffer, out int length)
        {
            length = 10;
            buffer[0] = (byte)Type;
            return buffer.Length >= length;
        }
    }
    public class WALTests
    {
        [Theory]
        [InlineData(2)]
        [InlineData(150)]
        [InlineData(10_000)]
        public async Task AddEntries(ulong count)
        {
            var provider = new MemoryWALSegmentProvider(new MemoryWALSegmentOptions { });
            await using var wal = new WriteAheadLog<MockRecord>("test", new LogOptions { Storage = provider });

            for(ulong i = 1; i <= count; i++)
            {
                wal.AppendEntries(Enumerable.Repeat(
                new MockLogEntry(i,1, ReplicatedLogEntryType.Content),1));
            }

            var header = wal.GetLastEntryHeader();

            Assert.True(header.EntryId == count);

        }

        [Fact]
        public async Task GetLastEntryHeader()
        {
            var provider = new MemoryWALSegmentProvider(new MemoryWALSegmentOptions { });
            await using var wal = new WriteAheadLog<MockRecord>("test", new LogOptions { Storage = provider });

            wal.AppendEntries(new[]{
                new MockLogEntry(1,1, ReplicatedLogEntryType.Content),
                new MockLogEntry(2,1, ReplicatedLogEntryType.Content),
            });

            var header = wal.GetLastEntryHeader();

            Assert.True(header.EntryId == 2 && header.Term == 1);

        }

        [Fact]
        public async Task GetEntries()
        {
            var provider = new MemoryWALSegmentProvider(new MemoryWALSegmentOptions { });
            await using var wal = new WriteAheadLog<MockRecord>("test", new LogOptions { Storage = provider });

            wal.AppendEntries(new[]{
                new MockLogEntry(1,1, ReplicatedLogEntryType.Content),
                new MockLogEntry(2,1, ReplicatedLogEntryType.Content),
            });

            var result = await wal.GetEntriesAsync<MockLogEntry>(1, 2);

            Assert.True(result.FirstEntryId == 1 && result.LastEntryId == 2);
            var nb = 0;
            foreach(var entry in result.Entries) 
            {
                Assert.True(entry.Type == ReplicatedLogEntryType.Content); nb++;
            }
            Assert.True(nb == 2);
           
        }

        [Fact]
        public async Task TruncateAfter()
        {
            var provider = new MemoryWALSegmentProvider(new MemoryWALSegmentOptions { });
            await using var wal = new WriteAheadLog<MockRecord>("test", new LogOptions { Storage = provider });

            for (ulong i = 1; i <= 10_000; i++)
            {
                wal.AppendEntries(Enumerable.Repeat(
                new MockLogEntry(i, 1, ReplicatedLogEntryType.Content), 1));
            }

            wal.TruncateAfter(100);

            var header = wal.GetLastEntryHeader();
            Assert.True(header.EntryId == 100);
        }

        [Fact]
        public async Task TruncateBefore()
        {
            var provider = new MemoryWALSegmentProvider(new MemoryWALSegmentOptions { });
            await using var wal = new WriteAheadLog<MockRecord>("test", new LogOptions { Storage = provider });

            for (ulong i = 1; i <= 10_000; i++)
            {
                wal.AppendEntries(Enumerable.Repeat(
                new MockLogEntry(i, 1, ReplicatedLogEntryType.Content), 1));
            }

            wal.TruncateBefore(1000);

            var header = wal.GetLastEntryHeader();
            Assert.True(header.EntryId == 10_000);
        }

    }
}
