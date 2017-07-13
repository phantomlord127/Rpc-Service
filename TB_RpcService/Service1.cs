using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace TB_RpcService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
            if (!EventLog.SourceExists("TB_Rpc-Service"))
            {
                EventLog.CreateEventSource("TB_Rpc-Service", "MyLog");
            }
            eventLog1.Source = "TB_Rpc-Service";
            eventLog1.Log = "MyLog";
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("in Onstart");
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("in Onstop");
        }
    }
}
