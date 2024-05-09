using Stormancer.Raft;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.ZoneTree
{
    internal class ZoneTreeCommand : ICommand<ZoneTreeCommand>
    {
        public Guid Id => throw new NotImplementedException();

        public bool IsSystem => throw new NotImplementedException();

        public static ZoneTreeCommand CreateSystemCommand(object? systemCommand)
        {
            throw new NotImplementedException();
        }

        public static bool TryRead(ReadOnlySequence<byte> buffer, out int bytesRead, [NotNullWhen(true)] out ZoneTreeCommand? operation)
        {
            throw new NotImplementedException();
        }

        public TSystemCommandContent AsSystem<TSystemCommandContent>() where TSystemCommandContent : IRecord<TSystemCommandContent>
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
