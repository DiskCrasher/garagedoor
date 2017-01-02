using System;
using Windows.Devices.Gpio;

namespace GarageDoor
{
    /// <summary>
    /// This class is used to communicate with the Raspberry Pi GPIO pins.
    /// </summary>
    internal class GarageDoorSensor
    {
        // GPIO numbers (not physical header pins)
        //private const int LED_PIN = 6;
        private const int SWITCH_PIN = 5;

        //private GpioPin ledPin;
        private GpioPin switchPin;
        //private GpioPinValue ledPinValue = GpioPinValue.High;

        private MainPage callingForm;

        public enum DOOR_STATE { UNKNOWN, OPEN, CLOSED }

        /// <summary>
        /// Sets the calling form and initializes GPIO pins.
        /// </summary>
        /// <param name="caller">An instance of MainPage.</param>
        internal GarageDoorSensor(MainPage caller)
        {
            callingForm = caller;
            InitGPIO();
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
                throw new InvalidOperationException("There is no GPIO controller on this device.");

            switchPin = gpio.OpenPin(SWITCH_PIN);
            //ledPin = gpio.OpenPin(LED_PIN);

            // Initialize LED to the OFF state by first writing a HIGH value
            // We write HIGH because the LED is wired in a active LOW configuration
            //ledPin.Write(GpioPinValue.High);
            //ledPin.SetDriveMode(GpioPinDriveMode.Output);

            // Check if input pull-up resistors are supported
            if (switchPin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                switchPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                switchPin.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            switchPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            switchPin.ValueChanged += buttonPin_ValueChanged;

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
            callingForm.UpdateStatus(e);
        }

        internal DOOR_STATE GetDoorState()
        {
            return (switchPin.Read() == GpioPinValue.Low) ? DOOR_STATE.CLOSED : DOOR_STATE.OPEN;
        }
    }
}