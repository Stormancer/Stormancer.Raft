using Stormancer.Raft;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.ZoneTree
{
    public class ZoneTreeLogEntry : IReplicatedLogEntry<ZoneTreeLogEntry>
    {
        public ulong Id => throw new NotImplementedException();

        public ulong Term => throw new NotImplementedException();

        public ReplicatedLogEntryType Type => throw new NotImplementedException();

        public static ZoneTreeLogEntry CreateSystem<TContent>(ulong id, ulong term, ReplicatedLogEntryType type, IRecord content)
        {
            throw new NotImplementedException();
        }

        public static bool TryRead(ulong id, ulong term, ReadOnlySpan<byte> content, [NotNullWhen(true)] out ZoneTreeLogEntry? value)
        {
            throw new NotImplementedException();
        }

        public TContent? As<TContent>() where TContent : IRecord<TContent>
        {
            throw new NotImplementedException();
        }

        public int GetLength()
        {
            throw new NotImplementedException();
        }

        public bool TryWrite(Span<byte> buffer, out int length)
        {
            throw new NotImplementedException();
        }
    }
}
