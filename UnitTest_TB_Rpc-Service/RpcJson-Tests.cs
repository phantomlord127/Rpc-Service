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
            Task<string> result = ExcecuteClient("{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[5,6]},\"id\":3}");
            result.Wait();
            JObject actualResultJObject = JsonConvert.DeserializeObject<JObject>(result.Result);
            JToken actualResult = actualResultJObject.GetValue("result");
            Assert.AreEqual(11, actualResult.ToObject<double>());
        }

        [TestMethod]
        public void TestBatchAdd()
        {
            Task<string> result = ExcecuteClient("[{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[5,6]},\"id\":3},{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[3,4]},\"id\":4}]");
            result.Wait();
            JArray actualResultJArray = JsonConvert.DeserializeObject<JArray>(result.Result);
            foreach (JObject obj in actualResultJArray)
            {
                JToken actualResult = obj.GetValue("result");
                JToken id = obj.GetValue("id");
                if (double.Parse(id.ToString()) == 3)
                {
                    Assert.AreEqual(11, actualResult.ToObject<double>());
                }
                else
                {
                    Assert.AreEqual(7, actualResult.ToObject<double>());
                }
            }
        }

        [TestMethod]
        public void TestManyConnections()
        {
            Task<string>[] tasks = new Task<string>[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = ExcecuteClient("[{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[5,6]},\"id\":3},{\"method\":\"add\",\"params\":{\"token\":\"test\",\"values\":[3,4]},\"id\":4}]");
            };
            Task.WaitAll(tasks, 3000);
            foreach (Task<string> task in tasks)
            {
                JArray actualResultJArray = JsonConvert.DeserializeObject<JArray>(task.Result);
                foreach (JObject obj in actualResultJArray)
                {
                    JToken actualResult = obj.GetValue("result");
                    JToken id = obj.GetValue("id");
                    if (double.Parse(id.ToString()) == 3)
                    {
                        Assert.AreEqual(11, actualResult.ToObject<double>());
                    }
                    else
                    {
                        Assert.AreEqual(7, actualResult.ToObject<double>());
                    }
                }
            }
        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _ws = new WebServer();
            _ws.Start();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _ws.Stop();
        }

        private async Task<string> ExcecuteClient(string msg)
        {
            string returnMessage = string.Empty;
            try
            {
                CancellationTokenSource ctSource = new CancellationTokenSource();
                ClientWebSocket client = new ClientWebSocket();
                byte[] byteBuffer = new byte[1024];
                ArraySegment<byte> buffer = new ArraySegment<byte>(byteBuffer);
                await client.ConnectAsync(new Uri("ws://localhost:8080/service"), ctSource.Token);
                Task<WebSocketReceiveResult> result = client.ReceiveAsync(buffer, ctSource.Token);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)),
                    WebSocketMessageType.Text, true, ctSource.Token);
                result.Wait(ctSource.Token);
                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test", ctSource.Token);
                client.Dispose();
                returnMessage = Encoding.UTF8.GetString(buffer.ToArray());
            }
            catch (Exception ex)
            {
                returnMessage = ex.ToString();
            }
            return returnMessage;
        }
    }
}
