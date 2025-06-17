using InTheHand.Bluetooth;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace SaltLamp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // staan onder de predefined in de Android App
        private const string SERVICE_UUID = "0000ffe0-0000-1000-8000-00805f9b34fb";
        private const string CHARACTERISTIC_UUID = "0000ffe1-0000-1000-8000-00805f9b34fb";
        private const ushort ServiceId = 0xFFE0;
        private const ushort ChanracteristicId = 0xFFE1;

        private int count = 0;
        private bool _commandButtonsEnabled;
        private BluetoothDevice _device;
        private GattService _service;
        private RemoteGattServer _gatt;
        private GattCharacteristic? _characteristic;
        private bool _panelLoading;
        private string _panelMainMessage = "Connecting to device ...";
        private string _panelSubMessage = string.Empty; //

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            LoadConfiguration();
            DataContext = this;

            // Auto connect to configured device
            AutoConnect(Config?.DeviceName ?? string.Empty);
        }

        public bool CommandButtonsEnabled 
        {
            get => _commandButtonsEnabled;
            private set
            {
                if (_commandButtonsEnabled != value)
                {
                    _commandButtonsEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<BluetoothDevice> Devices { get; private set; } = new List<BluetoothDevice>();
        public Configuration? Config { get; private set; }
        public ObservableCollection<string> Messages { get; private set; } = new ObservableCollection<string>();

        /// <summary>
        /// Gets or sets a value indicating whether [panel loading].
        /// </summary>
        /// <value>
        /// <c>true</c> if [panel loading]; otherwise, <c>false</c>.
        /// </value>
        public bool PanelLoading
        {
            get
            {
                return _panelLoading;
            }
            set
            {
                _panelLoading = value;
                NotifyPropertyChanged("PanelLoading");
            }
        }

        /// <summary>
        /// Gets or sets the panel main message.
        /// </summary>
        /// <value>The panel main message.</value>
        public string PanelMainMessage
        {
            get
            {
                return _panelMainMessage;
            }
            set
            {
                _panelMainMessage = value;
                NotifyPropertyChanged("PanelMainMessage");
            }
        }

        /// <summary>
        /// Gets or sets the panel sub message.
        /// </summary>
        /// <value>The panel sub message.</value>
        public string PanelSubMessage
        {
            get
            {
                return _panelSubMessage;
            }
            set
            {
                _panelSubMessage = value;
                NotifyPropertyChanged("PanelSubMessage");
            }
        }



        private void LoadConfiguration()
        {
            string fileName = "Configuration.json";
            string jsonString = File.ReadAllText(fileName);
            Config = JsonSerializer.Deserialize<Configuration>(jsonString);
        }

        private async void AutoConnect(string deviceName)
        {
            PanelMainMessage = $"Connecting to device {deviceName}...";
            PanelLoading = true;

            if (string.IsNullOrEmpty(deviceName))
            {
                // See what devices are available
                _device = await Bluetooth.RequestDeviceAsync(new RequestDeviceOptions { AcceptAllDevices = true });
                return;
            }

            Messages.Add($"Autoconnect to Bluetooth Device {deviceName}...");

            // Device ID = 7C9EBD4DD2E2 (ArnoIs50)
            // Device ID = 38182B89FE16 (TestBLE)
            var filter = new BluetoothLEScanFilter
            {
                Name = deviceName
            };
            var options = new RequestDeviceOptions();
            options.Filters.Add(filter);
            //_device = await Bluetooth.RequestDeviceAsync(options);
            var devices = await Bluetooth.ScanForDevicesAsync(options);
            if (devices.Count > 0)
            {
                _device = devices.First();
            }

            if (_device != null)
            {
                _gatt = _device.Gatt;
                PanelSubMessage = "Connecting to GATT Server...";
                Messages.Add(PanelSubMessage);
                await _gatt.ConnectAsync();

                PanelSubMessage = "Getting Service...";
                Messages.Add(PanelSubMessage);
                _service = await _gatt.GetPrimaryServiceAsync(BluetoothUuid.FromShortId(ServiceId));
                if (_service != null)
                {
                    PanelSubMessage = "Getting Characteristic...";
                    Messages.Add(PanelSubMessage);
                    _characteristic = await _service.GetCharacteristicAsync(BluetoothUuid.FromShortId(ChanracteristicId));

                    Messages.Add("Writing initial value...");
                    WriteToDeviceAsync("LampState0");

                    CommandButtonsEnabled = true;
                }
                else
                {
                    Messages.Add("Failed to get service");
                }
            }
            else
            {
                Messages.Add("Failed to connect to device");
            }

            PanelLoading = false;
        }

        private async void OnConnectClicked(object sender, RoutedEventArgs e)
        {
            if (Config == null)
            {
                Messages.Add("No configuration...");
                return;
            }

            Messages.Add("Requesting Bluetooth Device...");

            _device = await Bluetooth.RequestDeviceAsync(new RequestDeviceOptions { AcceptAllDevices = true });
            if (_device != null)
            {
                _gatt = _device.Gatt;
                Messages.Add("Connecting to GATT Server...");
                await _gatt.ConnectAsync();

                /*var servs = await _device.Gatt.GetPrimaryServicesAsync();
                Debug.WriteLine($"Found {servs.Count} services...");
                foreach (var serv in servs)
                {
                    var rssi = await _device.Gatt.ReadRssi();
                    Debug.WriteLine($"{rssi} {serv.Uuid} Primary:{serv.IsPrimary}");

                    Debug.Indent();

                    foreach (var chars in await serv.GetCharacteristicsAsync())
                    {
                        Debug.WriteLine($"{chars.Uuid} Properties:{chars.Properties}");

                        Debug.Indent();

                        foreach (var descriptors in await chars.GetDescriptorsAsync())
                        {
                            Debug.WriteLine($"Descriptor:{descriptors.Uuid}");

                            var val2 = await descriptors.ReadValueAsync();

                            if (descriptors.Uuid == GattDescriptorUuids.ClientCharacteristicConfiguration)
                            {
                                Debug.WriteLine($"Notifying:{val2[0] > 0}");
                            }
                            else if (descriptors.Uuid == GattDescriptorUuids.CharacteristicUserDescription)
                            {
                                Debug.WriteLine($"UserDescription:{ByteArrayToString(val2)}");
                            }
                            else
                            {
                                Debug.WriteLine(ByteArrayToString(val2));
                            }

                        }

                        Debug.Unindent();
                    }

                    Debug.Unindent();
                }*/


                Messages.Add("Getting Service...");
                _service = await _gatt.GetPrimaryServiceAsync(BluetoothUuid.FromShortId(ServiceId));
                if (_service != null)
                {
                    Messages.Add("Getting Characteristic...");
                    _characteristic = await _service.GetCharacteristicAsync(BluetoothUuid.FromShortId(ChanracteristicId));

                    Messages.Add("Writing initial value...");
                    WriteToDeviceAsync("LampState0");

                    CommandButtonsEnabled = true;
                }
                else
                {
                    Messages.Add("Failed to get service");
                }
            }
        }

        private void OnDisconnectClicked(object sender, RoutedEventArgs e)
        {
            Messages.Add("Disconnect from Bluetooth device...");

            _characteristic = null;
            _gatt?.Disconnect();
            _device?.Dispose();

            CommandButtonsEnabled = false;
        }

        private void OnCommandClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OnCommandClicked");

            var button = e.Source as Button;
            var state = button?.Tag as string ?? string.Empty;

            WriteToDeviceAsync(state);
        }

        private async void WriteToDeviceAsync(string value)
        {
            if (_characteristic == null)
            {
                Messages.Add("Characteristic not set");
                return;
            }

            Messages.Add($"Writing value {value} ...");
            var bytes = Encoding.ASCII.GetBytes(value);

            await _characteristic.WriteValueWithoutResponseAsync(bytes);
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }        
    }
}