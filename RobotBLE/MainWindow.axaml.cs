using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaApplication1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
// Biblioteki Windows BLE
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace AvaloniaApplication1
{
    public partial class MainWindow : Window
    {
        private readonly BleService _bleService = new BleService();

        private List<string> Komendy = new List<string> { "pin led on", "pin led off" };
        private int KomendyIndex = -1;

        private int Test_delay = 100;
        private int Test_For = 120;

        public event Action<string>? OnLog1;
        public event Action<string>? OnLog2;
        public event Action<string>? OnLogSystem;

        private BleConnectionModule _moduleUser1;
        private BleConnectionModule _moduleUser2;

        private readonly Guid SERVICE_UUID = Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
        private readonly Guid CHAR_UUID = Guid.Parse("0000ffe1-0000-1000-8000-00805f9b34fb");

        // DEKLARACJA KLIENTÓW DLA OBU TELEFONÓW
        private PhoneBleClient _phoneClient1;
        private PhoneBleClient _phoneClient2;

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += Window_KeyDown;

            _bleService.OnLog += (wiadomosc) => log(CommandOutput, wiadomosc);
            this.Opened += async (s, e) => await _bleService.ConnectAsync();
            this.Closed += (s, e) => _bleService.Disconnect();

            // Inicjalizacja klientów z ró¿nymi UUID
            _phoneClient1 = new PhoneBleClient(LogBoxP1, _bleService, "12345678-1234-5678-1234-56789abcdef0", "12345678-1234-5678-1234-56789abcdef1");
            _phoneClient2 = new PhoneBleClient(LogBoxP2, _bleService, "0000ffe0-0000-1000-8000-00805f9b34fb", "0000ffe1-0000-1000-8000-00805f9b34fb");

            // Reakcja na zmianê statusu po³¹czenia w UI
            _phoneClient1.OnConnectionStatusChanged += (isConnected) =>
                Dispatcher.UIThread.Post(() => StatusP1.IsChecked = isConnected);

            _phoneClient2.OnConnectionStatusChanged += (isConnected) =>
                Dispatcher.UIThread.Post(() => StatusP2.IsChecked = isConnected);
        }

        // Obs³uga przycisków REFRESH (Skanuj/Po³¹cz)
        private void ReConect_User1_Click(object? sender, RoutedEventArgs e) => _phoneClient1.StartScanning();
        private void ReConect_User2_Click(object? sender, RoutedEventArgs e) => _phoneClient2.StartScanning();

        // Obs³uga przycisków DISCONNECT
        private async void Disconnect_User1_Click(object? sender, RoutedEventArgs e) => await _phoneClient1.DisconnectAsync();
        private async void Disconnect_User2_Click(object? sender, RoutedEventArgs e) => await _phoneClient2.DisconnectAsync();

        public void phoneClient(string argument, int deviceNumber)
        {
            var client = deviceNumber == 1 ? _phoneClient1 : _phoneClient2;

            if (argument == "start")
            {
                client.StartScanning();
            }
            else if (argument == "dis")
            {
                client.DisconnectAsync();
            }
        }

        private async void DoCommand()
        {
            string rawCommand = CommandBox.Text?.Trim() ?? "";
            string command = rawCommand.ToLower();

            if (string.IsNullOrEmpty(command)) return;

            if (Komendy.Contains(command)) Komendy.Remove(command);
            Komendy.Insert(0, command);
            KomendyIndex = -1;

            if (!command.StartsWith("/"))
            {
                if (_bleService.IsConnected)
                {
                    await _bleService.SendMessageAsync(command);
                    log(CommandOutput, $"> {command}");
                }
                else
                {
                    log(CommandOutput, "No BLE connection");
                }
            }
            else
            {
                if (command == "/close") this.Close();
                else if (command == "/re") { _bleService.Disconnect(); await _bleService.ConnectAsync(); }
                else if (command.StartsWith("/ble_d"))
                {
                    string[] parts = rawCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && parts[1].StartsWith("--"))
                    {
                        _bleService.TargetName = parts[1].Substring(2);
                        log(CommandOutput, $"TARGET_NAME = {_bleService.TargetName}");
                    }
                    else
                    {
                        log(CommandOutput, $"{_bleService.TargetName}");
                    }
                }
                else if (command == "/save")
                {
                    Save();
                }
                else if (command == "/test")
                {
                    Start_Opuznienie_Test();
                }
                else if (command == "/p1 start") phoneClient("start", 1);
                else if (command == "/p1 dis") phoneClient("dis", 1);
                else if (command == "/p2 start") phoneClient("start", 2);
                else if (command == "/p2 dis") phoneClient("dis", 2);
                else log(CommandOutput, "Komenda systemowa nie istnieje");
            }

            CommandBox.Text = "";
        }

        async Task Start_Opuznienie_Test()
        {
            await _bleService.SendMessageAsync($"T{DateTime.Now:HH:mm:ss:fff}");
            for (int i = 0; i < Test_For; i++)
            {
                await _bleService.SendMessageAsync("pin led on");
                await Task.Delay(Test_delay);
                await _bleService.SendMessageAsync("pin led off");
                await Task.Delay(Test_delay);
            }
            await _bleService.SendMessageAsync($"E{DateTime.Now:HH:mm:ss:fff}");
        }

        private void ExitButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DrawerToggler != null) DrawerToggler.IsChecked = false;
        }
        private void DisconnectP1(object? sender, RoutedEventArgs e)
        {
            phoneClient("dis", 1);
        }
        private void DisconnectP2(object? sender, RoutedEventArgs e)
        {
            phoneClient("dis", 2);
        }

        private void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DrawerToggler != null) DrawerToggler.IsChecked = true;
            DoCommand();
        }

        private async void ReConect_Click(object? sender, RoutedEventArgs e)
        {
            _bleService.Disconnect();
            await _bleService.ConnectAsync();
        }

        private void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var lines = CommandOutput.Text?.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None) ?? Array.Empty<string>();

            var logObject = new
            {
                tekst = lines,
                TARGET_NAME = _bleService.TargetName,
                Komendy,
                Test_delay,
                Test_For
            };

            string json = JsonSerializer.Serialize(logObject, options);
            string path = "C:\\Users\\Muszesze\\Downloads\\Projekt_na_marzec\\AvaloniaApplication1\\log\\log.json";

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (CommandBox == null || !CommandBox.IsFocused) return;

            if (e.Key == Key.Up)
            {
                e.Handled = true;
                if (KomendyIndex < Komendy.Count - 1)
                {
                    KomendyIndex++;
                    CommandBox.Text = Komendy[KomendyIndex];
                }
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;
                if (KomendyIndex > 0)
                {
                    KomendyIndex--;
                    CommandBox.Text = Komendy[KomendyIndex];
                }
                else
                {
                    KomendyIndex = -1;
                    CommandBox.Text = "";
                }
            }
            else if (e.Key == Key.Enter)
            {
                DoCommand();
            }
        }

        private void log(TextBox targetTextBox, string text)
        {
            if (targetTextBox == null) return;

            targetTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}";

            var scrollViewer = targetTextBox.GetVisualDescendants()
                                           .OfType<ScrollViewer>()
                                           .FirstOrDefault();

            Dispatcher.UIThread.Post(() =>
            {
                targetTextBox.CaretIndex = targetTextBox.Text?.Length ?? 0;
                scrollViewer?.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
    }


    public class PhoneBleClient
    {
        // Usuniêto przypisanie na sta³e - teraz to pola instancji
        private readonly Guid SERVICE_UUID;
        private readonly Guid CHAR_UUID;
        public event Action<bool>? OnConnectionStatusChanged;
        private readonly BleService _bleService;
        private BluetoothLEAdvertisementWatcher? _watcher;
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _characteristic;
        private readonly TextBox _logBox;

        private static HashSet<ulong> ConnectedMacAddresses = new HashSet<ulong>();

        // NOWY KONSTRUKTOR: przyjmuje UUID jako stringi lub Guidy
        public PhoneBleClient(TextBox logBox, BleService bleService, string serviceUuid, string charUuid)
        {
            _logBox = logBox;
            _bleService = bleService;

            // Parsujemy przekazane stringi na obiekty Guid
            SERVICE_UUID = Guid.Parse(serviceUuid);
            CHAR_UUID = Guid.Parse(charUuid);
        }

        public void StartScanning()
        {
            Log("Searching for phone...");
            _watcher = new BluetoothLEAdvertisementWatcher();

            _watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(SERVICE_UUID);

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Start();
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args.Advertisement.ServiceUuids.Contains(SERVICE_UUID))
            {
                // Ignorujemy telefon, jeœli pierwsza instancja ju¿ siê z nim po³¹czy³a
                if (ConnectedMacAddresses.Contains(args.BluetoothAddress)) return;

                _watcher?.Stop();

                // Rezerwujemy ten adres MAC dla tej instancji
                ConnectedMacAddresses.Add(args.BluetoothAddress);

                string macAddress = string.Join(":", BitConverter.GetBytes(args.BluetoothAddress).Reverse().Select(b => b.ToString("X2")));
                Log($"Device found. MAC Address: {macAddress}");

                await ConnectToDevice(args.BluetoothAddress);
            }
        }

        private async Task ConnectToDevice(ulong bluetoothAddress)
        {
            try
            {

                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_device == null)
                {
                    Log("Device not found. Make sure the phone is advertising!");
                    ConnectedMacAddresses.Remove(bluetoothAddress); // Usuwamy blokadê w razie b³êdu
                    return;
                }

                Log($"Connected to {_device.BluetoothAddress}!");

                var servicesResult = await _device.GetGattServicesForUuidAsync(SERVICE_UUID);
                if (servicesResult.Status != GattCommunicationStatus.Success || !servicesResult.Services.Any())
                {
                    Log("Suitable service not found on the device.");
                    return;
                }

                var service = servicesResult.Services[0];

                var charsResult = await service.GetCharacteristicsForUuidAsync(CHAR_UUID);
                if (charsResult.Status != GattCommunicationStatus.Success || !charsResult.Characteristics.Any())
                {
                    Log("Suitable characteristic not found on the device.");
                    return;
                }

                _characteristic = charsResult.Characteristics[0];

                var status = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                {
                    _characteristic.ValueChanged += Characteristic_ValueChanged;
                    Log("......");
                }
                else
                {
                    Log("Failed to subscribe to notifications.");
                }
                if (status == GattCommunicationStatus.Success)
                {
                    _characteristic.ValueChanged += Characteristic_ValueChanged;
                    Log("Connected and Subscribed!");
                    OnConnectionStatusChanged?.Invoke(true); // POWIADOM UI
                }
            }
            catch (Exception ex)
            {
                Log($"Error during connection: {ex.Message}");
                ConnectedMacAddresses.Remove(bluetoothAddress); // Usuwamy blokadê w razie b³êdu
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
            byte[] data = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(data);
            string text = Encoding.UTF8.GetString(data).Trim(); // Trim() usunie zbêdne znaki nowej linii

            // 1. Logujemy w UI laptopa co przysz³o
            Log($"Received from phone: {text}");

            // 2. KLUCZOWE: Wysy³amy czysty tekst bezpoœrednio do mikrokontrolera
            if (_bleService != null && _bleService.IsConnected)
            {
                await _bleService.SendMessageAsync(text);
            }
        }

        private void Log(string message)
        {
            if (_logBox == null) return;

            Dispatcher.UIThread.Post(() =>
            {

                _logBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                var scrollViewer = _logBox.GetVisualDescendants()
                                          .OfType<ScrollViewer>()
                                          .FirstOrDefault();

                _logBox.CaretIndex = _logBox.Text?.Length ?? 0;
                scrollViewer?.ScrollToEnd();
            }, DispatcherPriority.Background);
        }

        public async Task DisconnectAsync()
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Received -= OnAdvertisementReceived;
                _watcher = null;
                Log("Scanning stopped.");
            }

            if (_characteristic != null)
            {
                _characteristic.ValueChanged -= Characteristic_ValueChanged;
                try
                {
                    await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch (Exception ex)
                {
                    Log($"Error disabling notifications (can be ignored): {ex.Message}");
                }
                _characteristic = null;
            }

            if (_device != null)
            {
                ConnectedMacAddresses.Remove(_device.BluetoothAddress);
                _device.Dispose();
                _device = null;
                Log("Disconnected.");
            }
            else
            {
                Log("No active connection to disconnect.");
            }
            OnConnectionStatusChanged?.Invoke(false);


            GC.Collect();
        }
    }
}