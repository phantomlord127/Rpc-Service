using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;

namespace TB_RpcService
{
    public class WebServer
    {
        static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        static private string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private static HttpListener _listener;
        private static List<Socket> _connections = new List<Socket>();
        private static byte[] _buffer = new byte[4096];
        private static CancellationTokenSource _ctSource = new CancellationTokenSource();
        private static CancellationToken _token = _ctSource.Token;
        static object[] services = new object[] {
           new ExampleCalculatorService()
        };

        public void Start()
        {
            // Start up the HttpListener on the passes Uri.  
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:8080/httpSocket/");
            _listener.Start();
            Console.WriteLine("Listening...");
            _listener.BeginGetContext(ContextCallback, _listener);
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

        private static void AcceptCallback(IAsyncResult result)
        {
            byte[] buffer = new byte[1024];
            try
            {
                Socket client = null;
                string headerResponse = "";
                if (serverSocket != null && serverSocket.IsBound)
                {
                    client = serverSocket.EndAccept(result);
                    var i = client.Receive(buffer);
                    headerResponse = Encoding.UTF8.GetString(buffer, 0, i);
                    // write received data to the console
                    Console.WriteLine(headerResponse);

                }
                if (client != null)
                {
                    /* Handshaking and managing ClientSocket */

                    string key = headerResponse.Replace("ey:", "`")
                              .Split('`')[1]                     // dGhlIHNhbXBsZSBub25jZQ== \r\n .......
                              .Replace("\r", "").Split('\n')[0]  // dGhlIHNhbXBsZSBub25jZQ==
                              .Trim();

                    // key should now equal dGhlIHNhbXBsZSBub25jZQ==
                    string test1 = AcceptKey(ref key);

                    string newLine = "\r\n";

                    string response = "HTTP/1.1 101 Switching Protocols" + newLine
                         + "Upgrade: websocket" + newLine
                         + "Connection: Upgrade" + newLine
                         + "Sec-WebSocket-Accept: " + test1 + newLine + newLine
                         //+ "Sec-WebSocket-Protocol: chat, superchat" + newLine
                         //+ "Sec-WebSocket-Version: 13" + newLine
                         ;

                    // which one should I use? none of them fires the onopen method
                    if (string.IsNullOrEmpty(test1))
                    {
                        client.Close();
                    }
                    else
                    {
                        client.Send(Encoding.UTF8.GetBytes(response));
                        //client.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
                    }
                }
            }
            catch (SocketException exception)
            {
                throw exception;
            }
            finally
            {
                if (serverSocket != null && serverSocket.IsBound)
                {
                    serverSocket.BeginAccept(null, 0, AcceptCallback, null);
                }
            }
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

        public static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private static string AcceptKey(ref string key)
        {
            string accceptKey = string.Empty;
            try
            {
                string longKey = key + guid;
                byte[] hashBytes = ComputeHash(longKey);
                accceptKey = Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Handschake nicht erfolgreich", ex);
            }
            return accceptKey;

        }

        static SHA1 sha1 = SHA1.Create();
        private static byte[] ComputeHash(string str)
        {
            return sha1.ComputeHash(Encoding.ASCII.GetBytes(str));
        }
    }
}
