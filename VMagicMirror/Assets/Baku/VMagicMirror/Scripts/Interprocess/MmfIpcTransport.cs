using System;
using System.Threading.Tasks;
using Baku.VMagicMirror.Mmf;
using Zenject;

namespace Baku.VMagicMirror.InterProcess
{
    /// <summary>
    /// Transporte IPC basado en MemoryMappedFile (backend actual para Windows).
    /// </summary>
    public class MmfIpcTransport : IIpcTransport
    {
        private readonly MemoryMappedFileConnector _connector;

        [Inject]
        public MmfIpcTransport()
        {
            _connector = new MemoryMappedFileConnector();
            _connector.StartAsServer(MmfChannelIdSource.ChannelId);
        }

        public event Action<ReadOnlyMemory<byte>> ReceiveCommand
        {
            add => _connector.ReceiveCommand += value;
            remove => _connector.ReceiveCommand -= value;
        }

        public event Action<(ushort id, ReadOnlyMemory<byte> data)> ReceiveQuery
        {
            add => _connector.ReceiveQuery += value;
            remove => _connector.ReceiveQuery -= value;
        }

        public bool LastMessageSent => _connector.LastMessageSent;

        public void SendCommand(ReadOnlyMemory<byte> data, bool isLastMessage = false)
            => _connector.SendCommand(data, isLastMessage);

        public Task<ReadOnlyMemory<byte>> SendQueryAsync(ReadOnlyMemory<byte> data)
            => _connector.SendQueryAsync(data);

        public void SendQueryResponse(ushort id, ReadOnlyMemory<byte> data)
            => _connector.SendQueryResponse(id, data);

        public Task StopAsync() => _connector.StopAsync();
    }
}
