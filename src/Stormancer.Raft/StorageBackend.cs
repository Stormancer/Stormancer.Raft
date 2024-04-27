using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public interface IReplicatedLogEntry<T> where T: IReplicatedLogEntry<T>
    {

        int GetLength();
        bool TryWrite(Span<byte> buffer, out int length);


        ulong Id { get; }

        ulong Term { get; }

        static abstract bool TryRead(ulong id, ulong term, ReadOnlySpan<byte> content,[NotNullWhen(true)] out T? value);
    }

    public interface ISerializedEntry
    {
        ulong Id { get; }
        ulong Term { get; }

        TLogEntry ReadAs<TLogEntry>() where TLogEntry : IReplicatedLogEntry<TLogEntry>;
    }

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
        bool TryAppendConfigurationChangeCommand(Guid cmdId, ShardsConfigurationRecord configuration, [NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error);
        bool TryAppendNoOpCommand([NotNullWhen(true)] out TLogEntry? entry, [NotNullWhen(false)] out Error? error);

        bool TryAppendEntries(IEnumerable<TLogEntry> entries);

        void ApplyEntries(ulong index);

        Task<IEnumerable<TLogEntry>> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm);

        bool TryTruncateEntriesAfter(ulong logEntryId);

        bool TryGetEntryTerm(ulong prevLogId, out ulong entryTerm);
        ValueTask<TCommandResult> WaitCommittedAsync(ulong entryId);
        void UpdateTerm(ulong term);



        ShardsConfigurationRecord CurrentShardsConfiguration { get; }
    }
}
