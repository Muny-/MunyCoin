using System;
using System.Net.Sockets;
using LibMunyCoin;
using PureWebSockets;

namespace MunyCoinClient
{
    class Program
    {
        
        static void Main(string[] args)
        {
            MunyCoin mnc = new MunyCoin();

            Console.ReadLine();

            mnc.Stop();
        }
    }
}
