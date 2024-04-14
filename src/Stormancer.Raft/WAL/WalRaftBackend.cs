using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    internal class WalShardBackend<TCommand, TCommandResult> : IStorageShardBackend<TCommand, TCommandResult>
        where TCommand : ICommand<TCommand>
        where TCommandResult : ICommandResult<TCommandResult>
    {
        public ulong LastAppliedLogEntry => throw new NotImplementedException();

        public ulong LastLogEntry => throw new NotImplementedException();

        public ulong LastLogEntryTerm => throw new NotImplementedException();

        public ulong CurrentTerm => throw new NotImplementedException();

        public ShardsConfigurationRecord CurrentShardsConfiguration => throw new NotImplementedException();

        public void ApplyEntries(ulong index)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<IReplicatedLogEntry>> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendCommand(TCommand command, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
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

        public ValueTask<TCommandResult> WaitCommittedAsync(ulong entryId)
        {
            throw new NotImplementedException();
        }
    }
}
