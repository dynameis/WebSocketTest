using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketHelper
{
    public delegate void WebSocketTextDataRecivedHandler(WebSocketWarpper sender, string message);
    public delegate void WebSocketBinaryDataRecivedHandler(WebSocketWarpper sender, byte[] data);
    public delegate void WebSocketDisconnectHandler(WebSocketWarpper sender);

    public class WebSocketWarpper : IDisposable
    {
        private System.IO.MemoryStream _recivedData = new System.IO.MemoryStream();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private WebSocket _socket;
        private CancellationTokenSource _source = new CancellationTokenSource();

        byte[] _reciveBuffer;
        ArraySegment<byte> _reciveBufferSegment;
        int _sendBufferSize;
        public WebSocketWarpper(WebSocket socket, int readBufferSize = 2048, int sendBufferSize = 2048)
        {
            _socket = socket;
            _reciveBuffer = new byte[readBufferSize];
            _reciveBufferSegment = new ArraySegment<byte>(_reciveBuffer);
            _sendBufferSize = sendBufferSize;
        }
        public Guid ID { get; } = Guid.NewGuid();
        public event WebSocketDisconnectHandler OnClose;

        public event WebSocketTextDataRecivedHandler OnTextMessage;
        public event WebSocketBinaryDataRecivedHandler OnBinaryMessage;
        public void Dispose()
        {
            _source.Cancel();
            _source.Dispose();
            _recivedData.Dispose();
            if (_socket != null) _socket.Dispose();
        }

        public async Task Send(string message)
        {
            if (_socket.State != WebSocketState.Open) return;
            await Task.Yield();
            //make sure is only one pedding send at same time
            await _semaphore.WaitAsync();
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    var source = Encoding.UTF8.GetBytes(message);
                    var length = source.Length;
                    int pos = 0;
                    do
                    {
                        if (_source.Token.IsCancellationRequested) return;
                        int take = _sendBufferSize;
                        if (pos + take > length)
                        {
                            take = length - pos;
                        }
                        var buffer = new ArraySegment<byte>(source, pos, take);
                        pos += take;
                        if (_socket.State == WebSocketState.Open)
                        {
                            await _socket.SendAsync(buffer, WebSocketMessageType.Text, pos >= length, _source.Token);
                        }
                        else
                        {
                            return;
                        }
                    } while (pos < length);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task WaitMessageUntilDisconnection()
        {
            await Task.Yield();

            while (_socket.State == WebSocketState.Open)
            {

                var result = await _socket.ReceiveAsync(_reciveBufferSegment, _source.Token);

                if (_source.Token.IsCancellationRequested) return;
                if (result.MessageType != WebSocketMessageType.Close)
                {
                    if (_source.Token.IsCancellationRequested) return;
                    await _recivedData.WriteAsync(_reciveBuffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        _recivedData.Position = 0;
                        var data = _recivedData.ToArray();
                        _recivedData.SetLength(0);
                        switch (result.MessageType)
                        {
                            case WebSocketMessageType.Binary:
                                if (OnBinaryMessage != null)
                                    Task.Run(() => OnBinaryMessage(this, data));
                                break;
                            case WebSocketMessageType.Text:
                                string decode = null;
                                try
                                {
                                    decode = Encoding.UTF8.GetString(data);
                                }
                                catch
                                {
                                    decode = "UTF8 decode failure";
                                }
                                if (OnTextMessage != null)
                                    Task.Run(() => OnTextMessage(this, decode));
                                break;
                        }
                    }
                }
            }
            OnClose?.Invoke(this);
        }
    }
}