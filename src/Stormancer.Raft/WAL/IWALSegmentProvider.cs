using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{

    public interface IWALSegmentProvider
    {
        IWALSegment GetOrCreateSegment(string category, int currentSegmentId);
    }
}
