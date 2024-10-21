using UnityEngine.UI;

namespace uconsole
{
    public class ConsoleCommandsUConsoleController
    {
        [ConsoleMethod("console.scale", "conscale", "Scale console canvas", "scale value")]
        public static void ScaleConsole(float scale)
        {
            UConsoleController.Instance.GetComponent<CanvasScaler>().scaleFactor = scale;
        }
    }
}