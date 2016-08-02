using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using WebSocketHelper;

namespace WebSocketTest
{

    public class WSHandler
    {
        static WebSocketHub _hub = new WebSocketHub();
        public static async Task Accept(HttpContext http)
        {
            //small buffer test
            var wap = _hub.Add(await http.WebSockets.AcceptWebSocketAsync(), 8, 8);
            wap.OnTextMessage += (s, m) =>
             {
                 _hub.BroadcastExcept(m, s.ID);
             };
            wap.OnBinaryMessage += (s, d) =>
            {
                _hub.BroadcastExcept(Encoding.Unicode.GetString(d), s.ID);
            };
            wap.OnClose += (s) =>
            {
                Console.WriteLine($"WC disconnected {wap.ID}");
            };
            Console.WriteLine($"WC connected {wap.ID}");
            await wap.Send("這是一個超過8bytes的已連線通知字串，輸入文字按下Send以發送廣播訊息給所有人。");
            await wap.WaitMessageUntilDisconnection();


        }
    }
}
