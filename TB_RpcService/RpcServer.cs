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

namespace TB_RpcService
{
    class RpcServer : IDisposable
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
                connection.Shutdown(SocketShutdown.Send);
                connection.BeginDisconnect(false, DisconnectCallback, connection);
                connection.Close(2);
            }
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
            //ToDo: Feststellen, ob hier noch neue Verbindungen aufgebaut werden können.
            Socket client = (Socket)result.AsyncState;
            if (IsSocketConnected(client))
            {
                int received = client.EndReceive(result);
                if (received > 0)
                {
                    byte[] data = new byte[received]; //the data is in the byte[] format, not string!
                    Buffer.BlockCopy(_buffer, 0, data, 0, data.Length);
                    if (data.Length > 12) //ToDo: Dreckiger Hack. Besser Paket korrekt auslesen!
                    {
                        string msg = GetDecodedData(data, data.Length);
                        Console.WriteLine($"Empfangen: {msg}");
                        client.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
                        JsonRpcStateAsync async = new JsonRpcStateAsync(RpcResultHandler, client);
                        async.JsonRpc = msg;
                        JsonRpcProcessor.Process(Handler.DefaultSessionId(), async);
                    }
                    else
                    {
                        Console.WriteLine("Verbindung geschlossen");
                        client.Close();
                        _connections.Remove(client);
                    }
                }
            }
            else
            {
                _connections.Remove(client);
            }
        }

        private static void DisconnectCallback(IAsyncResult result)
        {
            Socket connection = (Socket)result.AsyncState;
            connection.EndDisconnect(result);
            Console.WriteLine("Verbindung wurde vom Client getrennt");
        }

        private static void RpcResultHandler(IAsyncResult result)
        {
            Socket client = (Socket)result.AsyncState;
            if (client.Connected)
            {
                Console.WriteLine($"Gesendet: {((JsonRpcStateAsync)result).Result}");
                client.Send(GetEncodeMessage(((JsonRpcStateAsync)result).Result));
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

        private static byte[] GetEncodeMessage(string message)
        {
            Byte[] response;
            Byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            Byte[] frame = new Byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = (Byte)129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (Byte)127;
                frame[2] = (Byte)((length >> 56) & 255);
                frame[3] = (Byte)((length >> 48) & 255);
                frame[4] = (Byte)((length >> 40) & 255);
                frame[5] = (Byte)((length >> 32) & 255);
                frame[6] = (Byte)((length >> 24) & 255);
                frame[7] = (Byte)((length >> 16) & 255);
                frame[8] = (Byte)((length >> 8) & 255);
                frame[9] = (Byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new Byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        public static string GetDecodedData(byte[] buffer, int length)
        {
            byte b = buffer[1];
            int dataLength = 0;
            int totalLength = 0;
            int keyIndex = 0;

            if (b - 128 <= 125)
            {
                dataLength = b - 128;
                keyIndex = 2;
                totalLength = dataLength + 6;
            }

            if (b - 128 == 126)
            {
                dataLength = BitConverter.ToInt16(new byte[] { buffer[3], buffer[2] }, 0);
                keyIndex = 4;
                totalLength = dataLength + 8;
            }

            if (b - 128 == 127)
            {
                dataLength = (int)BitConverter.ToInt64(new byte[] { buffer[9], buffer[8], buffer[7], buffer[6], buffer[5], buffer[4], buffer[3], buffer[2] }, 0);
                keyIndex = 10;
                totalLength = dataLength + 14;
            }

            if (totalLength > length)
                throw new Exception("The buffer length is small than the data length");

            byte[] key = new byte[] { buffer[keyIndex], buffer[keyIndex + 1], buffer[keyIndex + 2], buffer[keyIndex + 3] };

            int dataIndex = keyIndex + 4;
            int count = 0;
            for (int i = dataIndex; i < totalLength; i++)
            {
                buffer[i] = (byte)(buffer[i] ^ key[count % 4]);
                count++;
            }

            return Encoding.UTF8.GetString(buffer, dataIndex, dataLength);
        }

        static bool IsSocketConnected(Socket s)
        {
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);
        }

    }

    public class ExampleCalculatorService : JsonRpcService
    {
        [JsonRpcMethod]
        private double add(string token, double[] values)
        {
            if (token == "test")
            {
                return values[0] + values[1];
            }
            else
            {
                return 0;
            }
        }
    }
}
