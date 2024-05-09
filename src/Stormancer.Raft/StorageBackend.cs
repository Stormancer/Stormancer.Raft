using Stormancer.Raft.WAL;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    

    public interface IStorageShardBackend<TCommand, TCommandResult, TLogEntry>
        where TCommand : ICommand<TCommand>
       where TCommandResult : ICommandResult<TCommandResult>
        where TLogEntry : IReplicatedLogEntry<TLogEntry>
    {
        ulong LastAppliedLogEntry { get; }

        ulong LastLogEntry { get; }
        ulong LastLogEntryTerm { get; }
        ulong CurrentTerm { get; }




        bool TryAppendCommand(TCommand command, [NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error);
        
        bool TryAppendEntries(IEnumerable<TLogEntry> entries);

        void ApplyEntries(ulong index);

        ValueTask<GetEntriesResult<TLogEntry>> GetEntries( ulong firstEntryId,  ulong lastEntryId);

        bool TryTruncateEntriesAfter(ulong logEntryId);

        bool TryGetEntryTerm(ulong prevLogId, out ulong entryTerm);
        ValueTask<TCommandResult> WaitCommittedAsync(ulong entryId);
        void UpdateTerm(ulong term);



        ShardsConfigurationRecord CurrentShardsConfiguration { get; }
    }
}
