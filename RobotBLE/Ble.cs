using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace AvaloniaApplication1
{
    public class BleService
    {
        private BluetoothLEDevice? _bleDevice;
        private GattCharacteristic? _rxCharacteristic;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private static readonly Guid SERVICE_UUID = Guid.Parse("0000181c-0000-1000-8000-00805f9b34fb");
        private static readonly Guid UART_RX_CHAR_UUID = Guid.Parse("00002a6f-0000-1000-8000-00805f9b34fb");

        public string TargetName { get; set; } = "PicoBLE";
        public bool IsConnected => _rxCharacteristic != null;

        public event Action<string>? OnLog;

        public async Task ConnectAsync()
        {
            try
            {
                OnLog?.Invoke($"Searching for device: {TargetName}...");

                var watcher = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Active
                };

                var tcs = new TaskCompletionSource<ulong>();

                watcher.Received += (w, e) =>
                {
                    if (e.Advertisement.LocalName == TargetName)
                    {
                        tcs.TrySetResult(e.BluetoothAddress);
                    }
                };

                watcher.Start();

                ulong address = await tcs.Task;
                watcher.Stop();

                OnLog?.Invoke($"Connecting to {address:X}...");

                _bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

                var servicesResult = await _bleDevice.GetGattServicesForUuidAsync(SERVICE_UUID);
                var service = servicesResult.Services.FirstOrDefault();

                if (service == null)
                {
                    OnLog?.Invoke("B£¥D: Nie znaleziono BLE.");
                    return;
                }

                var charsResult = await service.GetCharacteristicsForUuidAsync(UART_RX_CHAR_UUID);
                _rxCharacteristic = charsResult.Characteristics.FirstOrDefault();

                if (_rxCharacteristic == null)
                {
                    OnLog?.Invoke("ERROR: No RX.");
                }
                else
                {
                    OnLog?.Invoke("PO£¥CZONO Z PICO 2 W");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"ERROR BLE: {ex.Message}");
            }
        }

        public  async Task SendMessageAsync(string text)
        {
            if (_rxCharacteristic == null)
            {
                OnLog?.Invoke("ERROR: Próba wys³ania wiadomoœci bez po³¹czenia.");
                return;
            }

            await _sendLock.WaitAsync();
            try
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(text));
                await _rxCharacteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Disconnect()
        {
            _bleDevice?.Dispose();
            _bleDevice = null;
            _rxCharacteristic = null;
            OnLog?.Invoke("Roz³¹czono.");
        }
    }
}
