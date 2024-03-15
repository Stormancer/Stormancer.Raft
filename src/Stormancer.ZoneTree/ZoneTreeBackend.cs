using Stormancer.ShardedDb;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.ZoneTree
{
    internal class ZoneTreeBackend : IStorageShardBackend<ZoneTreeCommand, ZoneTreeCommandResult>
    {
        public ZoneTreeBackend() 
            {
                var provider = new Tenray.ZoneTree.WAL.WriteAheadLogProvider(null, null);
                provider.GetOrCreateWAL()
        
        }
        public ulong LastAppliedLogEntry => throw new NotImplementedException();

        public ulong LastLogEntry => throw new NotImplementedException();

        public ulong LastLogEntryTerm => throw new NotImplementedException();

        public ulong CurrentTerm => throw new NotImplementedException();

        public ShardsConfigurationRecord CurrentShardsConfiguration => throw new NotImplementedException();

        public void ApplyEntries(ulong index)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IReplicatedLogEntry> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendCommand(ZoneTreeCommand command, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendConfigurationChangeCommand(Guid cmdId, ShardsConfigurationRecord configuration, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendEntries(IEnumerable<IReplicatedLogEntry> entries)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendNoOpCommand([NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            throw new NotImplementedException();
        }

        public bool TryGetEntryTerm(ulong prevLogId, out ulong entryTerm)
        {
            throw new NotImplementedException();
        }

        public bool TryTruncateEntriesAfter(ulong logEntryId)
        {
            throw new NotImplementedException();
        }

        public void UpdateTerm(ulong term)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ZoneTreeCommandResult> WaitCommittedAsync(ulong entryId)
        {
            throw new NotImplementedException();
        }
    }
}
