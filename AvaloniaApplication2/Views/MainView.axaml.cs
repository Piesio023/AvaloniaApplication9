using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaApplication2.Services;
using static System.Net.Mime.MediaTypeNames;


namespace AvaloniaApplication2.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void ButtonHello_Click(object? sender, RoutedEventArgs e)
    {
        // Odwo³anie bezpoœrednie
        var bleService = AvaloniaApplication2.App.BleService;

        if (bleService != null)
        {
            bleService.StartServer();
            MainText.Text = bleService.Status;
        }
        else
        {
            MainText.Text = "Us³uga BLE nie zosta³a za³adowana.";
        }
    }
}