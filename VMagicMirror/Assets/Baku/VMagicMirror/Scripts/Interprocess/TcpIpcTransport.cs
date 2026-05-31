using System;
using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Baku.VMagicMirror.Mmf;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror.InterProcess
{
    /// <summary>
    /// Transporte IPC alternativo basado en TCP loopback.
    /// Se usa principalmente para Linux como backend experimental.
    /// </summary>
    public class TcpIpcTransport : IIpcTransport
    {
        private const byte PacketTypeCommand = 0;
        private const byte PacketTypeQuery = 1;
        private const byte PacketTypeResponse = 2;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<ReadOnlyMemory<byte>>> _queries = new();
        private readonly object _sendLock = new();

        private Task _acceptLoopTask;
        private NetworkStream _stream;
        private ushort _requestId;

        [Inject]
        public TcpIpcTransport()
        {
            var port = DerivePortFromChannelId(MmfChannelIdSource.ChannelId);
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        }

        public event Action<ReadOnlyMemory<byte>> ReceiveCommand;
        public event Action<(ushort id, ReadOnlyMemory<byte> data)> ReceiveQuery;

        public bool LastMessageSent { get; private set; }

        public void SendCommand(ReadOnlyMemory<byte> data, bool isLastMessage = false)
        {
            if (TrySendPacket(PacketTypeCommand, 0, data) && isLastMessage)
            {
                LastMessageSent = true;
            }
        }

        public Task<ReadOnlyMemory<byte>> SendQueryAsync(ReadOnlyMemory<byte> data)
        {
            if (_stream == null)
            {
                return Task.FromResult(ReadOnlyMemory<byte>.Empty);
            }

            var id = GenerateQueryId();
            var source = new TaskCompletionSource<ReadOnlyMemory<byte>>();
            _queries[id] = source;
            if (!TrySendPacket(PacketTypeQuery, id, data))
            {
                _queries.TryRemove(id, out _);
                return Task.FromResult(ReadOnlyMemory<byte>.Empty);
            }
            return source.Task;
        }

        public void SendQueryResponse(ushort id, ReadOnlyMemory<byte> data)
        {
            TrySendPacket(PacketTypeResponse, id, data);
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            _stream?.Dispose();
            _listener.Stop();
            if (_acceptLoopTask != null)
            {
                try
                {
                    await _acceptLoopTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                    client.NoDelay = true;
                    _stream = client.GetStream();
                    await ReadLoopAsync(_stream, token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    _stream?.Dispose();
                    _stream = null;
                    client?.Dispose();
                }
            }
        }

        private async Task ReadLoopAsync(NetworkStream stream, CancellationToken token)
        {
            var header = new byte[7];
            while (!token.IsCancellationRequested)
            {
                if (!await ReadExactAsync(stream, header, token))
                {
                    break;
                }

                var type = header[0];
                var id = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(1, 2));
                var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(3, 4));
                if (length < 0)
                {
                    break;
                }

                var body = length == 0 ? Array.Empty<byte>() : new byte[length];
                if (length > 0 && !await ReadExactAsync(stream, body, token))
                {
                    break;
                }

                switch (type)
                {
                    case PacketTypeCommand:
                        ReceiveCommand?.Invoke(body);
                        break;
                    case PacketTypeQuery:
                        ReceiveQuery?.Invoke((id, body));
                        break;
                    case PacketTypeResponse:
                        if (_queries.TryRemove(id, out var source))
                        {
                            source.SetResult(body);
                        }
                        break;
                }
            }
        }

        private bool TrySendPacket(byte type, ushort id, ReadOnlyMemory<byte> body)
        {
            var stream = _stream;
            if (stream == null)
            {
                return false;
            }

            var header = new byte[7];
            header[0] = type;
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(1, 2), id);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(3, 4), body.Length);

            try
            {
                lock (_sendLock)
                {
                    stream.Write(header, 0, header.Length);
                    if (body.Length > 0)
                    {
                        var data = body.ToArray();
                        stream.Write(data, 0, data.Length);
                    }
                    stream.Flush();
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token);
                if (read <= 0)
                {
                    return false;
                }
                offset += read;
            }
            return true;
        }

        private static int DerivePortFromChannelId(string channelId)
        {
            // Puerto dentro de rango de puertos de usuario para evitar privilegios.
            // Mapeo determinista por instancia para que Unity y el cliente compartan el mismo endpoint.
            var hash = 17;
            var text = channelId ?? string.Empty;
            for (var i = 0; i < text.Length; i++)
            {
                hash = (hash * 31) + text[i];
            }

            var positive = hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            return 35000 + (positive % 20000);
        }
    }
}
