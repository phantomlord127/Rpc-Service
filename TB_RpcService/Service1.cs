using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;

namespace TB_RpcService
{
    public partial class Service1 : ServiceBase
    {
        WebServer _ws;

        public Service1()
        {
            InitializeComponent();
            eventLog1.Source = "Application";
        }

        public void TestStartStop(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Hit Enter to stop the Service.");
            Console.ReadLine();
            OnStop();
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("in Onstart", EventLogEntryType.Information);
            _ws = new WebServer();
            _ws.Start();
            //// Start up the HttpListener on the passes Uri.  
            //HttpListener listener = new HttpListener();
            //listener.Prefixes.Add("http://0.0.0.0:8080/httpSocket/");
            //listener.Start();
            //Console.WriteLine("Listening...");
            //// Accept the HttpListenerContext 
            //HttpListenerContext listenerContext = await listener.GetContextAsync();

            //// Check if this is for a websocket request 
            //if (listenerContext.Request.IsWebSocketRequest)
            //{
            //    ProcessRequest(listenerContext);
            //}
            //else
            //{
            //    // Since we are expecting WebSocket requests and this is not - send HTTP 400 
            //    listenerContext.Response.StatusCode = 400;
            //    listenerContext.Response.Close();
            //}
        }

        //private async void ProcessRequest(HttpListenerContext listenerContext)
        //{
        //    WebSocketContext webSocketContext = null;

        //    try
        //    {
        //        // Accept the WebSocket request 
        //        webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
        //    }
        //    catch (Exception ex)
        //    {
        //        // If any error occurs then send HTTP Status 500 
        //        listenerContext.Response.StatusCode = 500;
        //        listenerContext.Response.Close();
        //        Console.WriteLine("Exception : {0}", ex.Message);
        //        return;
        //    }

        //    // Accept the WebSocket connect.  
        //    WebSocket webSocket = webSocketContext.WebSocket;
        //    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("hello Wörld")), WebSocketMessageType.Text, true, null);
        //}

        protected override void OnStop()
        {
            eventLog1.WriteEntry("in Onstop", EventLogEntryType.Information);
            _ws.Stop();
        }
     }
}
