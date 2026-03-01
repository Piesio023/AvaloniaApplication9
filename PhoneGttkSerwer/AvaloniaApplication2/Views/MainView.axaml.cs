using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaApplication2.Services;
using System;

namespace AvaloniaApplication2.Views;

public partial class MainView : UserControl
{
    private DispatcherTimer _sendTimer;
    private int _lastSentValue = -999;

    public MainView()
    {
        InitializeComponent();

        // Inicjalizacja timera - wysy³anie co 0.2 sekundy
        _sendTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.2)
        };
        _sendTimer.Tick += SendTimer_Tick;
        _sendTimer.Start();
    }

    // Metoda wywo³ywana cyklicznie przez timer
    private void SendTimer_Tick(object? sender, EventArgs e)
    {
        if (MainAppPanel.IsVisible == false)
        {
            // Pobieramy wartoœæ suwaka i zaokr¹glamy do liczby ca³kowitej
            int currentValue = (int)Math.Round(VerticalSlider.Value);

            // Wysy³amy tylko jeœli wartoœæ siê zmieni³a
            if (currentValue != _lastSentValue)
            {
                var bleService = AvaloniaApplication2.App.BleService;
                if (bleService != null)
                {
                    if (currentValue > 0)
                    {
                        bleService.BleServer_SendNotification($"engine +{currentValue}");
                        _lastSentValue = currentValue;
                    }
                    else if (currentValue == 0)
                    {
                        bleService.BleServer_SendNotification($"engine {currentValue}");
                        _lastSentValue = currentValue;
                    }
                    else
                    {
                        bleService.BleServer_SendNotification($"engine {currentValue}");
                        _lastSentValue = currentValue;
                    }
                }
            }
        }
        else 
        {
            // Pobieramy wartoœæ suwaka i zaokr¹glamy do liczby ca³kowitej
            int currentValue2 = (int)Math.Round(VerticalSlider2.Value);

            // Wysy³amy tylko jeœli wartoœæ siê zmieni³a
            if (currentValue2 != _lastSentValue)
            {
                var bleService = AvaloniaApplication2.App.BleService;
                if (bleService != null)
                {
                        bleService.BleServer_SendNotification($"servo {currentValue2}");
                        _lastSentValue = currentValue2;
                }
            }
        }
    }

    private void Slider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (MainAppPanel.IsVisible == false)
        {
            // Magnes na zero (snapowanie wizualne)
            if (e.Property == Slider.ValueProperty && sender is Slider slider)
            {
                if (e.NewValue is double newValue)
                {
                    double snapThreshold = 5.0;
                    if (newValue != 0 && newValue > -snapThreshold && newValue < snapThreshold)
                    {
                        slider.Value = 0;
                    }
                }
            }
        }
        else
        {
            // Magnes na zero (snapowanie wizualne)
            if (e.Property == Slider.ValueProperty && sender is Slider slider)
            {
                if (e.NewValue is double newValue)
                {
                    double snapThreshold = 5050.0;
                    if (newValue != 0 && newValue > -snapThreshold && newValue < snapThreshold)
                    {
                        slider.Value = 49151;
                    }
                }
            }
        }
    }

    private void Connect_Click(object? sender, RoutedEventArgs e)
    {
        var bleService = AvaloniaApplication2.App.BleService;

        if (bleService != null)
        {
            string serviceUuid = "";
            string charUuid = "";
            string newName = "";

            if (PlayerComboBox.SelectedIndex == 0) // Player 1
            {
                serviceUuid = "12345678-1234-5678-1234-56789abcdef0";
                charUuid = "12345678-1234-5678-1234-56789abcdef1";
                newName = "User1";
                MainAppPanel.IsVisible = true;
            }
            else // Player 2
            {
                serviceUuid = "0000ffe0-0000-1000-8000-00805f9b34fb";
                charUuid = "0000ffe1-0000-1000-8000-00805f9b34fb";
                newName = "User2";
                MainAppPanel2.IsVisible = true;
            }

            bleService.StartServer(serviceUuid, charUuid, newName);
            //MainText.Text = bleService.Status;
            SetupPanel.IsVisible = false;
        }
    }

    private void ButtonStatus_Click(object? sender, RoutedEventArgs e)
    {
        var bleService = AvaloniaApplication2.App.BleService;
        //if (bleService != null) MainText.Text = bleService.Status;
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var bleService = AvaloniaApplication2.App.BleService;
        //if (bleService != null && !string.IsNullOrWhiteSpace(test.Text))
       // {
         //   bleService.BleServer_SendNotification(test.Text);
        //}
    }

    private void Button_Click_stop(object? sender, RoutedEventArgs e)
    {
        var bleService = AvaloniaApplication2.App.BleService;
        if (bleService != null)
        {
            bleService.StopServer();
            //MainText.Text = bleService.Status;

            MainAppPanel.IsVisible = false;
            MainAppPanel2.IsVisible = false;
            SetupPanel.IsVisible = true;
        }
    }
}