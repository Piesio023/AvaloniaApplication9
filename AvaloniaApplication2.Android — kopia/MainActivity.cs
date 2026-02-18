using Android;
using Avalonia;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia.Android;
using AvaloniaApplication2.Services; // Odwołanie do interfejsu w projekcie głównym
//using AvaloniaApplication2.Android.Implementation; // Lub inna nazwa namespace, którą masz w AndroidBleService.cs
using ReactiveUI.Avalonia;
using System.Collections.Generic;

namespace AvaloniaApplication2.Android;

[Activity(
    Label = "AvaloniaApplication2.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Wywołujemy sprawdzanie uprawnień przy starcie
        CheckBluetoothPermissions();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Przypisanie konkretnej implementacji Androidowej do statycznego pola
        AvaloniaApplication2.App.BleService = new AndroidBleService();

        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
    }

    private void CheckBluetoothPermissions()
    {
        // Lista uprawnień potrzebnych dla BLE (Android 12+)
        string[] permissions =
        {
            Manifest.Permission.BluetoothScan,
            Manifest.Permission.BluetoothConnect,
            Manifest.Permission.BluetoothAdvertise,
            Manifest.Permission.AccessFineLocation
        };

        List<string> permissionsToRequest = new List<string>();
        foreach (var permission in permissions)
        {
            if (ContextCompat.CheckSelfPermission(this, permission) != Permission.Granted)
            {
                permissionsToRequest.Add(permission);
            }
        }

        if (permissionsToRequest.Count > 0)
        {
            ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), 0);
        }
    }
}