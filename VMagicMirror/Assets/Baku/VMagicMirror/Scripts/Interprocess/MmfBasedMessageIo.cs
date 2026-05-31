using System;
using System.Threading.Tasks;
using Baku.VMagicMirror.IpcMessage;
using Cysharp.Threading.Tasks;
using Zenject;

namespace Baku.VMagicMirror.InterProcess
{
    /// <summary> MemoryMappedFile越しでWPFと通信するクラス </summary>
    public class MmfBasedMessageIo : 
        IMessageReceiver, IMessageSender, IMessageDispatcher,
        IReleaseBeforeQuit, ITickable
    {
        [Inject]
        public MmfBasedMessageIo(IIpcTransport transport)
        {
            _transport = transport;
            _transport.ReceiveCommand += OnReceiveCommand;
            _transport.ReceiveQuery += OnReceiveQuery;
        }

        private readonly IpcMessageDispatcher _dispatcher = new();
        private readonly IIpcTransport _transport;

        public event Action<Message> SendingMessage;
        public bool LastMessageSent => _transport.LastMessageSent;
        
        public void SendCommand(Message message, bool isLastMessage = false)
        {
            try
            {
                SendingMessage?.Invoke(message);
            }
            catch (Exception ex)
            {
                LogOutput.Instance.Write(ex);                
            }

            _transport.SendCommand(message.Data, isLastMessage);
        }

        public async Task<string> SendQueryAsync(Message message)
        {
            var data = await _transport.SendQueryAsync(message.Data);
            return new ReceivedCommand(data).GetStringValue();
        }
        
        public void AssignCommandHandler(VmmCommands command, Action<ReceivedCommand> handler)
            => _dispatcher.AssignCommandHandler(command, handler);

        public void AssignQueryHandler(VmmCommands query, Action<ReceivedQuery> handler)
            => _dispatcher.AssignQueryHandler(query, handler);

        public void ReceiveCommand(ReceivedCommand command) => _dispatcher.ReceiveCommand(command);
        
        public void ReleaseBeforeCloseConfig()
        {
            //何もしない: この時点でメッセージI/Oは停止しないでもOK
        }

        public async Task ReleaseResources()
        {
            // NOTE: MMFの処理はメインスレッドと関係ないとこで走っているので明示的に戻らせる
            await using (UniTask.ReturnToMainThread())
            {
                await _transport.StopAsync();
            }
        }

        void ITickable.Tick() => _dispatcher.Tick();

        private void OnReceiveCommand(ReadOnlyMemory<byte> data)
        {
            _dispatcher.ReceiveCommand(new ReceivedCommand(data));
        }
        
        private async void OnReceiveQuery((ushort id, ReadOnlyMemory<byte> data) value)
        {
            var res = await _dispatcher.ReceiveQuery(new ReceivedQuery(value.data));
            var body = MessageSerializer.String((ushort)VmmCommands.Unknown, res);
            _transport.SendQueryResponse(value.id, body);
        }
    }
}
