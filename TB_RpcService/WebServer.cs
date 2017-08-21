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
using log4net;
using Newtonsoft.Json.Linq;

namespace TB_RpcService
{
    public class WebServer
    {
        private static HttpListener _listener;
        private static CancellationTokenSource _ctSource = new CancellationTokenSource();
        private static CancellationToken _token = _ctSource.Token;
        private static ILog _log;
        private static WebSocket _webSocket;
        static object[] services = new object[] {
           new ExampleCalculatorService()
        };
        public static WebSocket Client {
            get {if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    return _webSocket;
                return null;
            }
            private set {
                if ((_webSocket == null || _webSocket.State != WebSocketState.Open) && value != null && value.State == WebSocketState.Open)
                    _webSocket = value;
            }
        }

        public void Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            _log = LogManager.GetLogger(typeof(WebServer));
            //Config Austin Harris Rpc
            Config.SetErrorHandler(OnJsonRpcException);
            Config.SetPreProcessHandler(new PreProcessHandler(PreProcess));
            // Start up the HttpListener on the passes Uri.  
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:8080/httpSocket/");
            _listener.Start();
            _listener.BeginGetContext(ContextCallback, _listener);
            _log.InfoFormat("Listener is startet for the following Prefixes:{0}", string.Join(",", _listener.Prefixes));
        }

        private JsonRpcException PreProcess(JsonRequest request, object context)
        {
            JObject j = request.Params as JObject;
            if (j == null || ! j.First.HasValues || j.First.First.ToString() != "test")
            {
                _log.WarnFormat("The token of the request is invalid. Request: {0} . Context: {1}", request, context);
                throw new JsonRpcException(-32602, "Invalid Token", null);
            }
            _log.DebugFormat("Accept the following request: {0}", request);
            return null;
        }

        private JsonRpcException OnJsonRpcException(JsonRequest request, JsonRpcException ex)
        {
            _log.ErrorFormat("Exception in JsonRpc. Request: {0} . Exception: {1}", request, ex);
            ex.data = null;
            return ex;
        }

        private void ContextCallback(IAsyncResult result)
        {
            // Accept the HttpListenerContext
            HttpListener listener = (HttpListener)result.AsyncState;
            if (! listener.IsListening)
            {
                _log.Error("Listiner is not listinig");
                return;
            }
            HttpListenerContext listenerContext = listener.EndGetContext(result);
            listener.BeginGetContext(ContextCallback, listener);
            // Check if this is for a websocket request 
            if (listenerContext.Request.IsWebSocketRequest)
            {
                _log.InfoFormat("Connection Request by {0}:{1}", listenerContext.Request.RemoteEndPoint.Address, listenerContext.Request.RemoteEndPoint.Port);
                //string webSocketKey = listenerContext.Request.Headers.Get("Sec-WebSocket-Key");
                //listenerContext.Response.AddHeader("Sec-WebSocket-Accept", AcceptKey(ref webSocketKey));
                ProcessRequest(listenerContext);
            }
            else
            {
                // Since we are expecting WebSocket requests and this is not - send HTTP 400 
                listenerContext.Response.StatusCode = 400;
                listenerContext.Response.Close();
                _log.ErrorFormat("Connection Request is not a Websocket Request. {0}", listenerContext.Request);
            }
        }

        public void Stop()
        {
            _ctSource.Cancel();
            _log.Info("Listener is shutting down.");
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
                _log.DebugFormat("New webSocketConnection: {0}", webSocketContext);
            }
            catch (Exception ex)
            {
                // If any error occurs then send HTTP Status 500 
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                _log.ErrorFormat("Error AcceptWebSocket. ListenerContext: {0} . Exception: {1}", listenerContext, ex);
                return;
            }
            // Accept the WebSocket connect.
            byte[] buffer = new byte[256];
            ArraySegment<byte> bufferSegment = new ArraySegment<byte>(buffer);
            WebSocket webSocket = webSocketContext.WebSocket;
            while (webSocket.State != WebSocketState.Closed)
            {
                WebSocketReceiveResult request = await webSocket.ReceiveAsync(bufferSegment, _token);
                if (request.MessageType == WebSocketMessageType.Close)
                {
                    if (webSocket.State == WebSocketState.CloseReceived)
                    {
                        await Task.Delay(100); //Warum muss hier noch kurz gewartet werden? Ohne diese Zeile wird die Verbindung getrennt, trotz noch zu sendenden Text.
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by Client.", _token);
                        _log.DebugFormat("Connection closed by Client. Reson: {0}", request.CloseStatusDescription);
                    }
                    else if (webSocket.State == WebSocketState.Aborted)
                    {
                        webSocket.Abort();
                        _log.WarnFormat("WebSocket state = aborted. Websocket: {0} . Request: {1}", webSocket, request);
                    }
                }
                else if (request.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, request.Count);
                    _log.DebugFormat("Server received: {0}", msg);
                    JsonRpcStateAsync async = new JsonRpcStateAsync(RpcResultHandler, webSocket);
                    async.JsonRpc = msg;
                    JsonRpcProcessor.Process(Handler.DefaultSessionId(), async);
                }
                else
                {
                    await webSocket.SendAsync(bufferSegment, WebSocketMessageType.Binary, request.EndOfMessage, _token);
                    _log.DebugFormat("Keep alive message received: {0}", request);
                }
            }
            webSocket.Dispose();
            _log.InfoFormat("Connection closed with {0}:{1}", listenerContext.Request.RemoteEndPoint.Address, listenerContext.Request.RemoteEndPoint.Port);
        }

        private static async void RpcResultHandler(IAsyncResult result)
        {
            WebSocket client = (WebSocket)result.AsyncState;
            if (client.State == WebSocketState.Open)
            {
                string msgText = ((JsonRpcStateAsync)result).Result;
                if (string.Compare(msgText, "Windows Update gestartet") == 0)
                {
                    Client = client;
                }
                byte[] msg = Encoding.UTF8.GetBytes(msgText);
                await client.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Text, true, _token);
                _log.DebugFormat("Send anser to client: {0}", ((JsonRpcStateAsync)result).Result);
            }
        }
    }
}
