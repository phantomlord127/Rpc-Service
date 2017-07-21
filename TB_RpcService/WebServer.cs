using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;

namespace TB_RpcService
{
    public class WebServer
    {
        private static HttpListener _listener;
        private static byte[] _buffer = new byte[4096];
        private static CancellationTokenSource _ctSource = new CancellationTokenSource();
        private static CancellationToken _token = _ctSource.Token;
        static object[] services = new object[] {
           new ExampleCalculatorService()
        };

        public void Start()
        {
            //Config Austin Harris Rpc
            Config.SetErrorHandler(OnJsonRpcException);
            Config.SetPreProcessHandler(new PreProcessHandler(PreProcess));
            // Start up the HttpListener on the passes Uri.  
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:8080/httpSocket/");
            _listener.Start();
            Console.WriteLine("Listening...");
            _listener.BeginGetContext(ContextCallback, _listener);

        }

        private JsonRpcException PreProcess(JsonRequest request, object context)
        {
            JObject j = request.Params as JObject;
            if (j == null || ! j.First.HasValues || j.First.First.ToString() != "test")
            {
                throw new JsonRpcException(-32602, "Invalid Token", null);
            }
            return null;
        }

        private JsonRpcException OnJsonRpcException(JsonRequest request, JsonRpcException ex)
        {
            ex.data = null;
            return ex;
        }

        private void ContextCallback(IAsyncResult result)
        {
            // Accept the HttpListenerContext
            HttpListener listener = (HttpListener)result.AsyncState;
            if (! listener.IsListening)
            {
                //throw new Exception("Listener disposed");
                Console.WriteLine("Listening is stopped.");
                return;
            }
            HttpListenerContext listenerContext = listener.EndGetContext(result);
            listener.BeginGetContext(ContextCallback, listener);
            // Check if this is for a websocket request 
            if (listenerContext.Request.IsWebSocketRequest)
            {
                Console.WriteLine($"Connection Request by: {listenerContext.Request.RemoteEndPoint.Address}");
                string webSocketKey = listenerContext.Request.Headers.Get("Sec-WebSocket-Key");
                //listenerContext.Response.AddHeader("Sec-WebSocket-Accept", AcceptKey(ref webSocketKey));
                ProcessRequest(listenerContext);
            }
            else
            {
                // Since we are expecting WebSocket requests and this is not - send HTTP 400 
                listenerContext.Response.StatusCode = 400;
                listenerContext.Response.Close();
                Console.WriteLine("Connection Request is not a Websocket Request.");
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }

        private async void ProcessRequest(HttpListenerContext listenerContext)
        {
            WebSocketContext webSocketContext = null;
            try
            {
                // Accept the WebSocket request 
                webSocketContext = await listenerContext.AcceptWebSocketAsync(null, new TimeSpan(0, 0, 30));
            }
            catch (Exception ex)
            {
                // If any error occurs then send HTTP Status 500 
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Console.WriteLine("Exception : {0}", ex.Message);
                return;
            }
            // Accept the WebSocket connect.
            byte[] buffer = new byte[256];
            ArraySegment<byte> bufferSegment = new ArraySegment<byte>(buffer);
            WebSocket webSocket = webSocketContext.WebSocket;
            while (webSocket.State != WebSocketState.Closed)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(bufferSegment, _token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (webSocket.State == WebSocketState.CloseReceived)
                    {
                        await Task.Delay(100); //Warum muss hier noch kurz gewartet werden? Ohne diese Zeile wird die Verbindung getrennt, trotz noch zu sendenden Text.
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by Client.", _token);
                        Console.WriteLine($"Connection closed by Client. Reson: {result.CloseStatusDescription}");
                    }
                    else if (webSocket.State == WebSocketState.Aborted)
                    {
                        webSocket.Abort();
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Server Empfangen: {msg}");
                    JsonRpcStateAsync async = new JsonRpcStateAsync(RpcResultHandler, webSocket);
                    async.JsonRpc = msg;
                    JsonRpcProcessor.Process(Handler.DefaultSessionId(), async);
                }
                else
                {
                    await webSocket.SendAsync(bufferSegment, WebSocketMessageType.Binary, result.EndOfMessage, _token);
                }
            }
            webSocket.Dispose();
            Console.WriteLine("Connection closed");
        }

        private static async void RpcResultHandler(IAsyncResult result)
        {
            WebSocket client = (WebSocket)result.AsyncState;
            if (client.State == WebSocketState.Open)
            {
                Console.WriteLine($"Gesendet: {((JsonRpcStateAsync)result).Result}");
                byte[] msg = Encoding.UTF8.GetBytes(((JsonRpcStateAsync)result).Result);
                await client.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Text, true, _token);
            }
        }
    }
}
