using InTheHand.Bluetooth;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            
            DataContext = this;
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

        private async void OnConnectClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Requesting Bluetooth Device...");

            // Device ID = 7C9EBD4DD2E2 (ArnoIs50)

            _device = await Bluetooth.RequestDeviceAsync(new RequestDeviceOptions { AcceptAllDevices = true });
            if (_device != null)
            {              
                _gatt = _device.Gatt;
                Debug.WriteLine("Connecting to GATT Server...");
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


                Debug.WriteLine("Getting Service...");
                var _service = await _gatt.GetPrimaryServiceAsync(BluetoothUuid.FromShortId(ServiceId));
                if (_service != null)
                {
                    Debug.WriteLine("Getting Characteristic...");
                    _characteristic = await _service.GetCharacteristicAsync(BluetoothUuid.FromShortId(ChanracteristicId));

                    Debug.WriteLine("Writing initial value...");
                    WriteToDeviceAsync("LampState0");

                    CommandButtonsEnabled = true;
                }
            }

            Debug.WriteLine("Finished");
        }

        private void OnDisconnectClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Disconnect from Bluetooth device...");

            _gatt?.Disconnect();
            _device?.Dispose();
            _characteristic = null;

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
                Debug.WriteLine("Characteristic not set");
                return;
            }

            Debug.WriteLine("Writing value...");
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