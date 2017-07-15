using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;
using LiteRpc.Server;
using LightNode.Server;

namespace TB_RpcService
{
    class RpcServer :IDisposable
    {
        static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        static private string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private static List<Socket> _connections = new List<Socket>();
        private static byte[] _buffer = new byte[4096];
        static object[] services = new object[] {
           new ExampleCalculatorService()
        };

        public RpcServer()
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 8080));
            serverSocket.Listen(128);
            serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        public void Dispose()
        {
            foreach (Socket connection in _connections)
            {
                connection.Close(2);
                connection.Dispose();
            }
            serverSocket.Close(0);
            serverSocket.Dispose();
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

                    var key = headerResponse.Replace("ey:", "`")
                              .Split('`')[1]                     // dGhlIHNhbXBsZSBub25jZQ== \r\n .......
                              .Replace("\r", "").Split('\n')[0]  // dGhlIHNhbXBsZSBub25jZQ==
                              .Trim();

                    // key should now equal dGhlIHNhbXBsZSBub25jZQ==
                    var test1 = AcceptKey(ref key);

                    var newLine = "\r\n";

                    var response = "HTTP/1.1 101 Switching Protocols" + newLine
                         + "Upgrade: websocket" + newLine
                         + "Connection: Upgrade" + newLine
                         + "Sec-WebSocket-Accept: " + test1 + newLine + newLine
                         //+ "Sec-WebSocket-Protocol: chat, superchat" + newLine
                         //+ "Sec-WebSocket-Version: 13" + newLine
                         ;

                    // which one should I use? none of them fires the onopen method
                    client.Send(Encoding.UTF8.GetBytes(response));

                    serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
                    client.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
                    //var i = client.Receive(buffer); // wait for client to send a message

                    //// once the message is received decode it in different formats
                    //Console.WriteLine(Convert.ToBase64String(buffer).Substring(0, i));
                    //Console.WriteLine((Encoding.UTF8.GetString(buffer)).Substring(0, i));
                    //Console.WriteLine("\n\nPress enter to send data to client");
                    //Console.Read();

                    //var subA = SubArray<byte>(buffer, 0, i);
                    //client.Send(subA);
                    //Thread.Sleep(10000);//wait for message to be send


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

        private static void ReceiveCallback(IAsyncResult result)
        {
            Socket client = (Socket)result.AsyncState;
            if (client.Connected)
            {
                int received = client.EndReceive(result);
                if (received > 0)
                {
                    byte[] data = new byte[received]; //the data is in the byte[] format, not string!
                    Buffer.BlockCopy(_buffer, 0, data, 0, data.Length);
                    //Console.WriteLine(Encoding.UTF8.GetString(data));
                    foreach (EncodingInfo encoding in Encoding.GetEncodings())
                    {
                        Console.WriteLine($"{encoding.Name} ergibt: {encoding.GetEncoding().GetString(data)}");
                    }
                    client.Send(data);
                    client.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
                }
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
            string longKey = key + guid;
            byte[] hashBytes = ComputeHash(longKey);
            return Convert.ToBase64String(hashBytes);
        }

        static SHA1 sha1 = SHA1CryptoServiceProvider.Create();
        private static byte[] ComputeHash(string str)
        {
            return sha1.ComputeHash(Encoding.ASCII.GetBytes(str));
        }
    }

}

public class ExampleCalculatorService : JsonRpcService
{
    [JsonRpcMethod]
    private double add(double l, double r)
    {
        return l + r;
    }
}
