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
            
        }

      

        protected override void OnStop()
        {
            eventLog1.WriteEntry("in Onstop", EventLogEntryType.Information);
            _ws.Stop();
        }
     }
}
