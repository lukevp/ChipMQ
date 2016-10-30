using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Shapes;
using System.Windows.Threading;
using ZeroMQ;

namespace Debugger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ZContext context;
        ZSocket subscriber;


        public class CPUModel : INotifyPropertyChanged
        {
            private string _ram;
            private string _pc;
            private ObservableCollection<string> _registers;
            public string RAM
            {
                get
                {
                    return _ram;
                }
                set
                {
                    _ram = value;
                    NotifyPropertyChanged("RAM");
                }
            }
            public string PC
            {
                get
                {
                    return _pc;
                }
                set
                {
                    _pc = value;
                    NotifyPropertyChanged("PC");
                }
            }
            public ObservableCollection<string> Registers
            {
                get
                {
                    return _registers;
                }
                set
                {
                    _registers = value;
                    NotifyPropertyChanged("REGISTERS");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void NotifyPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public CPUModel CPU
        {
            get; set;
        }

        public MainWindow()
        {
            InitializeComponent();
            CPU = new CPUModel();
            DataContext = this;
            context = new ZContext();
            subscriber = new ZSocket(context, ZSocketType.SUB);
            subscriber.ReceiveHighWatermark = 1;
            // TODO: let the display update whenever it receives a message instead of doing timeouts.
            subscriber.ReceiveTimeout = new TimeSpan(0, 0, 0, 0, 5);
            subscriber.Connect("tcp://127.0.0.1:5556");

            /* foreach (IPAddress address in WUProxy_GetPublicIPs())
				{
					var epgmAddress = string.Format("epgm://{0};239.192.1.1:8100", address);
					Console.WriteLine("I: Connecting to {0}...", epgmAddress);
					subscriber.Connect(epgmAddress);
				}
			} */

            // Subscribe to Debugger Events
            Console.WriteLine("Subscribing to debugger events...");
            subscriber.Subscribe(new byte[] { 0xFF });

            // TODO: let the display update whenever it receives a message instead of doing timeouts.
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
                        byte[] debugArray;
                        using (var memoryStream = new MemoryStream((int)frame.Length))
                        {
                            frame.CopyTo(memoryStream);
                            // HACK: make this more optimized.
                            debugArray = memoryStream.ToArray().Skip(1).ToArray();
                        }

                        //DateLabel.Content = "1234";
                        UpdateDebugger(debugArray);
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        private string ToHex(IEnumerable<byte> input)
        {
            return ToHex(input.ToArray());
        }

        private string ToHex(byte[] input)
        {
            return BitConverter.ToString(input).Replace("-", "");
        }
        private string ToHexWithSpaces(IEnumerable<byte> input)
        {
            return ToHexWithSpaces(input.ToArray());
        }
        private string ToHexWithSpaces(byte[] input)
        {
            return BitConverter.ToString(input).Replace("-", " ");
        }

        void UpdateDebugger(byte[] debugArray)
        {
            // TODO: de-serialize byte array to object
            CPU.PC = ToHex(debugArray.Take(2));
            ObservableCollection<string> regs = new ObservableCollection<string>();
            for (int j = 0; j < 16; j++)
            {
                regs.Add(ToHex(debugArray.Skip(2 + j).Take(1)));
            }
            CPU.Registers = regs;
            //debugArray.Skip(2)
            CPU.RAM = ToHexWithSpaces(debugArray.Skip(56));

        }
    }
}
