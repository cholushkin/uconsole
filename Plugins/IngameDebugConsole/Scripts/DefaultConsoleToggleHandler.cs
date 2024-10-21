using UnityEngine;

namespace uconsole
{
    /*
     * Default handler for showing and hiding the in-game console.
     * This handler broadcasts an event when the console is toggled (shown or hidden),
     * which other parts of the system can listen to and respond accordingly.
     *
     * The most common use case for this is to disable player input when the console is active
     * (so players don't accidentally control the character while using the console) and to
     * re-enable input when the console is hidden.
     *
     * Developers can replace this default handler with their own implementation
     * to handle specific game logic when the console is toggled.
     */
    public class DefaultConsoleToggleHandler : MonoBehaviour
    {
        public class EventConsoleToggle
        {
            public bool ConsoleEnabled;
        }

        public void OnConsoleToggle(bool flag)
        {
            GlobalEventAggregator.EventAggregator.Publish(new EventConsoleToggle { ConsoleEnabled = flag });
        }
    }
}