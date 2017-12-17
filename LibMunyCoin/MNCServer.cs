using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace LibMunyCoin
{
    class MNCServer
    {
        private CancellationTokenSource cancellation;
        private WebSocketListener server;

        Task acceptingTask;

        public MNCServer(int port)
        {
            Console.WriteLine("Starting Echo Server");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

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

            var listenEndPoints = new Uri[] {
                new Uri("ws://0.0.0.0:" + port) // will listen both IPv4 and IPv6
            };

            // starting the server
            server = new WebSocketListener(listenEndPoints, options);

            server.StartAsync().Wait();

            Console.WriteLine("Echo Server listening: " + string.Join(", ", Array.ConvertAll(listenEndPoints, e => e.ToString())) + ".");
            Console.WriteLine("You can test echo server at http://www.websocket.org/echo.html.");

            acceptingTask = AcceptWebSocketsAsync(server, cancellation.Token);
        }

        public void Stop()
        {
            Console.WriteLine("Server stopping.");
            cancellation.Cancel();
            server.StopAsync().Wait();
            acceptingTask.Wait();
            Console.WriteLine("Server stopped");
        }


        private static async Task AcceptWebSocketsAsync(WebSocketListener server, CancellationToken cancellation)
        {
            await Task.Yield();

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var webSocket = await server.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                    if (webSocket == null)
                    {
                        if (cancellation.IsCancellationRequested || !server.IsStarted)
                            break; // stopped

                        continue; // retry
                    }

#pragma warning disable 4014
                    HandleAllIncomingMessagesAsync(webSocket, cancellation);
#pragma warning restore 4014
                }
                catch (OperationCanceledException)
                {
                    /* server is stopped */
                    break;
                }
                catch (Exception acceptError)
                {
                    Console.WriteLine("An error occurred while accepting client.", acceptError);
                }
            }

            Console.WriteLine("Server has stopped accepting new clients.");
        }

        private static async Task HandleAllIncomingMessagesAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            Console.WriteLine($"Client '{webSocket.RemoteEndpoint}' connected.");
            try
            {
                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var messageText = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                        if (messageText == null)
                            break; // webSocket is disconnected

                        //await webSocket.WriteStringAsync(messageText, cancellation).ConfigureAwait(false);

                        Console.WriteLine($"Client '{webSocket.RemoteEndpoint}' sent: {messageText}.");
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception readWriteError)
                    {
                        Console.WriteLine("An error occurred while reading/writing echo message.", readWriteError);
                        await webSocket.CloseAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                webSocket.Dispose();
                Console.WriteLine("Client '" + webSocket.RemoteEndpoint + "' disconnected.");
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine("Unobserved Exception: ", e.Exception);
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception: ", e.ExceptionObject as Exception);
        }
    }
}