using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace WebSocketHelper
{
    public class WebSocketHub : IDisposable
    {
        ConcurrentDictionary<Guid, WebSocketWarpper> _clients = new ConcurrentDictionary<Guid, WebSocketWarpper>();

        public WebSocketWarpper Add(WebSocket socket, int readBufferSize = 2048, int sendBufferSize = 2048)
        {
            if (_clients == null)
                throw new InvalidOperationException("WebSocketHub Disposed");

            var wapper = new WebSocketWarpper(socket, readBufferSize, sendBufferSize);
            _clients.TryAdd(wapper.ID, wapper);
            return wapper;
        }
        public void Send(Guid targetClientID, string message)
        {
            WebSocketWarpper wapper;
            if (_clients.TryGetValue(targetClientID, out wapper))
            {
                wapper.Send(message);
            }
        }
        public void Broadcast(string message)
        {
            Parallel.ForEach(_clients.Values, (client) =>
            {
                client.Send(message);
            });

        }
        public void BroadcastExcept(string message, params Guid[] ids)
        {
            ICollection<Guid> idHash = null;
            if (ids.Count() > 7)
            {
                idHash = new HashSet<Guid>(ids);
            }
            else
            {
                idHash = ids.ToArray();
            }
            Parallel.ForEach(_clients.Values, (client) =>
            {
                if (!idHash.Contains(client.ID))
                    client.Send(message);
            });
        }
        public void Dispose()
        {
            var clients = _clients;
            _clients = null;
            foreach (var c in clients)
            {
                try
                {
                    c.Value.Dispose();
                }
                catch
                {

                }
            }
        }
    }
}
