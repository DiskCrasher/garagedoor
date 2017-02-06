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
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace GarageDoor
{
    public enum DOOR_STATE { UNKNOWN, OPEN, CLOSED }

    /// <summary>
    /// This class is used to communicate with the Raspberry Pi GPIO pins.
    /// </summary>
    internal sealed class GarageDoorSensor : IDisposable
    {
        // GPIO numbers (not physical header pins)
        private const int OUTPUT_PIN = 6; // To 2N3053 transistor.
        private const int INPUT_PIN = 5; // From magnetic sensor.

        private GpioPin m_inputPin;
        private GpioPin m_outputPin;

        private MainPage m_callingForm;

        /// <summary>
        /// Sets the calling form and initializes GPIO pins.
        /// </summary>
        /// <param name="caller">An instance of MainPage.</param>
        internal GarageDoorSensor(MainPage caller)
        {
            m_callingForm = caller;
            InitGPIO();
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
                throw new InvalidOperationException("There is no GPIO controller on this device.");

            m_inputPin = gpio.OpenPin(INPUT_PIN);
            m_outputPin = gpio.OpenPin(OUTPUT_PIN);

            // Check if input pull-up resistors are supported
            if (m_inputPin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                m_inputPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                m_inputPin.SetDriveMode(GpioPinDriveMode.Input);

            if (m_outputPin.IsDriveModeSupported(GpioPinDriveMode.OutputOpenDrainPullUp))
                m_outputPin.SetDriveMode(GpioPinDriveMode.OutputOpenDrainPullUp);
            else
                m_outputPin.SetDriveMode(GpioPinDriveMode.Output);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            m_inputPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);

            // Initialize button to the OFF state by first writing a LOW value.
            // We write LOW because the output is wired in a active HIGH configuration.
            m_outputPin.Write(GpioPinValue.Low);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            m_inputPin.ValueChanged += buttonPin_ValueChanged;
        }

        private void buttonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            //toggle the state of the LED every time the button is pressed
            //if (e.Edge == GpioPinEdge.FallingEdge)
            //{
            //    ledPinValue = (ledPinValue == GpioPinValue.Low) ?
            //        GpioPinValue.High : GpioPinValue.Low;
            //    ledPin.Write(ledPinValue);
            //}

            //need to invoke UI updates on the UI thread because this event
            //handler gets invoked on a separate thread.
            m_callingForm.UpdateStatus(e);
        }

        internal DOOR_STATE GetDoorState()
        {
            return (m_inputPin.Read() == GpioPinValue.Low) ? DOOR_STATE.CLOSED : DOOR_STATE.OPEN;
        }

        internal void InitiateButtonPush()
        {
            m_outputPin.Write(GpioPinValue.High);
            Task.Delay(-1).Wait(100);
            m_outputPin.Write(GpioPinValue.Low);
        }

        #region IDisposable Support
        private bool m_alreadyDisposed = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!m_alreadyDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    m_inputPin?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                m_alreadyDisposed = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GarageDoorSensor() {
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