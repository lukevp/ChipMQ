using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace Emulator
{
    class Program
    {
        static void Main(string[] args)
        {

            CPU cpu = new CPU(60);



            using (var context = new ZContext())
            using (var publisher = new ZSocket(context, ZSocketType.PUB))
            {
                string address = "tcp://*:5556";
                Console.WriteLine("Binding publisher on {0}", address);
                publisher.Bind(address);
                
                while (true)
                {
                    int steps = cpu.Update();
                    if (steps > 0)
                    {
                        // Send current display to all subscribers
                        var update = "U " + cpu.GetCompressedDisplay();
                        using (var updateFrame = new ZFrame(update))
                        {
                            Console.WriteLine(update);
                            publisher.Send(updateFrame);
                        }
                    }
                    else
                    {
                        Console.WriteLine("no update to send..");
                    }
                }
            }
        }
        
    }
}
