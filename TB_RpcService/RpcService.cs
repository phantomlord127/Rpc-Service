using System;
using System.Collections.Generic;
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
    }
}
