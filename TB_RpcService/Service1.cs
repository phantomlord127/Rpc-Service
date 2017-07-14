using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;

namespace TB_RpcService
{
    public partial class Service1 : ServiceBase
    {
        RpcServer _rpcServer;

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
            //ConsoleServer test = new ConsoleServer();

            //AsyncCallback rpcResultHandler = new AsyncCallback(_ => Console.WriteLine(((JsonRpcStateAsync)_).Result));
            //JsonRpcStateAsync async = new JsonRpcStateAsync(rpcResultHandler, null);
            //async.JsonRpc = "{'method':'add','params':[1,2],'id':1}";
            //JsonRpcProcessor.Process(Handler.DefaultSessionId(), async);

            _rpcServer = new RpcServer();
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("in Onstop", EventLogEntryType.Information);
            _rpcServer.Dispose();

        }
    }
}
