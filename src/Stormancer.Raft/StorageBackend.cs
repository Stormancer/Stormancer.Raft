using Stormancer.Raft.WAL;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public enum ReplicatedLogEntryType
    {
        SystemClusterConfiguration,
        NoOp,
        Content
    }
    public interface IReplicatedLogEntry<T>  where T: IReplicatedLogEntry<T>
    {

        int GetLength();
        bool TryWrite(Span<byte> buffer, out int length);


        ulong Id { get; }

        ulong Term { get; }

        ReplicatedLogEntryType Type { get; }

       

        /// <summary>
        /// If the entry is of type ClusterConfiguration, returns the stored <see cref="ShardsConfigurationRecord"/>
        /// </summary>
        /// <returns></returns>
        TContent? As<TContent>() where TContent: IRecord<TContent>;
        static abstract bool TryRead(ulong id, ulong term, ReadOnlySpan<byte> content,[NotNullWhen(true)] out T? value);


        static abstract T CreateSystem<TContent>(ulong id, ulong term, ReplicatedLogEntryType type, IRecord content);

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
