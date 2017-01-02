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
// See https://msdn.microsoft.com/en-us/library/windows/apps/xaml/jj150599 (XAML socket connections)


namespace GarageDoor
{
    /// <summary>
    /// Main page that displays door status. Also maintains a <see cref="DispatcherTimer"/> 
    /// for sending alert e-mails.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly GarageDoorSensor sensor;

        private readonly DispatcherTimer dispatcherTimer = new DispatcherTimer();
        private DateTimeOffset startTime, lastTime, stopTime;
        private int timesTicked = 0;
        private bool sendClosedAlert = false;

        const int ALERT_DELAY_HOURS = 0;
        const int ALERT_DELAY_MINUTES = 5;
        const int ALERT_DELAY_SECONDS = 0;
        const int MAX_TICKS = 1;

        private readonly SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private readonly SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);

        /// <summary>
        /// Initializes GPIO pins, <see cref="DispatcherTimer"/>, and updates door status.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();
            sensor = new GarageDoorSensor(this);
            DispatcherTimerSetup();
            ledEllipse.Fill = sensor.GetDoorState() == DOOR_STATE.OPEN ? greenBrush : redBrush;
            GpioStatus.Text = $"GPIO pins initialized correctly.\nDoor is currently {sensor.GetDoorState()}.";

            if (sensor.GetDoorState() == DOOR_STATE.OPEN)
            {
                startTime = DateTimeOffset.Now;
                lastTime = startTime;
                dispatcherTimer.Start();
            }
        }

        /// <summary>
        /// Call this method to update form, start/stop the <see cref="DispatcherTimer"/>,
        /// and send alert e-mails.
        /// </summary>
        /// <param name="e"></param>
        public void UpdateStatus(GpioPinValueChangedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Edge == GpioPinEdge.FallingEdge)
                    DisplayClosedStatus();
                else
                    DisplayOpenStatus();
            });
        }

        private void DisplayClosedStatus()
        {
            ledEllipse.Fill = redBrush;
            GpioStatus.Text = $"Door is CLOSED\nDoor was closed at {DateTime.Now}.";
            if (dispatcherTimer.IsEnabled)
            {
                dispatcherTimer.Stop();
                stopTime = DateTimeOffset.Now;
                if (sendClosedAlert) SendAlertEmail();
                sendClosedAlert = false;
            }
        }

        private void DisplayOpenStatus()
        {
            ledEllipse.Fill = greenBrush;
            GpioStatus.Text = $"Door is OPEN\nDoor was opened at {DateTime.Now}.";
            startTime = DateTimeOffset.Now;
            lastTime = startTime;
            dispatcherTimer.Start();
        }
 
        private void DispatcherTimerSetup()
        {
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(ALERT_DELAY_HOURS, ALERT_DELAY_MINUTES, ALERT_DELAY_SECONDS);
        }

        private void dispatcherTimer_Tick(object sender, object e)
        {
            DateTimeOffset timeNow = DateTimeOffset.Now;
            TimeSpan timeSinceLastTick = timeNow - lastTime;
            lastTime = timeNow;
            timesTicked++;

            if (timesTicked >= MAX_TICKS)
            {
                stopTime = timeNow;
                dispatcherTimer.Stop();
                timeSinceLastTick = stopTime - startTime;
                SendAlertEmail();
                sendClosedAlert = true;
            }
        }

        private void SendAlertEmail()
        {
            using (Mailer m = new Mailer())
            {
                m.Connect();
                m.Send(GpioStatus.Text);
            }
        }
    }
}
