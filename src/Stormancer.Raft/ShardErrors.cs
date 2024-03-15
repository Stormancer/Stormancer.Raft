using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public class ShardErrors
    {
        public static ErrorId OperationFailedNotReplicated { get; } = Errors.Register(5001, "Shard.OperationFailed.NotReplicated", "The leader failed to replicate the operation before stepping down.");
        public static ErrorId NotLeader { get;  } = Errors.Register(5002, "Shard.NotLeader", "This operation can only be performed on the leader.");
    }
}
