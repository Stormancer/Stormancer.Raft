using Stormancer.Raft;
using Stormancer.Raft.WAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Logger;

namespace Stormancer.ZoneTree
{
  
    internal class ReplicatedZoneTree<TKey, TValue>
    {
        private readonly IZoneTree<TKey, TValue> _zoneTree;
        private readonly ReplicatedStorageShard<ZoneTreeCommand, ZoneTreeCommandResult,ZoneTreeLogEntry> _shard;
        private readonly WalShardBackend<ZoneTreeCommand, ZoneTreeCommandResult,ZoneTreeLogEntry> _backend;

        public ReplicatedZoneTree(IZoneTree<TKey, TValue> zoneTree, ReplicatedStorageShard<ZoneTreeCommand, ZoneTreeCommandResult, ZoneTreeLogEntry> shard, WalShardBackend<ZoneTreeCommand, ZoneTreeCommandResult,ZoneTreeLogEntry> backend)
        {
            _zoneTree = zoneTree;
            _shard = shard;
            _backend = backend;
        }
        public IZoneTreeMaintenance<TKey, TValue> Maintenance => _zoneTree.Maintenance;

        public bool IsReadOnly { get => _zoneTree.IsReadOnly && _shard.LeaderUid != null; set => _zoneTree.IsReadOnly = value; }

        public ILogger Logger => _zoneTree.Logger;

        public void AtomicUpsert(in TKey key, in TValue value)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(in TKey key)
        {
            throw new NotImplementedException();
        }

        public long Count(TimeSpan maxStale = default)
        {
            throw new NotImplementedException();
        }

        public long CountFullScan(TimeSpan maxStale = default)
        {
            throw new NotImplementedException();
        }

        public IZoneTreeIterator<TKey, TValue> CreateIterator(IteratorType iteratorType = IteratorType.AutoRefresh, TimeSpan maxStale = default, bool includeDeletedRecords = false)
        {
            throw new NotImplementedException();
        }

        public IMaintainer CreateMaintainer()
        {
            throw new NotImplementedException();
        }

        public IZoneTreeIterator<TKey, TValue> CreateReverseIterator(IteratorType iteratorType = IteratorType.AutoRefresh, TimeSpan maxStale = default, bool includeDeletedRecords = false)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void ForceDelete(in TKey key)
        {
            throw new NotImplementedException();
        }

        public bool TryAtomicAdd(in TKey key, in TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryAtomicAddOrUpdate(in TKey key, in TValue valueToAdd, ValueUpdaterDelegate<TValue> valueUpdater)
        {
            throw new NotImplementedException();
        }

        public bool TryAtomicGetAndUpdate(in TKey key, out TValue value, ValueUpdaterDelegate<TValue> valueUpdater)
        {
            throw new NotImplementedException();
        }

        public bool TryAtomicUpdate(in TKey key, in TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryDelete(in TKey key)
        {
            throw new NotImplementedException();
        }

        public bool TryGet(in TKey key, out TValue value, TimeSpan maxStale = default)
        {
            throw new NotImplementedException();
        }

        public bool TryGetAndUpdate(in TKey key, out TValue value, ValueUpdaterDelegate<TValue> valueUpdater)
        {
            throw new NotImplementedException();
        }

        public void Upsert(in TKey key, in TValue value)
        {
            throw new NotImplementedException();
        }
    }
}
