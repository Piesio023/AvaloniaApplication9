using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Util;
using AvaloniaApplication2.Services;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Android;

public class AndroidBleService : IBleService
{
    private BleServer _server;
    public string Status { get; private set; } = "Gotowy";

    public void BleServer_SendNotification(string message)
    {
        if (_server == null)
        {
            var context = global::Android.App.Application.Context;
            _server = new BleServer(context);
        }
        _server.SendNotification(message);
    }

    public void StartServer()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            _server = new BleServer(context);
            _server.Start();
            Status = "Rozglaszanie...";
        }
        catch (Exception ex)
        {
            Status = $"Blad: {ex.Message}";
            Log.Error("BLE_SERVICE", $"Wyjatek startu: {ex}");
        }
    }
    public void StopServer()
    {
        if (_server != null)
        {
            _server.Stop();
            _server = null; // WAŻNE: Wyczyść instancję, aby StartServer stworzył czystą
            Status = "Zatrzymano";
        }
    }
}

public class BleServer
{
    private readonly Context _context;
    private readonly BluetoothManager _manager;
    private readonly BluetoothAdapter _adapter;
    private BluetoothLeAdvertiser _advertiser;
    private BluetoothGattServer _gattServer;
    private BluetoothDevice _connectedDevice;
    public static readonly UUID SERVICE_UUID = UUID.FromString("12345678-1234-5678-1234-56789abcdef0");
    public static readonly UUID CHAR_UUID = UUID.FromString("12345678-1234-5678-1234-56789abcdef1");
    public static readonly UUID CCCD_UUID = UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");
    private BluetoothGattCharacteristic _characteristic;
    private bool _isRunning = false;
    private AdvertiseCallbackImpl _advertiseCallback = new AdvertiseCallbackImpl();

    // Właściwość do sprawdzania, czy serwer nadal powinien działać
    public bool IsRunning => _isRunning;
    public BleServer(Context context)
    {
        _context = context;
        _manager = (BluetoothManager)context.GetSystemService(Context.BluetoothService);
        _adapter = _manager?.Adapter;
    }

    public void Start()
    {
        if (_adapter == null || !_adapter.IsEnabled) return;

        _characteristic = new BluetoothGattCharacteristic(CHAR_UUID, GattProperty.Read | GattProperty.Notify, GattPermission.Read);
        var descriptor = new BluetoothGattDescriptor(CCCD_UUID, GattDescriptorPermission.Write | GattDescriptorPermission.Read);
        _characteristic.AddDescriptor(descriptor);

        var service = new BluetoothGattService(SERVICE_UUID, GattServiceType.Primary);
        service.AddCharacteristic(_characteristic);

        _gattServer = _manager.OpenGattServer(_context, new ServerCallback(this));
        _gattServer?.AddService(service);

        _isRunning = true;
        StartAdvertising();
    }

    public void StartAdvertising() // Zmiana na publiczną, aby Callback miał dostęp
    {
        if (_connectedDevice != null || !_isRunning) return; // Nie startuj, jeśli zamykamy serwer!

        _advertiser = _adapter.BluetoothLeAdvertiser;
        var settings = new AdvertiseSettings.Builder()
            .SetAdvertiseMode(AdvertiseMode.LowLatency)
            .SetConnectable(true)
            .SetTimeout(0)
            .SetTxPowerLevel(AdvertiseTx.PowerHigh)
            .Build();

        var data = new AdvertiseData.Builder()
            .SetIncludeDeviceName(true)
            .AddServiceUuid(new ParcelUuid(SERVICE_UUID))
            .Build();

        // 2. Użyj tej samej instancji callbacku!
        _advertiser.StartAdvertising(settings, data, _advertiseCallback);
        Log.Info("BLE", "Rozpoczęto rozgłaszanie");
    }

    private void StopAdvertising()
    {
        // 3. Użyj tej samej instancji do zatrzymania!
        _advertiser?.StopAdvertising(_advertiseCallback);
        Log.Info("BLE", "Zatrzymano rozgłaszanie");
    }

    public void Stop()
    {
        _isRunning = false; // Najpierw powiedzmy systemowi, że się zamykamy
        StopAdvertising();

        if (_connectedDevice != null)
        {
            var deviceToDisconnect = _connectedDevice;
            _connectedDevice = null; // Czyścimy przed anulowaniem
            _gattServer?.CancelConnection(deviceToDisconnect);
        }

        _gattServer?.ClearServices(); // Dobra praktyka przed zamknięciem
        _gattServer?.Close();
        _gattServer = null;
    }

    public void SendNotification(string message)
    {
        if (_characteristic == null || _gattServer == null || _connectedDevice == null) return;

        byte[] value = Encoding.UTF8.GetBytes(message);
        _characteristic.SetValue(value);

        bool success = _gattServer.NotifyCharacteristicChanged(_connectedDevice, _characteristic, false);
        Log.Debug("BLE", $"Wysłano do {_connectedDevice.Address}: {(success ? "Sukces" : "Błąd")}");
    }

    class ServerCallback : BluetoothGattServerCallback
    {
        private readonly BleServer _parent;
        public ServerCallback(BleServer parent) => _parent = parent;

        public override void OnConnectionStateChange(BluetoothDevice device, ProfileState status, ProfileState newState)
        {
            if (newState == ProfileState.Connected)
            {
                // Logika "tylko jeden": jeśli już ktoś jest, rozłącz nowego (lub starego)
                if (_parent._connectedDevice != null && _parent._connectedDevice.Address != device.Address)
                {
                    _parent._gattServer.CancelConnection(device);
                    return;
                }

                _parent._connectedDevice = device;
                _parent.StopAdvertising(); // Przestań być widocznym dla innych
                Log.Info("BLE", $"Połączono z: {device.Address}");
            }
            else if (newState == ProfileState.Disconnected)
            {
                if (_parent._connectedDevice?.Address == device.Address)
                {
                    _parent._connectedDevice = null;
                    Log.Info("BLE", "Urządzenie rozłączone. Wznawiam rozgłaszanie...");
                    _parent.StartAdvertising(); // Znowu widoczny dla PC
                }
            }
        }

        public override void OnDescriptorWriteRequest(BluetoothDevice device, int requestId, BluetoothGattDescriptor descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            if (responseNeeded)
                _parent._gattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
        }


    public override void OnServiceAdded(GattStatus status, BluetoothGattService service)
        {
            Log.Info("BLE", $"Serwis dodany: {service.Uuid}, Status: {status}");
        }
    }

    class AdvertiseCallbackImpl : AdvertiseCallback
    {
        public override void OnStartSuccess(AdvertiseSettings settingsInEffect) =>
            Log.Info("BLE", "Rozglaszanie BLE (Advertising) wystartowalo.");

        public override void OnStartFailure(AdvertiseFailure errorCode) =>
            Log.Error("BLE", $"Blad rozglaszania: {errorCode}");
    }
}