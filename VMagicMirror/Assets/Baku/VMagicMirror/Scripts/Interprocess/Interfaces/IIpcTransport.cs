using System;
using System.Threading.Tasks;

namespace Baku.VMagicMirror.InterProcess
{
    /// <summary>
    /// Transporte base para IPC entre Unity y procesos externos.
    /// El objetivo es mantener el protocolo de mensajes separado del backend de transporte.
    /// </summary>
    public interface IIpcTransport
    {
        event Action<ReadOnlyMemory<byte>> ReceiveCommand;
        event Action<(ushort id, ReadOnlyMemory<byte> data)> ReceiveQuery;

        bool LastMessageSent { get; }

        void SendCommand(ReadOnlyMemory<byte> data, bool isLastMessage = false);
        Task<ReadOnlyMemory<byte>> SendQueryAsync(ReadOnlyMemory<byte> data);
        void SendQueryResponse(ushort id, ReadOnlyMemory<byte> data);
        Task StopAsync();
    }
}
