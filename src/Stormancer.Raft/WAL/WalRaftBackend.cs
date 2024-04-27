using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    internal class WalShardBackend<TCommand, TCommandResult, TLogEntry> : IStorageShardBackend<TCommand, TCommandResult,TLogEntry>
        where TCommand : ICommand<TCommand>
        where TCommandResult : ICommandResult<TCommandResult>
        where TLogEntry : IReplicatedLogEntry<TLogEntry>
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

        public Task<IEnumerable<TLogEntry>> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendCommand(TCommand command, [NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendConfigurationChangeCommand(Guid cmdId, ShardsConfigurationRecord configuration, [NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendEntries(IEnumerable<TLogEntry> entries)
        {
            throw new NotImplementedException();
        }

        public bool TryAppendNoOpCommand([NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error)
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
