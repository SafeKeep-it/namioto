namespace Thoth;

public static class ThothConsole
{
    public static IThothConsoleBuilder Create()
    {
        return new ThothConsoleBuilder();
    }
}