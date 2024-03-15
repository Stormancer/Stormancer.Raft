using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public interface IReplicatedLogEntry
    {

        int GetLength();
        bool TryWrite(Span<byte> buffer, out int length);


        ulong Id { get; }

        ulong Term { get; }
    }

    public interface IStorageShardBackend<TCommand, TCommandResult>
        where TCommand : ICommand<TCommand>
       where TCommandResult : ICommandResult<TCommandResult>
    {
        ulong LastAppliedLogEntry { get; }

        ulong LastLogEntry { get; }
        ulong LastLogEntryTerm { get; }
        ulong CurrentTerm { get; }




        bool TryAppendCommand(TCommand command, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error);
        bool TryAppendConfigurationChangeCommand(Guid cmdId, ShardsConfigurationRecord configuration, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error);
        bool TryAppendNoOpCommand([NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error);

        bool TryAppendEntries(IEnumerable<IReplicatedLogEntry> entries);

        void ApplyEntries(ulong index);

        Task<IEnumerable<IReplicatedLogEntry>> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm);

        bool TryTruncateEntriesAfter(ulong logEntryId);

        bool TryGetEntryTerm(ulong prevLogId, out ulong entryTerm);
        ValueTask<TCommandResult> WaitCommittedAsync(ulong entryId);
        void UpdateTerm(ulong term);



        ShardsConfigurationRecord CurrentShardsConfiguration { get; }
    }
}
