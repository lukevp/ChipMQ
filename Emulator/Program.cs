using System;
using System.Collections.Generic;
using System.IO;
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

            ICPU cpu = new CPU(60);

            // TODO: present user with a way to load files.
            //var fileData = File.ReadAllBytes(@"C:\Projects\Development\CandL\ChipMQ\Games\Chip-8 Demos\Maze (alt) [David Winter, 199x].ch8");
            var fileData = File.ReadAllBytes(@"C:\Projects\Development\CandL\ChipMQ\TestPrograms\fonttest.ch8");

            cpu.Load(fileData);


            using (var context = new ZContext())
            using (var publisher = new ZSocket(context, ZSocketType.PUB))
            {
                string address = "tcp://*:5556";
                Console.WriteLine("Binding publisher on {0}", address);
                publisher.Bind(address);

                cpu.Reset();
                var counter = 0;
                while (true)
                {
                    int steps = cpu.Update();
                    if (steps > 0)
                    {
                        // Send current display to all subscribers
                        var update = cpu.DisplayArray;
                        using (var updateFrame = new ZFrame(update))
                        {
                            //Console.WriteLine(update);
                            Console.WriteLine(counter++);
                            publisher.Send(updateFrame);
                        }
                        var update2 = cpu.DebugArray;

                        using (var updateFrame = new ZFrame(update2))
                        {
                            //Console.WriteLine(update2);
                            publisher.Send(updateFrame);
                        }
                    }
                    else
                    {
                        //Thread.Sleep(100);
                        //Console.WriteLine("no update to send..");
                    }
                }
            }
        }
        
    }
}
