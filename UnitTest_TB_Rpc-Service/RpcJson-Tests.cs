using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TB_RpcService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnitTest_TB_RpcService
{
    [TestClass]
    public class UnitTest1
    {
        static WebServer _ws;

        [TestMethod]
        public void TestSignleAdd()
        {
            Task.Factory.StartNew(() => ExcecuteClient("{\"method\":\"add\",\"params\":{\"token\":\"2\",\"values\":[5,6]},\"id\":3}")
            .ContinueWith((t) => AssertResult(t)));
        }
        
        [TestMethod]
        public void TestBatchAdd()
        {
            Task.Factory.StartNew(() => ExcecuteClient("[{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[5,6]},\"id\":3},{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[3,4]},\"id\":4}]")
            .ContinueWith((t) => AssertResult(t)));
        }

        [TestMethod]
        public void TestManyConnections()
        {
            Task<string>[] tasks = new Task<string>[1000];
            for (int i = 0; i < 1000; i++)
            {
                tasks[i] = ExcecuteClient("[{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[5,6]},\"id\":3},{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[3,4]},\"id\":4}]");
                tasks[i].ContinueWith((t) => AssertResult(t));
            };
            Console.WriteLine("Wait all");
            Task.WaitAll(tasks);
            foreach (Task<string> task in tasks)
            {
                task.Dispose();
            }
        }

        [TestMethod]
        public void TestWUApi()
        {
            Task.Factory.StartNew(() => ExcecuteClient("{\"method\":\"updateComputer\",\"params\":{\"token\":\"test\"},\"id\":1}")
            .ContinueWith((t) => AssertResult(t)));
        }

        private static void AssertResult(Task<string> result)
        {
            Console.WriteLine($"AssertResult for: {Thread.CurrentThread.Name} and Taks: {result.Id} at {DateTime.Now.ToLongTimeString()}");
            var actualResultJArray = JsonConvert.DeserializeObject(result.Result);
            if (actualResultJArray.GetType() == typeof(JArray))
            {
                foreach (JObject obj in (JArray)actualResultJArray)
                {
                    AsserResultObject(obj);
                }
            }
            else
            {
                AsserResultObject((JObject)actualResultJArray);
            }
        }

        private static void AsserResultObject(JObject obj)
        {
            JToken actualResult = obj.GetValue("result");
            JToken id = obj.GetValue("id");
            if (double.Parse(id.ToString()) == 1)
            {
                Assert.AreEqual(string.Empty, actualResult.ToObject<string>());
            }
            else if (double.Parse(id.ToString()) == 3)
            {
                Assert.AreEqual(11, actualResult.ToObject<double>());
            }
            else
            {
                Assert.AreEqual(7, actualResult.ToObject<double>());
            }

        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _ws = new WebServer();
            _ws.Start();
            Thread.Sleep(1000);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _ws.Stop();
        }

        private async Task<string> ExcecuteClient(string msg)
        {
            string returnMessage = string.Empty;
            ClientWebSocket client = new ClientWebSocket();
            CancellationTokenSource ctSource = new CancellationTokenSource();
            try
            {
                byte[] byteBuffer = new byte[256];
                ArraySegment<byte> buffer = new ArraySegment<byte>(byteBuffer);
                await client.ConnectAsync(new Uri("ws://localhost:8080/httpSocket"), ctSource.Token);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)),
                    WebSocketMessageType.Text, true, ctSource.Token);
                WebSocketReceiveResult result = await client.ReceiveAsync(buffer, ctSource.Token);
                returnMessage = Encoding.UTF8.GetString(byteBuffer, 0, result.Count);
                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test", ctSource.Token);
                await client.ReceiveAsync(buffer, ctSource.Token);
                ctSource.Cancel();
            }
            catch (Exception ex)
            {
                returnMessage = ex.ToString();
            }
            finally
            {
                if(!ctSource.IsCancellationRequested)
                {
                    ctSource.Cancel();
                }
                ctSource.Dispose();
                client.Dispose();
            }
            return returnMessage;
        }
    }
}
