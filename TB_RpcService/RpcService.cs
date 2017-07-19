using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AustinHarris.JsonRpc;

namespace TB_RpcService
{
    public class ExampleCalculatorService : JsonRpcService
    {
        [JsonRpcMethod]
        private double add(string token, double[] values)
        {
            return values[0] + values[1];
        }

        [JsonRpcMethod]
        private bool shutDownPc(string token)
        {
            var psi = new ProcessStartInfo("shutdown", "/s /t 30");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
            return true;
        }
    }
}
