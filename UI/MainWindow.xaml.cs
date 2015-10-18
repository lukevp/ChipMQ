using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ZeroMQ;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ZContext context;
        ZSocket subscriber;

        public MainWindow()
        {
            InitializeComponent();
            // TODO: dispose these whhen exiting?
            context = new ZContext();
            subscriber = new ZSocket(context, ZSocketType.SUB);
            subscriber.ReceiveHighWatermark = 1;
            subscriber.ReceiveTimeout = new TimeSpan(0, 0, 0, 0, 5);
            subscriber.Connect("tcp://127.0.0.1:5556");

            /* foreach (IPAddress address in WUProxy_GetPublicIPs())
				{
					var epgmAddress = string.Format("epgm://{0};239.192.1.1:8100", address);
					Console.WriteLine("I: Connecting to {0}...", epgmAddress);
					subscriber.Connect(epgmAddress);
				}
			} */

            // Subscribe to zipcode
            Console.WriteLine("Subscribing to UI events...");
            subscriber.Subscribe(new byte[] { 0x01 });

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(5);
            timer.Tick += timer_Tick;
            timer.Start();

        }

        void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                using (var message = subscriber.ReceiveMessage())
                {
                    if (message.Count > 0)
                    {
                        // if more than one message is waiting to be rendered, just render the most recent one that's there.
                        var frame = message[message.Count - 1];
                        // copy array out of stream into an array.
                        byte[] displayArray;
                        using (var memoryStream = new MemoryStream((int)frame.Length))
                        {
                            frame.CopyTo(memoryStream);
                            displayArray = memoryStream.ToArray();
                        }
                        DateLabel.Content = displayArray.ToString();
                    }
                }
            }
            catch (ZException)
            { }
        }
    }
}
