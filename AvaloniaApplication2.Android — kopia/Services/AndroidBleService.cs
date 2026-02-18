using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Util;
using Java.Util;
using AvaloniaApplication2.Services;

namespace AvaloniaApplication2.Android;

public class AndroidBleService : IBleService
{
    private BleServer _server;
    public string Status { get; private set; } = "Gotowy";

    public void StartServer()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            _server = new BleServer(context);
            _server.Start();
            Status = "Rozg³aszanie... Po³¹cz siê z PC!";
        }
        catch (Exception ex)
        {
            Status = $"B³¹d: {ex.Message}";
        }
    }
}

public class BleServer
{
    private Context _context;
    private BluetoothManager _manager;
    private BluetoothAdapter _adapter;
    private BluetoothLeAdvertiser _advertiser;
    private BluetoothGattServer _gattServer;

    // Lista pod³¹czonych urz¹dzeñ (np. Twój komputer)
    private List<BluetoothDevice> _connectedDevices = new List<BluetoothDevice>();

    // UUIDs - U¿yj tych samych w programie na komputerze!
    // Service UUID
    public static readonly UUID SERVICE_UUID = UUID.FromString("12345678-1234-5678-1234-56789abcdef0");
    // Characteristic UUID (to tutaj bêdziemy wysy³aæ "Hello World")
    public static readonly UUID CHAR_UUID = UUID.FromString("12345678-1234-5678-1234-56789abcdef1");

    private BluetoothGattCharacteristic _characteristic;
    private bool _isRunning = false;

    public BleServer(Context context)
    {
        _context = context;
        _manager = (BluetoothManager)context.GetSystemService(Context.BluetoothService);
        _adapter = _manager.Adapter;
    }

    public void Start()
    {
        if (_adapter == null || !_adapter.IsEnabled) return;

        // 1. Konfiguracja Charakterystyki (Read + Notify)
        _characteristic = new BluetoothGattCharacteristic(
            CHAR_UUID,
            GattProperty.Read | GattProperty.Notify,
            GattPermission.Read);

        // 2. Konfiguracja Serwisu
        var service = new BluetoothGattService(SERVICE_UUID, GattServiceType.Primary);
        service.AddCharacteristic(_characteristic);

        // 3. Uruchomienie Serwera GATT (odbieranie po³¹czeñ)
        _gattServer = _manager.OpenGattServer(_context, new ServerCallback(this));
        _gattServer.AddService(service);

        // 4. Rozpoczêcie Rozg³aszania (Advertising) - ¿eby PC nas widzia³
        _advertiser = _adapter.BluetoothLeAdvertiser;
        var settings = new AdvertiseSettings.Builder()
            .SetAdvertiseMode(AdvertiseMode.LowLatency)
            .SetConnectable(true)
            .SetTxPowerLevel(AdvertiseTx.PowerHigh)
            .Build();

        var data = new AdvertiseData.Builder()
            .SetIncludeDeviceName(true)
            .AddServiceUuid(new ParcelUuid(SERVICE_UUID))
            .Build();

        _advertiser.StartAdvertising(settings, data, new AdvertiseCallbackImpl());

        // 5. Uruchomienie pêtli wysy³aj¹cej dane
        _isRunning = true;
        StartSendingDataLoop();
    }

    private void StartSendingDataLoop()
    {
        Task.Run(async () =>
        {
            while (_isRunning)
            {
                if (_connectedDevices.Count > 0)
                {
                    SendNotification("Hello World " + DateTime.Now.ToString("HH:mm:ss"));
                }
                await Task.Delay(2000); // Co 2 sekundy
            }
        });
    }

    private void SendNotification(string message)
    {
        byte[] value = Encoding.UTF8.GetBytes(message);
        _characteristic.SetValue(value);

        foreach (var device in _connectedDevices)
        {
            // Powiadamiamy pod³¹czone urz¹dzenie, ¿e wartoœæ siê zmieni³a
            // False na koñcu oznacza, ¿e nie czekamy na potwierdzenie odbioru
            _gattServer.NotifyCharacteristicChanged(device, _characteristic, false);
        }
        Log.Info("BLE", $"Wys³ano: {message}");
    }

    // Callback obs³uguj¹cy po³¹czenia przychodz¹ce
    class ServerCallback : BluetoothGattServerCallback
    {
        private readonly BleServer _parent;

        public ServerCallback(BleServer parent)
        {
            _parent = parent;
        }

        public override void OnConnectionStateChange(BluetoothDevice? device, ProfileState status, ProfileState newState)
        {
            base.OnConnectionStateChange(device, status, newState);

            if (newState == ProfileState.Connected)
            {
                Log.Info("BLE", $"Urz¹dzenie pod³¹czone: {device.Address}");
                lock (_parent._connectedDevices)
                {
                    _parent._connectedDevices.Add(device);
                }
            }
            else if (newState == ProfileState.Disconnected)
            {
                Log.Info("BLE", $"Urz¹dzenie roz³¹czone: {device.Address}");
                lock (_parent._connectedDevices)
                {
                    _parent._connectedDevices.Remove(device);
                }
            }
        }

        // Wymagane, aby PC móg³ w³¹czyæ subskrypcjê powiadomieñ
        public override void OnDescriptorWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattDescriptor? descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            if (responseNeeded)
            {
                _parent._gattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
            }
        }

        public override void OnCharacteristicReadRequest(BluetoothDevice device, int requestId, int offset, BluetoothGattCharacteristic characteristic)
        {
            _parent._gattServer.SendResponse(device, requestId, GattStatus.Success, offset, characteristic.GetValue());
        }
    }

    class AdvertiseCallbackImpl : AdvertiseCallback
    {
        public override void OnStartSuccess(AdvertiseSettings settingsInEffect) =>
            Log.Info("BLE", "Reklama wystartowa³a poprawnie.");

        public override void OnStartFailure(AdvertiseFailure errorCode) =>
            Log.Warn("BLE", $"B³¹d reklamy: {errorCode}");
    }
}