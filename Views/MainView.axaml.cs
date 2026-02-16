using Android.Widget;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaApplication9;
using Android.Bluetooth.LE;
using Android.OS;
using Android.Bluetooth;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Java.Util;



namespace AvaloniaApplication9.Views;

public partial class MainView : UserControl
{
   
    public MainView()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var context = Android.App.Application.Context;


        tekst.Text = "dziala";

        

        bleAdvertiser.StartAdvertising(context);
    }   
}
