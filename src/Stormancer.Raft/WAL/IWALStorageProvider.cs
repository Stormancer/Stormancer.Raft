using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{

    public interface IWALStorageProvider
    {
        IWALSegment GetOrCreateSegment(string category, int segmentId);
        bool TryReadMetadata<TMetadataContent>([NotNullWhen(true)] out LogMetadata<TMetadataContent>? metadata) where TMetadataContent : IRecord<TMetadataContent>;
        void SaveMetadata<TMetadataContent>(LogMetadata<TMetadataContent> metadata) where TMetadataContent : IRecord<TMetadataContent>;
    }
}
