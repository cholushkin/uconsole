using UnityEngine;



namespace uconsole
{
    public class ConsoleActivatorBase : MonoBehaviour
    {
        public GameObject ConsoleInstance;
        
        public void ToggleConsole()
        {
            ConsoleInstance.GetComponent<UConsoleController>().Toggle();
        }

        public void DestroyConsole()
        {
            Destroy(ConsoleInstance);
            ConsoleInstance = null;
        }
    }
}
