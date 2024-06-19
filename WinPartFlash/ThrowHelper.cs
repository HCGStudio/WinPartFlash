namespace WinPartFlash;

public static class ThrowHelper
{
    public static void ThrowArgumentExceptionIf(bool condition, string message)
    {
        if (condition)
            throw new ArgumentException(message);
    }
}