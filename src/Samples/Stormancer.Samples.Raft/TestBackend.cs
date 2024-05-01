using Microsoft.Extensions.ObjectPool;
using Stormancer.Raft;
using Stormancer.Threading;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Stormancer.Raft
{
    internal struct IncrementOperation : ICommand<IncrementOperation>
    {
        public IncrementOperation(int value = 1)
        {
            Value = value;
            Id = Guid.NewGuid();
        }

        public IncrementOperation(Guid id, int value = 1)
        {
            Value = value;
            Id = id;
        }
        public int Value { get; } = 1;

        public Guid Id { get; }

        public static bool TryRead(ReadOnlySequence<byte> buffer, out int bytesRead, [NotNullWhen(true)] out IncrementOperation operation)
        {
            if (buffer.Length < 12)
            {
                bytesRead = 0;
                operation = default;
                return false;
            }


            var reader = new SequenceReader<byte>(buffer);

            Guid id;

            var slice = reader.Sequence.Slice(0, 16);
            if (slice.IsSingleSegment)
            {
                id = new Guid(slice.FirstSpan);
            }
            else
            {
                Span<byte> idBuffer = stackalloc byte[16];
                slice.CopyTo(idBuffer);
                id = new Guid(idBuffer);
            }
            reader.Advance(16);

            if (reader.TryReadBigEndian(out int value))
            {
                operation = new IncrementOperation(id, value);
                bytesRead = 20;
                return true;
            }
            else
            {
                bytesRead = 0;
                operation = default;
                return false;
            }
        }
        public int GetLength() => 20;
        public void Write(Span<byte> span)
        {

            Id.TryWriteBytes(span.Slice(0, 16));
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(16), Value);

        }
    }

    internal struct IncrementResult : ICommandResult<IncrementResult>
    {
        public IncrementResult(Guid guid, BigInteger value)
        {
            OperationId = guid;
            Success = true;
            CurrentValue = value;
        }
        public IncrementResult(Guid guid, Error? error)
        {

            OperationId = guid;
            Error = error;
            Success = false;
        }


        [MemberNotNullWhen(false, "Error")]
        public bool Success { get; }

        public Error? Error { get; }

        public Guid OperationId { get; }

        public BigInteger CurrentValue { get; }

        public static IncrementResult CreateFailed(Guid operationId, Error error)
        {
            return new IncrementResult(operationId, error);
        }

        private static (bool isSuccess, byte resultLength) DecodeFlags(byte flag)
        {
            return ((flag & 1) == 1, (byte)(flag >> 1));
        }

        private static byte EncodeFlag(bool isSuccess, byte resultLength)
        {
            var v = resultLength << 1;
            if (isSuccess)
            {
                v = v | 1;
            }
            return (byte)v;
        }

        public static bool TryRead(ReadOnlySequence<byte> buffer, out int bytesRead, [NotNullWhen(true)] out IncrementResult result)
        {
            var reader = new SequenceReader<byte>(buffer);

            //success
            // Guid | flags | result
            //


            Guid id;
            if (reader.Sequence.Length < 17)
            {
                result = default;
                bytesRead = 0;
                return false;
            }
            var slice = reader.Sequence.Slice(0, 16);
            if (slice.IsSingleSegment)
            {
                id = new Guid(slice.FirstSpan);
            }
            else
            {
                Span<byte> idBuffer = stackalloc byte[16];
                slice.CopyTo(idBuffer);
                id = new Guid(idBuffer);
            }
            reader.Advance(16);
            reader.TryRead(out var flags);

            var (success, length) = DecodeFlags(flags);

            if (success)
            {
                if (buffer.Length < 17 + length)
                {
                    result = default;
                    bytesRead = 0;
                    return false;
                }
                else
                {
                    slice = buffer.Slice(17, length);
                    if (slice.IsSingleSegment)
                    {
                        var r = new BigInteger(slice.FirstSpan);
                        result = new IncrementResult(id, r);
                        bytesRead = 17 + length;
                        return true;
                    }
                    else
                    {
                        Span<byte> span = stackalloc byte[length];
                        slice.CopyTo(span);

                        var r = new BigInteger(span);
                        result = new IncrementResult(id, r);
                        bytesRead = 17 + length;
                        return true;

                    }

                }



            }
            else
            {
                if (Errors.TryRead(buffer.Slice(9), out var errorBytesRead, out var error))
                {
                    bytesRead = errorBytesRead + 17;
                    result = new IncrementResult(id, error);
                    return true;
                }
                else
                {
                    bytesRead = 0;
                    result = default;
                    return false;
                }
            }
        }

        public int GetLength() => Success ? 17 + CurrentValue.GetByteCount() : Errors.GetLength(Error);
        public void Write(Span<byte> span)
        {

            var length = (byte)CurrentValue.GetByteCount();

            this.OperationId.TryWriteBytes(span.Slice(0, 16));
            span[16] = EncodeFlag(Success, length);


            var r = (ICommandResult<IncrementResult>)this;
            if (r.Success)
            {
                CurrentValue.TryWriteBytes(span.Slice(17), out _);

            }
            else
            {
                var errorSpan = span[17..];
                Errors.Write(ref errorSpan, r.Error);
            }

        }
    }

    internal class IncrementBackend : IStorageShardBackend<IncrementOperation, IncrementResult>
    {

       
        private class ClusterConfigurationEntry : IReplicatedLogEntry
        {
            public ClusterConfigurationEntry(ulong term, ulong id, ShardsConfigurationRecord shardsConfig)
            {
                Term = term;
                Id = id;
                ShardsConfiguration = shardsConfig;
            }

            public ClusterConfigurationEntry(ulong term, ulong id, Span<byte> buffer)
            {
                Term = term;
                Id = id;
                if (ShardsConfigurationRecord.TryRead(buffer[1..], out var config, out _))
                {
                    ShardsConfiguration = config;
                }
                else
                {
                    throw new InvalidOperationException("failed to create entry from binary data.");
                }
            }

            public ulong Id { get; }

            public ulong Term { get; }

            public ShardsConfigurationRecord ShardsConfiguration { get; }


            public int GetLength()
            {
                return ShardsConfiguration.GetLength() + 1;
            }

            public bool TryWrite(Span<byte> buffer, out int length)
            {
                length = GetLength() + 1;
                buffer[0] = 1;
                return ShardsConfiguration.TryWrite(buffer[1..]);
            }
        }

        private LinkedList<IReplicatedLogEntry> _entries = new LinkedList<IReplicatedLogEntry>();
        private LinkedListNode<IReplicatedLogEntry>? _lastAppliedEntry = null;
        private Dictionary<ulong, AsyncOperationWithData<Guid, IncrementResult>> _pendingOperations = new();
        private ObjectPool<AsyncOperationWithData<Guid, IncrementResult>> _operationPool = new DefaultObjectPool<AsyncOperationWithData<Guid, IncrementResult>>(new AsyncOperationWithDataPoolPolicy<Guid, IncrementResult>());
        private BigInteger _currentValue = 0;

        public BigInteger CurrentValue => _currentValue;

        public ulong LastAppliedLogEntry => _lastAppliedEntry?.Value.Id ?? 0;

        public ulong LastLogEntry => _entries.Last?.ValueRef.Id ?? 0;

        public ulong LastLogEntryTerm => _entries.Last?.ValueRef.Term ?? 0;

        public ulong CurrentTerm { get; private set; }

        public ShardsConfigurationRecord CurrentShardsConfiguration { get; private set; } = new ShardsConfigurationRecord(null, null);

        public bool TryAppendEntries(IEnumerable<IReplicatedLogEntry> entries)
        {
            lock (_syncRoot)
            {
                if (entries.Any())
                {
                    LinkedListNode<IReplicatedLogEntry>? currentNode = null;
                    var first = true;


                    foreach (var entry in entries)
                    {

                        if (currentNode == null && first)
                        {
                            TryFindNode(entry.Id, out currentNode);
                        }
                        first = false;

                        if (currentNode != null)
                        {
                            if (currentNode.ValueRef.Id != entry.Id)
                            {
                                return false;
                            }

                            if (currentNode.ValueRef.Term != entry.Term)
                            {

                                var logEntry = CreateEntry(entry);
                                if (_pendingOperations.Remove(entry.Id, out var operation))
                                {
                                    operation.TrySetResult(IncrementResult.CreateFailed(operation.Item, new Error(ShardErrors.OperationFailedNotReplicated, null)));
                                }
                                currentNode.Value = logEntry;
                            }
                            currentNode = currentNode.Next;
                        }
                        else
                        {
                            if (_entries.Last == null || _entries.Last.ValueRef.Id == entry.Id - 1)
                            {
                                var logEntry = CreateEntry(entry);
                                Append(logEntry);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
        }

        public bool TryTruncateEntriesAfter(ulong logEntryId)
        {
            lock (_syncRoot)
            {
                if (LastAppliedLogEntry <= logEntryId)
                {
                    if (TryFindNode(logEntryId, out var currentNode))
                    {
                        while (_entries.Last != currentNode)
                        {
                            Debug.Assert(_entries.Last != null);
                            if (_pendingOperations.Remove(_entries.Last.ValueRef.Id, out var operation))
                            {
                                operation.TrySetResult(IncrementResult.CreateFailed(operation.Item, new Error(ShardErrors.OperationFailedNotReplicated, null)));
                            }
                            _entries.RemoveLast();
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                }



            }
        }

        private void Append(IReplicatedLogEntry entry)
        {
            _entries.AddLast(entry);
            if (entry is ClusterConfigurationEntry configEntry)
            {
                CurrentShardsConfiguration = configEntry.ShardsConfiguration;
            }
        }

        public bool TryAppendCommand(IncrementOperation command, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            lock (_syncRoot)
            {
                var e = new Entry(CurrentTerm, LastLogEntry + 1, command.Value);
                Append(e);

                var op = _operationPool.Get();
                op.Item = command.Id;
                _pendingOperations.Add(e.Id, op);
                entry = e;
                error = default;
                return true;
            }
        }


        public bool TryAppendConfigurationChangeCommand(Guid cmdId, ShardsConfigurationRecord configuration, [NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            lock (_syncRoot)
            {
                var e = new ClusterConfigurationEntry(CurrentTerm, LastLogEntry + 1, configuration);
                Append(e);

                var op = _operationPool.Get();
                op.Item = cmdId;
                _pendingOperations.Add(e.Id, op);
                entry = e;
                error = default;
                return true;
            }
        }

        public bool TryAppendNoOpCommand([NotNullWhen(true)] out IReplicatedLogEntry? entry, [NotNullWhen(false)] out Error? error)
        {
            return TryAppendCommand(new IncrementOperation(0), out entry, out error);
        }


        public ValueTask<IncrementResult> WaitCommittedAsync(ulong entryId)
        {

            if (_pendingOperations.TryGetValue(entryId, out var operation))
            {
                return operation.ValueTaskOfT;
            }
            else if (LastAppliedLogEntry >= entryId)
            {
                return ValueTask.FromResult(new IncrementResult(Guid.Empty, _currentValue));
            }
            else
            {
                return ValueTask.FromException<IncrementResult>(new InvalidOperationException($"No pending operation for {entryId}"));
            }
        }


        private IReplicatedLogEntry CreateEntry(IReplicatedLogEntry entry)
        {
            IReplicatedLogEntry CreateEntry(ulong term, ulong id, ref Span<byte> buffer)
            {
                var type = buffer[0];
                if (type == 0)
                {
                    return new Entry(entry.Term, entry.Id, buffer);
                }
                else if (type == 1)
                {
                    return new ClusterConfigurationEntry(entry.Term, entry.Id, buffer);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }


            var length = entry.GetLength();



            if (length < 128)
            {
                Span<byte> buffer = stackalloc byte[length];

                entry.TryWrite(buffer, out length);

                return CreateEntry(entry.Term, entry.Id, ref buffer);

            }
            else
            {
                using var owner = MemoryPool<byte>.Shared.Rent(length);
                Span<byte> buffer = owner.Memory.Span;
                entry.TryWrite(buffer, out length);

                return CreateEntry(entry.Term, entry.Id, ref buffer);
            }
        }

        private bool TryFindNode(ulong id, [NotNullWhen(true)] out LinkedListNode<IReplicatedLogEntry>? entryNode)
        {
            if (id > LastLogEntry)
            {
                entryNode = null;
                return false;
            }

            if (id == 1)
            {
                if (_entries.First != null)
                {
                    entryNode = _entries.First;
                    return true;
                }
                else
                {
                    entryNode = null;
                    return false;
                }
            }

            for (var node = _entries.Last; node != null; node = node.Previous)
            {
                if (node.ValueRef.Id == id)
                {
                    entryNode = node;
                    return true;
                }

            }
            entryNode = null;
            return false;
        }

        private object _syncRoot = new object();
        public void ApplyEntries(ulong index)
        {
            lock (_syncRoot)
            {
                if (index > LastAppliedLogEntry)
                {



                    for (var current = _lastAppliedEntry != null ? _lastAppliedEntry.Next : _entries.First; current != null && current.ValueRef.Id <= index; current = current.Next)
                    {
                        var value = current.Value;
                        if (value is Entry e)
                        {
                            _currentValue += e.Value;

                        }
                        else if (value is ClusterConfigurationEntry configEntry)
                        {
                            this.CurrentShardsConfiguration = configEntry.ShardsConfiguration;
                        }
                        _lastAppliedEntry = current;
                        if (_pendingOperations.Remove(value.Id, out var op))
                        {
                            op.TrySetResult(new IncrementResult(op.Item, _currentValue));
                        }
                    }
                }
            }
        }

        public Task<IEnumerable<IReplicatedLogEntry>> GetEntries(ref ulong firstEntryId, ref ulong lastEntryId, out ulong prevLogEntryId, out ulong prevLogEntryTerm)
        {
            LinkedListNode<IReplicatedLogEntry>? firstEntry = null;
            LinkedListNode<IReplicatedLogEntry>? lastEntry = null;
            if (_entries.Last == null)
            {
                prevLogEntryId = 0;
                prevLogEntryTerm = 0;
                return Task.FromResult(Enumerable.Empty<IReplicatedLogEntry>());
            }

            if (firstEntryId > lastEntryId)
            {
                prevLogEntryId = LastLogEntry;
                prevLogEntryTerm = LastLogEntryTerm;
                return Task.FromResult(Enumerable.Empty<IReplicatedLogEntry>());
            }
            if (firstEntryId <= 1)
            {
                firstEntry = _entries.First;
            }

            if (lastEntryId >= _entries.Last.ValueRef.Id)
            {
                lastEntry = _entries.Last;
            }

            for (var currentNode = _entries.Last; currentNode != null; currentNode = currentNode.Previous)
            {
                if (firstEntryId == currentNode.ValueRef.Id)
                {
                    firstEntry = currentNode;
                }
                if (lastEntryId == currentNode.ValueRef.Id)
                {
                    lastEntry = currentNode;
                }
                if (firstEntry != null && lastEntry != null)
                {
                    break;
                }


            }


            if (firstEntry == null)
            {
                firstEntry = _entries.First;
            }
            if (lastEntry == null)
            {
                lastEntry = _entries.Last;
            }

            if (firstEntry == null || lastEntry == null)
            {
                firstEntryId = 0;
                lastEntryId = 0;
                prevLogEntryId = 0;
                prevLogEntryTerm = 0;
                return Task.FromResult(Enumerable.Empty<IReplicatedLogEntry>());
            }


            firstEntryId = firstEntry.ValueRef.Id;
            lastEntryId = lastEntry.ValueRef.Id;
            var prevEntry = firstEntry.Previous;
            if (prevEntry != null)
            {
                prevLogEntryId = prevEntry.ValueRef.Id;
                prevLogEntryTerm = prevEntry.ValueRef.Term;
            }
            else
            {
                prevLogEntryId = 0;
                prevLogEntryTerm = 0;
            }


            IEnumerable<IReplicatedLogEntry> Enumerate(LinkedListNode<IReplicatedLogEntry> first, LinkedListNode<IReplicatedLogEntry> last)
            {
                var current = first;

                while (current != last)
                {
                    yield return current.Value;
                    current = current.Next;

                }
                yield return last.Value;
            }


            return Task.FromResult(Enumerate(firstEntry, lastEntry));
        }

        public bool TryGetEntryTerm(ulong entryId, out ulong entryTerm)
        {
            if (TryFindNode(entryId, out var node))
            {
                entryTerm = node.ValueRef.Term;
                return true;
            }
            else
            {
                entryTerm = default;
                return false;
            }
        }

        public void UpdateTerm(ulong term)
        {
            CurrentTerm = term;
        }

       
    }

    internal class MessageChannel : IReplicatedStorageMessageChannel
    {
        MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
        private class ShardInstance
        {
            public ShardInstance(IReplicatedStorageMessageHandler handler)
            {
                Handler = handler;
            }

            public IReplicatedStorageMessageHandler Handler { get; }
        }

        private readonly Dictionary<Guid, ShardInstance> _shards = new Dictionary<Guid, ShardInstance>();
        private readonly int _latency;

        public MessageChannel(int latency)
        {
            _latency = latency;
        }

        public void AddShard(Guid id, IReplicatedStorageMessageHandler shard)
        {
            _shards.Add(id, new ShardInstance(shard));
        }

        public void RemoveShard(Guid id)
        {
            _shards.Remove(id);
        }

        public void AppendEntries(Guid origin, Guid destination, ulong term, IEnumerable<IReplicatedLogEntry> entries, ulong lastLeaderEntryId, ulong prevLogIndex, ulong prevLogTerm, ulong leaderCommit)
        {
            if (_shards.TryGetValue(destination, out var shard))
            {
                async Task AppendEntriesImpl()
                {
                    await Task.Delay(_latency);
                    var result = shard.Handler.OnAppendEntries(term, origin, entries,lastLeaderEntryId, prevLogIndex, prevLogTerm, leaderCommit);
                    await Task.Delay(_latency);
                    if (_shards.TryGetValue(origin, out var originShard))
                    {
                        originShard.Handler.OnAppendEntriesResult(destination, result.LeaderId, result.Term, prevLogIndex + 1, result.LastLogEntryId, result.LastAppliedLogEntryId, result.Success);
                    }
                }
                _ = AppendEntriesImpl();
            }
            else
            {
                if (_shards.TryGetValue(origin, out var originShard))
                {
                    originShard.Handler.OnAppendEntriesResult(destination, origin, 0, 0, 0, 0, false);
                }
            }
        }



        public bool IsShardConnected(Guid shardUid)
        {
            return _shards.ContainsKey(shardUid);
        }

        public void ForwardOperationToPrimary(Guid origin, Guid leaderUid, ref ReadOnlySpan<byte> operation)
        {
            if (_shards.TryGetValue(leaderUid, out var shard))
            {

                using var owner = _memoryPool.Rent(operation.Length);
                operation.CopyTo(owner.Memory.Span);
                shard.Handler.TryProcessForwardOperation(origin, new ReadOnlySequence<byte>(owner.Memory.Slice(0, operation.Length)), out _);

            }
        }

        public void SendForwardOperationResult(Guid origin, Guid destination, ref ReadOnlySpan<byte> result)
        {
            if (_shards.TryGetValue(destination, out var shard))
            {
                using var owner = _memoryPool.Rent(result.Length);
                result.CopyTo(owner.Memory.Span);
                shard.Handler.ProcessForwardOperationResponse(origin, new ReadOnlySequence<byte>(owner.Memory.Slice(0, result.Length)), out _);
            }
        }

        public async Task<RequestVoteResult> RequestVoteAsync(Guid candidateId, Guid destination, ulong term, ulong lastLogIndex, ulong lastLogTerm)
        {
            if (_shards.TryGetValue(destination, out var state))
            {
                await Task.Delay(_latency);
                var result = state.Handler.OnRequestVote(term, candidateId, lastLogIndex, lastLogTerm);
                await Task.Delay(_latency);
                result.RequestSuccess = true;
                return result;
            }
            else
            {
                return new RequestVoteResult { Term = term, VoteGranted = false, RequestSuccess = false };
            }

        }

        public async Task<AppendEntriesResult> AppendEntriesAsync(Guid origin, Guid destination, ulong term, IEnumerable<IReplicatedLogEntry> entries, ulong lastLeaderEntryId, ulong prevLogIndex, ulong prevLogTerm, ulong leaderCommit)
        {
            if (_shards.TryGetValue(destination, out var shard))
            {
                await Task.Delay(_latency);
                var result = shard.Handler.OnAppendEntries(term, origin, entries, lastLeaderEntryId, prevLogIndex, prevLogTerm, leaderCommit);
                await Task.Delay(_latency);

                return result;
            }
            else
            {
                return new AppendEntriesResult();
                
            }
        }
    }
}
