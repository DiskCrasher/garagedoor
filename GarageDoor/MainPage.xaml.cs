/*
MIT License

Copyright (c) 2017 Michael J. Lowery

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409
// See https://msdn.microsoft.com/en-us/library/windows/apps/xaml/jj150599 (XAML socket connections)

namespace GarageDoor
{
    /// <summary>
    /// Main page that displays door status. Also maintains a <see cref="DispatcherTimer"/> 
    /// for sending alert e-mails.
    /// </summary>
    public sealed partial class MainPage : Page, IDisposable
    {
        const int HISTORY_QUEUE_SIZE = 10;
        const int ALERT_DELAY_HOURS = 0;
        const int ALERT_DELAY_MINUTES = 3;
        const int ALERT_DELAY_SECONDS = 5;
        const int MAX_TICKS = 1;

        private readonly GarageDoorSensor m_sensor;
        private readonly Queue<string> m_history = new Queue<string>(HISTORY_QUEUE_SIZE);
        private readonly DispatcherTimer m_doorOpenTimer = new DispatcherTimer();
        private DateTimeOffset m_startTime, m_lastTime, m_stopTime;
        private int m_timesTicked = 0;
        private bool m_sendClosedAlert = false;

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
            UpdateHistoryTextBlock();

            if (m_sensor.GetDoorState() == DOOR_STATE.OPEN)
            {
                m_startTime = DateTimeOffset.Now;
                m_lastTime = m_startTime;
                m_doorOpenTimer.Start();
                button.Content = "Close";
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
                if (e.Edge == GpioPinEdge.FallingEdge) HandleCloseEvent();
                else HandleOpenEvent();
            });
        }

        private void HandleOpenEvent()
        {
            button.Content = "Close";
            ledEllipse.Fill = m_greenBrush;
            GpioStatus.Text = $"Door is OPEN\nDoor was opened at {DateTime.Now}.";
            m_history.Enqueue($"{DateTime.Now} - Door is OPEN.");
            if (m_history.Count > HISTORY_QUEUE_SIZE) m_history.Dequeue();
            UpdateHistoryTextBlock();
            m_startTime = DateTimeOffset.Now;
            m_lastTime = m_startTime;
            m_doorOpenTimer.Start();
        }

        private void HandleCloseEvent()
        {
            TimeSpan duration = DateTimeOffset.Now - m_startTime;
            button.Content = "Open";
            ledEllipse.Fill = m_redBrush;
            GpioStatus.Text = $"Door is CLOSED\nDoor was closed at {DateTime.Now}.";
            m_history.Enqueue($"{DateTime.Now} - Door is CLOSED (open duration: {duration.ToString(@"hh\:mm\:ss")})");
            if (m_history.Count > HISTORY_QUEUE_SIZE) m_history.Dequeue();
            UpdateHistoryTextBlock();
            if (m_sendClosedAlert) SendAlertEmail("CLOSED");
            m_sendClosedAlert = false;

            if (m_doorOpenTimer.IsEnabled)
            {
                m_doorOpenTimer.Stop();
                m_stopTime = DateTimeOffset.Now;
            }
        }

        private void DispatcherTimerSetup()
        {
            m_doorOpenTimer.Tick += DoorOpenTimer_Tick;
            m_doorOpenTimer.Interval = new TimeSpan(ALERT_DELAY_HOURS, ALERT_DELAY_MINUTES, ALERT_DELAY_SECONDS);
        }

        /// <summary>
        /// Checks how long the door has been open and if it exceeds predefined duration,
        /// sends an alert e-mail.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoorOpenTimer_Tick(object sender, object e)
        {
            DateTimeOffset timeNow = DateTimeOffset.Now;
            TimeSpan timeSinceLastTick = timeNow - m_lastTime;
            m_lastTime = timeNow;
            m_timesTicked++;

            if (m_timesTicked >= MAX_TICKS)
            {
                m_stopTime = timeNow;
                m_doorOpenTimer.Stop();
                timeSinceLastTick = m_stopTime - m_startTime;
                SendAlertEmail("OPEN");
                m_sendClosedAlert = true;
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            m_sensor.InitiateButtonPush();
        }

        private void buttonClearHistory_Click(object sender, RoutedEventArgs e)
        {
            m_history.Clear();
            UpdateHistoryTextBlock();
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
        private void SendAlertEmail(string doorStatus)
        {
            using (Mailer m = new Mailer())
            {
                m.Connect();
                m.Send(doorStatus, GpioStatus.Text + Environment.NewLine + Environment.NewLine + textBlock.Text);
            }
        }

        #region IDisposable Support
        private bool m_alreadyDisposed = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!m_alreadyDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    m_sensor?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                m_alreadyDisposed = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MainPage() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
