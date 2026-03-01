using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace AvaloniaApplication1
{
    public class BleConnectionModule
    {
        private readonly string _targetName;
        private readonly Guid _serviceUuid;
        private readonly Guid _charUuid;

        private BluetoothLEDevice _bleDevice;
        private CancellationTokenSource _cts;

        public bool IsRunning { get; private set; } = false;

        // --- ZMIANA: Event zamiast Tui ---
        // To pozwoli przekazać tekst do GUI
        public event Action<string> OnLog;

        public BleConnectionModule(string targetName, Guid serviceUuid, Guid charUuid)
        {
            _targetName = targetName;
            _serviceUuid = serviceUuid;
            _charUuid = charUuid;
        }

        // Pomocnicza metoda do zgłaszania logów
        private void Log(string message)
        {
            // Formatujemy wiadomość tutaj, np. dodając nazwę urządzenia
            OnLog?.Invoke($"[{_targetName}] {message}");
        }

        public async Task StartAsync()
        {
            if (IsRunning)
            {
                Log("Moduł już pracuje.");
                return;
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await ConnectLoop(token);
                }
                catch (Exception ex)
                {
                    Log($"Krytyczny błąd wątku: {ex.Message}");
                }
                finally
                {
                    Cleanup();
                }
            });
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            Log("Szukanie urządzenia...");

            string selector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(_targetName);
            var devices = await DeviceInformation.FindAllAsync(selector);
            var deviceJob = devices.FirstOrDefault();

            if (deviceJob == null)
            {
                Log("Urządzenie nie znalezione.");
                IsRunning = false;
                return;
            }

            if (token.IsCancellationRequested) return;

            Log("Łączenie...");
            _bleDevice = await BluetoothLEDevice.FromIdAsync(deviceJob.Id);

            if (_bleDevice == null)
            {
                Log("Nie udało się połączyć.");
                return;
            }

            var service = await GetService(_bleDevice, _serviceUuid, token);
            if (service == null) return;

            var characteristic = await GetCharacteristic(service, _charUuid, token);
            if (characteristic == null) return;

            characteristic.ValueChanged += Characteristic_ValueChanged;
            var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify
            );

            if (status == GattCommunicationStatus.Success)
            {
                Log("Połączono! Odbieranie danych...");

                try
                {
                    await Task.Delay(-1, token);
                }
                catch (TaskCanceledException) { }
            }
            else
            {
                Log($"Błąd subskrypcji: {status}");
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;
            Log("Zatrzymywanie...");
            _cts?.Cancel();
            await Task.Delay(200);
        }

        private void Cleanup()
        {
            IsRunning = false;
            Log("Rozłączono.");

            if (_bleDevice != null)
            {
                _bleDevice.Dispose();
                _bleDevice = null;
            }
            _cts?.Dispose();
            _cts = null;
        }

        private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] input = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(input);

            string data;
            try { data = Encoding.UTF8.GetString(input); }
            catch { data = BitConverter.ToString(input); }

            // Przekazanie danych do GUI
            Log($"DANE: {data}");
        }

        private async Task<GattDeviceService> GetService(BluetoothLEDevice device, Guid uuid, CancellationToken token)
        {
            var result = await device.GetGattServicesForUuidAsync(uuid, BluetoothCacheMode.Cached).AsTask(token);
            if (result.Status == GattCommunicationStatus.Success && result.Services.Count > 0) return result.Services[0];

            result = await device.GetGattServicesForUuidAsync(uuid, BluetoothCacheMode.Uncached).AsTask(token);
            if (result.Status == GattCommunicationStatus.Success && result.Services.Count > 0) return result.Services[0];

            Log("Brak serwisu.");
            return null;
        }

        private async Task<GattCharacteristic> GetCharacteristic(GattDeviceService service, Guid uuid, CancellationToken token)
        {
            var result = await service.GetCharacteristicsForUuidAsync(uuid);
            if (result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0) return result.Characteristics[0];

            Log("Brak charakterystyki.");
            return null;
        }
    }
}