using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace SerialPortDebugTool
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly char[,] EscapeChars =
        {
            {'0', '\0'},
            {'a', '\a'},
            {'b', '\b'},
            {'t', '\t'},
            {'n', '\n'},
            {'v', '\v'},
            {'f', '\f'},
            {'r', '\r'},
            {'\\', '\\'}
        };

        private static readonly Dictionary<char, char> Hex2TextData =
            new Dictionary<char, char>();

        private static readonly Dictionary<char, char> Text2HexData =
            new Dictionary<char, char>();

        private readonly SerialPort port = new SerialPort();

        private readonly Timer timer = new Timer(2000);
        private DataType rxDataType = DataType.Text;
        private DataType txDataType = DataType.Text;

        static MainWindow()
        {
            for (var i = 0; i < EscapeChars.GetLength(0); ++i)
            {
                Text2HexData.Add(EscapeChars[i, 0], EscapeChars[i, 1]);
                Hex2TextData.Add(EscapeChars[i, 1], EscapeChars[i, 0]);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadComboBoxData();
            port.Encoding = Encoding.UTF8;
            port.DataReceived += SerialDataReceived;
            SerialPortSwitch.Click += (s, e) => TrySwitchSerialPort();
            SendDataButton.Click += (s, e) => SendSerialData(TransmitText.Text);
            RxDataText.Checked += (s, e) => rxDataType = DataType.Text;
            RxDataHex.Checked += (s, e) => rxDataType = DataType.Hex;
            RxDataEscape.Checked += (s, e) => rxDataType = DataType.Escape;
            TxDataText.Checked += (s, e) => txDataType = DataType.Text;
            TxDataHex.Checked += (s, e) => txDataType = DataType.Hex;
            TxDataEscape.Checked += (s, e) => txDataType = DataType.Escape;
            ClearButton.MouseDown += (s, e) => e.Handled = true;
            ClearButton.MouseUp += (s, e) => ReceiveText.Clear();
            MainLRSplitter.MouseEnter += (s, e) => MainLRLine.Opacity = 1;
            MainLRSplitter.MouseLeave += (s, e) => MainLRLine.Opacity = 0;
            TxRxTDSplitter.MouseEnter += (s, e) => TxRxTDLine.Opacity = 1;
            TxRxTDSplitter.MouseLeave += (s, e) => TxRxTDLine.Opacity = 0;
            var watcher = new ManagementEventWatcher();
            watcher.Query =
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 or EventType = 3");
            watcher.EventArrived += (sender, args) => { Dispatcher.Invoke(LoadComs); };
            watcher.Start();
            timer.AutoReset = false;
            timer.Enabled = false;
            timer.Elapsed += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var a = new DoubleAnimation(0.5, 0, new Duration(TimeSpan.FromSeconds(0.5)))
                    {
                        AutoReverse = false
                    };
                    ToastGrid.BeginAnimation(OpacityProperty, a);
                });
            };
        }

        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string text;
            switch (rxDataType)
            {
                case DataType.Text:
                    text = port.ReadExisting();
                    break;
                case DataType.Hex:
                    var n = port.BytesToRead;
                    var data = new byte[n];
                    port.Read(data, 0, n);
                    var s = new StringBuilder();
                    foreach (var b in data)
                        s.Append("0123456789ABCDEF"[(b >> 4) & 0x0F])
                            .Append("0123456789ABCDEF"[b & 0x0F]).Append(' ');
                    text = s.ToString();
                    break;
                case DataType.Escape:
                    var t = port.ReadExisting();
                    var r = new StringBuilder();
                    foreach (var c in t)
                        if (c < 0x20)
                        {
                            char ch;
                            if (Hex2TextData.TryGetValue(c, out ch))
                                r.Append('\\').Append(ch);
                            else
                                r.Append("\\x")
                                    .Append("0123456789ABCDEF"[(c >> 4) & 0x0F])
                                    .Append("0123456789ABCDEF"[c & 0x0F]);
                        }
                        else
                        {
                            r.Append(c);
                        }

                    text = r.ToString();
                    break;
                default:
                    port.ReadExisting();
                    return;
            }

            ReceiveText.Dispatcher.Invoke(() =>
            {
                ReceiveText.AppendText(text);
                ReceiveText.ScrollToEnd();
            });
        }

        private void SendSerialData(string rawData)
        {
            if (!port.IsOpen)
            {
                Toast("串口未打开");
                return;
            }
            try
            {
                switch (txDataType)
                {
                    case DataType.Text:
                        port.Write(rawData);
                        break;
                    case DataType.Hex:
                        var text = rawData.ToUpper();
                        var re = new List<byte>(text.Length / 3);
                        var li = Regex.Split(text, "[^0-9A-F]+");
                        var sour = "0123456789ABCDEF";
                        foreach (var l in li)
                            if (!string.IsNullOrEmpty(l))
                            {
                                var len = l.Length & 0xFFFFFFFE;
                                for (var i = 0; i < len; i += 2)
                                    re.Add((byte) ((sour.IndexOf(l[i]) << 4) | sour.IndexOf(l[i + 1])));
                                if ((l.Length & 1) == 1)
                                    re.Add((byte) sour.IndexOf(l[l.Length - 1]));
                            }

                        var data = re.ToArray();
                        var s = new StringBuilder();
                        foreach (var b in data)
                            s.Append("0123456789ABCDEF"[(b >> 4) & 0x0F])
                                .Append("0123456789ABCDEF"[b & 0x0F]).Append(' ');
                        TransmitText.Text = s.ToString();
                        port.Write(data, 0, data.Length);
                        break;
                    case DataType.Escape:
                        var r = new StringBuilder();
                        var isE = false;
                        foreach (var c in rawData)
                            if (isE)
                            {
                                char ch;
                                if (Text2HexData.TryGetValue(c, out ch))
                                    r.Append(ch);
                                else
                                    r.Append('\\').Append(c);
                                isE = false;
                            }
                            else if (c == '\\')
                                isE = true;
                            else
                                r.Append(c);
                        if (isE) r.Append('\\');
                        port.Write(r.ToString());
                        break;
                }
            }
            catch (Exception e)
            {
                Toast($"发送失败\n{e.Message}");
            }
        }

        private void TrySwitchSerialPort()
        {
            if (port.IsOpen)
            {
                port.Close();
                SerialPortSwitch.Content = "打开串口";
            }
            else
            {
                try
                {
                    if (port.IsOpen)
                        port.Close();
                    port.PortName = (string) PortNameComboBox.SelectedValue;
                    var baudRateText = BaudRateComboBox.SelectedValue;
                    if (baudRateText == null)
                    {
                        var baudRate = 9600;
                        if (int.TryParse(BaudRateComboBox.Text, out baudRate))
                            port.BaudRate = baudRate;
                        else
                            throw new InvalidOperationException();
                    }
                    else
                    {
                        port.BaudRate = (int) baudRateText;
                    }

                    port.Parity = (Parity) ParityComboBox.SelectedValue;
                    port.DataBits = (int) DataBitsComboBox.SelectedValue;
                    port.StopBits = (StopBits) StopBitsComboBox.SelectedValue;
                    port.Open();
                    SerialPortSwitch.Content = "关闭串口";
                }
                catch (Exception e)
                {
                    SerialPortSwitch.Content = "打开串口";
                    Toast($"打开串口失败\n{e.Message}");
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Toast(string msg)
        {
            ToastText.Content = msg;
            var isRun = timer.Enabled;
            timer.Stop();
            if (!isRun)
            {
                var a = new DoubleAnimation(0, 0.5, new Duration(TimeSpan.FromSeconds(0.5)))
                {
                    AutoReverse = false
                };
                ToastGrid.BeginAnimation(OpacityProperty, a);
            }

            timer.Start();
        }

        private void LoadComboBoxData()
        {
            LoadComs();
            BaudRateComboBox.ItemsSource = new List<BaudRateData>
            {
                new BaudRateData {Value = 300},
                new BaudRateData {Value = 600},
                new BaudRateData {Value = 1200},
                new BaudRateData {Value = 1800},
                new BaudRateData {Value = 2400},
                new BaudRateData {Value = 3600},
                new BaudRateData {Value = 4800},
                new BaudRateData {Value = 7200},
                new BaudRateData {Value = 9600},
                new BaudRateData {Value = 14400},
                new BaudRateData {Value = 19200},
                new BaudRateData {Value = 28800},
                new BaudRateData {Value = 38400},
                new BaudRateData {Value = 57600},
                new BaudRateData {Value = 115200},
                new BaudRateData {Value = 921600},
                new BaudRateData {Value = 1000000},
                new BaudRateData {Value = 2000000}
            };
            BaudRateComboBox.DisplayMemberPath = "Value";
            BaudRateComboBox.SelectedValuePath = "Value";
            BaudRateComboBox.SelectedIndex = 8;
            ParityComboBox.ItemsSource = new List<ParityData>
            {
                new ParityData {Name = "无", Value = Parity.None},
                new ParityData {Name = "奇校验", Value = Parity.Odd},
                new ParityData {Name = "偶校验", Value = Parity.Even},
                new ParityData {Name = "1", Value = Parity.Mark},
                new ParityData {Name = "0", Value = Parity.Space}
            };
            ParityComboBox.DisplayMemberPath = "Name";
            ParityComboBox.SelectedValuePath = "Value";
            ParityComboBox.SelectedIndex = 0;
            DataBitsComboBox.ItemsSource = new List<DataBitsData>
            {
                new DataBitsData {Value = 5},
                new DataBitsData {Value = 6},
                new DataBitsData {Value = 7},
                new DataBitsData {Value = 8}
            };
            DataBitsComboBox.DisplayMemberPath = "Value";
            DataBitsComboBox.SelectedValuePath = "Value";
            DataBitsComboBox.SelectedIndex = 3;
            StopBitsComboBox.ItemsSource = new List<StopBitsData>
            {
                new StopBitsData {Name = "1位", Value = StopBits.One},
                new StopBitsData {Name = "1.5位", Value = StopBits.OnePointFive},
                new StopBitsData {Name = "2位", Value = StopBits.Two}
            };
            StopBitsComboBox.DisplayMemberPath = "Name";
            StopBitsComboBox.SelectedValuePath = "Value";
            StopBitsComboBox.SelectedIndex = 0;
        }

        private void LoadComs()
        {
            var selectedItem = PortNameComboBox.SelectedItem as string;
            var portNameDataList = new List<PortNameData>();
            var portNames = SerialPort.GetPortNames();
            var num = -1;
            for (var index = 0; index < portNames.Length; ++index)
            {
                portNameDataList.Add(new PortNameData
                {
                    Name = portNames[index]
                });
                if (portNames[index] == selectedItem)
                    num = index;
            }

            if (port.IsOpen && num == -1 && !string.IsNullOrEmpty(selectedItem))
            {
                portNameDataList.Add(new PortNameData
                {
                    Name = $"{selectedItem} (已断开)"
                });
                num = portNameDataList.Count - 1;
            }

            PortNameComboBox.ItemsSource = portNameDataList;
            PortNameComboBox.DisplayMemberPath = "Name";
            PortNameComboBox.SelectedValuePath = "Name";
            PortNameComboBox.SelectedIndex = num == -1 ? 0 : num;
        }
    }

    public class BaudRateData
    {
        public int Value { get; set; }
    }

    public class DataBitsData
    {
        public int Value { get; set; }
    }

    public class ParityData
    {
        public string Name { get; set; }
        public Parity Value { get; set; }
    }

    public class PortNameData
    {
        public string Name { get; set; }
    }

    public class StopBitsData
    {
        public string Name { get; set; }
        public StopBits Value { get; set; }
    }

    internal enum DataType
    {
        Text = 1,
        Hex = 2,
        Escape = 3
    }
}