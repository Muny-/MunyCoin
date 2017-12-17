using System;
using System.Net;
using vtortola.WebSockets;

namespace LibMunyCoin
{
    public class MunyCoin
    {
        MNCServer server;

        MNCClient client;

        public MunyCoin()
        {
            int randPort = new Random().Next(1000, 65535);

            server = new MNCServer(randPort);

            client = new MNCClient();
            
            var webSocket = client.Connect("ws://coin.muny.us:" + randPort);

            while(true)
            {
                string msg = Console.ReadLine();

                if (msg == "stop")
                {
                    Stop();
                    break;
                }
                else
                    webSocket.WriteStringAsync(msg);
            }
        }

        public void Stop()
        {
            server.Stop();

            client.DisconnectAll();
        }
    }
}
