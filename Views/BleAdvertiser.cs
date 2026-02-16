using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Java.Util;


 namespace AvaloniaApplication9
{


    public class BleAdvertiser
    {
        BluetoothAdapter _adapter;
        BluetoothLeAdvertiser _advertiser;
        static readonly UUID SERVICE_UUID = UUID.FromString("12345678-1234-5678-1234-56789abcdef0");
        const string DEVICE_NAME = "AvaloniaBLE";

        public void StartAdvertising(Context context)
        {
            BluetoothManager manager = (BluetoothManager)context.GetSystemService(Context.BluetoothService);
            _adapter = manager.Adapter;
            if (!_adapter.IsEnabled || !_adapter.IsMultipleAdvertisementSupported) return;

            
            _advertiser = _adapter.BluetoothLeAdvertiser;

            var settings = new AdvertiseSettings.Builder()
                .SetAdvertiseMode(AdvertiseMode.LowLatency)
                .SetTxPowerLevel(AdvertiseTx.PowerHigh)
                .SetConnectable(true)
                .Build();

            var data = new AdvertiseData.Builder()
                .SetIncludeDeviceName(true)
                .AddServiceUuid(new ParcelUuid(SERVICE_UUID))
                .Build();

            _advertiser.StartAdvertising(settings, data, new AdvertiseCallbackImpl());
        }

        class AdvertiseCallbackImpl : AdvertiseCallback
        {
            public override void OnStartSuccess(AdvertiseSettings settingsInEffect) =>
                Log.Info("BLE", "Reklama rozpoczęta");

            public override void OnStartFailure(AdvertiseFailure errorCode) =>
                Log.Warn("BLE", $"Reklama nie powiodła się: {errorCode}");
        }

        public void StopAdvertising()
        {
            _advertiser?.StopAdvertising(new AdvertiseCallbackImpl());
        }

        BleAdvertiser bleAdvertiser = new BleAdvertiser();

    }

    

    


}
