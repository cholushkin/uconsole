using uconsole;

public class ConsoleActivatorInitial : ConsoleActivatorBase
{
    public bool KillConsoleOnStart;
    public bool OpenConsoleOnStart;

    void Awake()
    {
        if (KillConsoleOnStart)
            DestroyConsole();
        
        if (OpenConsoleOnStart)
        {
            ConsoleInstance.GetComponent<UConsoleController>().ShowLogWindow();
        }
        else
        {
            ConsoleInstance.GetComponent<UConsoleController>().HideLogWindow();
        }
    }
}