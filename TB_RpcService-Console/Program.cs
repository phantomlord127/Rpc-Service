using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TB_RpcService;

namespace TB_RpcService_Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start des Services.");
            Service1 test = new Service1();
            Console.WriteLine("Ende");
        }
    }
}
