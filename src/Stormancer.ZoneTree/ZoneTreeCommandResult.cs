using Stormancer.Raft;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.ZoneTree
{
    internal class ZoneTreeCommandResult : ICommandResult<ZoneTreeCommandResult>
    {
        public Guid OperationId => throw new NotImplementedException();

        public bool Success => throw new NotImplementedException();

        public Error? Error => throw new NotImplementedException();

        public static ZoneTreeCommandResult CreateFailed(Guid operationId, Error error)
        {
            throw new NotImplementedException();
        }

        public static bool TryRead(ReadOnlySequence<byte> buffer, out int bytesRead, [NotNullWhen(true)] out ZoneTreeCommandResult? result)
        {
            throw new NotImplementedException();
        }

        public int GetLength()
        {
            throw new NotImplementedException();
        }

        public void Write(Span<byte> span)
        {
            throw new NotImplementedException();
        }
    }
}
