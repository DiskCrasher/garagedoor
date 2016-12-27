using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using static GarageDoor.GarageDoorSensor;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GarageDoor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly GarageDoorSensor sensor;

        private readonly SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private readonly SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);

        public MainPage()
        {
            this.InitializeComponent();
            sensor = new GarageDoorSensor(this);
            ledEllipse.Fill = sensor.GetDoorState() == DOOR_STATE.OPEN ? greenBrush : redBrush;
            GpioStatus.Text = $"GPIO pins initialized correctly.\nDoor is currently {sensor.GetDoorState()}.";
        }

        public void UpdateStatus(GpioPinValueChangedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Edge == GpioPinEdge.FallingEdge)
                {
                    ledEllipse.Fill = redBrush;
                    GpioStatus.Text = $"CLOSED\nDoor was closed at {DateTime.Now}.";
                }
                else
                {
                    ledEllipse.Fill = greenBrush;
                    GpioStatus.Text = $"OPEN\nDoor was opened at {DateTime.Now}.";
                }
            });

        }
    }
}
