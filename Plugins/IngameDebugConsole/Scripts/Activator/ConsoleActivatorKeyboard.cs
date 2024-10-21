using uconsole;
using UnityEngine;


namespace uconsole
{
    public class ConsoleActivatorKeyboard : ConsoleActivatorBase
    {
        public KeyCode[] Keys;
        private bool _toggled;

        void Update()
        {
            if (_toggled)
            {
                // process previous frame result to avoid printing toggle key to the console
                ToggleConsole();
            }
            _toggled = false;
            foreach (var keyCode in Keys)
                if (Input.GetKeyDown(keyCode))
                {
                    _toggled = true;
                    break;
                }
        }
    }
}