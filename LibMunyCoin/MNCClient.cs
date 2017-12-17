using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace LibMunyCoin
{
    class MNCClient
    {
        WebSocketClient client;
        CancellationTokenSource cancellation;
        Task<WebSocket> connectingTask;
        List<WebSocket> webSockets = new List<WebSocket>();

        
        public MNCClient()
        {

            cancellation = new CancellationTokenSource();

            var bufferSize = 1024 * 8; // 8KiB
            var bufferPoolSize = 100 * bufferSize; // 800KiB pool

            var options = new WebSocketListenerOptions
            {
                SubProtocols = new[] { "text" },
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                PingMode = PingMode.Manual,
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize)
            };
            options.Standards.RegisterRfc6455(factory =>
            {
                factory.MessageExtensions.RegisterDeflateCompression();
            });
            // configure tcp transport
            options.Transports.ConfigureTcp(tcp =>
            {
                tcp.BacklogSize = 100; // max pending connections waiting to be accepted
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            // adding the WSS extension
            //var certificate = new X509Certificate2(File.ReadAllBytes("<PATH-TO-CERTIFICATE>"), "<PASSWORD>");
           // options.ConnectionExtensions.RegisterSecureConnection(certificate);

           client = new WebSocketClient(options);
        }

        public WebSocket Connect(string host)
        {
            connectingTask = client.ConnectAsync(new Uri(host), cancellation.Token);

            Console.WriteLine("Connecting");

            connectingTask.Wait(cancellation.Token);

            var webSocket = connectingTask.Result;

            webSockets.Add(webSocket);

            Console.WriteLine($"Connected to {webSocket.RemoteEndpoint.ToString()}");

            return webSocket;
        }

        public void DisconnectAll()
        {
            webSockets.ForEach(webSocket => {
                if (webSocket.IsConnected) webSocket.CloseAsync().Wait();
            });
        }
    }
}