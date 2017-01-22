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
        private readonly GarageDoorSensor m_sensor;

        private readonly Queue<string> m_history = new Queue<string>(10);
        private readonly DispatcherTimer m_dispatcherTimer = new DispatcherTimer();
        private DateTimeOffset m_startTime, m_lastTime, m_stopTime;
        private int m_timesTicked = 0;
        private bool m_sendClosedAlert = false;

        const int ALERT_DELAY_HOURS = 0;
        const int ALERT_DELAY_MINUTES = 3;
        const int ALERT_DELAY_SECONDS = 5;
        const int MAX_TICKS = 1;

        private readonly SolidColorBrush m_redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private readonly SolidColorBrush m_greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);

        /// <summary>
        /// Initializes GPIO pins, <see cref="DispatcherTimer"/>, and updates door status.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();
            m_sensor = new GarageDoorSensor(this);
            DispatcherTimerSetup();
            ledEllipse.Fill = m_sensor.GetDoorState() == DOOR_STATE.OPEN ? m_greenBrush : m_redBrush;
            GpioStatus.Text = $"GPIO pins initialized correctly.\nDoor is currently {m_sensor.GetDoorState()}.";
            m_history.Enqueue($"{DateTime.Now} - Program started with door {m_sensor.GetDoorState()}.");

            if (m_sensor.GetDoorState() == DOOR_STATE.OPEN)
            {
                m_startTime = DateTimeOffset.Now;
                m_lastTime = m_startTime;
                m_dispatcherTimer.Start();
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
                    HandleCloseEvent();
                else
                    HandleOpenEvent();
            });
        }

        private void HandleCloseEvent()
        {
            ledEllipse.Fill = m_redBrush;
            GpioStatus.Text = $"Door is CLOSED\nDoor was closed at {DateTime.Now}.";
            m_history.Enqueue($"{DateTime.Now} - Door is CLOSED.");

            if (m_dispatcherTimer.IsEnabled)
            {
                m_dispatcherTimer.Stop();
                m_stopTime = DateTimeOffset.Now;
                if (m_sendClosedAlert) SendAlertEmail();
                m_sendClosedAlert = false;
            }
        }

        private void HandleOpenEvent()
        {
            ledEllipse.Fill = m_greenBrush;
            GpioStatus.Text = $"Door is OPEN\nDoor was opened at {DateTime.Now}.";
            m_history.Enqueue($"{DateTime.Now} - Door is OPEN.");
            m_startTime = DateTimeOffset.Now;
            m_lastTime = m_startTime;
            m_dispatcherTimer.Start();
        }
 
        private void DispatcherTimerSetup()
        {
            m_dispatcherTimer.Tick += dispatcherTimer_Tick;
            m_dispatcherTimer.Interval = new TimeSpan(ALERT_DELAY_HOURS, ALERT_DELAY_MINUTES, ALERT_DELAY_SECONDS);
        }

        private void dispatcherTimer_Tick(object sender, object e)
        {
            UpdateHistoryTextBlock();
            DateTimeOffset timeNow = DateTimeOffset.Now;
            TimeSpan timeSinceLastTick = timeNow - m_lastTime;
            m_lastTime = timeNow;
            m_timesTicked++;

            if (m_timesTicked >= MAX_TICKS)
            {
                m_stopTime = timeNow;
                m_dispatcherTimer.Stop();
                timeSinceLastTick = m_stopTime - m_startTime;
                SendAlertEmail();
                m_sendClosedAlert = true;
            }
        }

        /// <summary>
        /// Displays contents of history queue in GUI.
        /// </summary>
        private void UpdateHistoryTextBlock()
        {
            textBlock.Text = "";
            foreach (string entry in m_history)
                textBlock.Text += entry + Environment.NewLine;
        }

        /// <summary>
        /// Sends out an e-mail alert via SMTP.
        /// </summary>
        private void SendAlertEmail()
        {
            using (Mailer m = new Mailer())
            {
                m.Connect();
                m.Send(GpioStatus.Text + Environment.NewLine + Environment.NewLine + textBlock.Text);
            }
        }
    }
}
