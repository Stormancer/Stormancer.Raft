using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    internal class LogMetadata
    {
        public int Version { get; set; } = 1;

        public int CurrentSegmentId { get; set; } = 0;

        public List<ulong> SegmentsStarts { get; set; } = new List<ulong>();

    }
}
