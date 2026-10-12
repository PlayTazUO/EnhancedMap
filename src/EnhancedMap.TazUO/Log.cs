using Assistant;

public static class Log
{
    public static void Info(string text)
    {
        var prev = Console.BackgroundColor;
        var prevFor = Console.ForegroundColor;
        Console.BackgroundColor = ConsoleColor.Cyan;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($"[{Engine.PLUGIN_NAME}]  (INFO): {text}");
        Console.BackgroundColor = prev;
        Console.ForegroundColor = prevFor;
        Console.WriteLine();
    }

    public static void Warning(string text)
    {
        var prev = Console.BackgroundColor;
        var prevFor = Console.ForegroundColor;
        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($"[{Engine.PLUGIN_NAME}]  (WARN): {text}");
        Console.BackgroundColor = prev;
        Console.ForegroundColor = prevFor;
        Console.WriteLine();
    }

    public static void Error(string text)
    {
        var prev = Console.BackgroundColor;
        var prevFor = Console.ForegroundColor;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($"[{Engine.PLUGIN_NAME}] (ERROR): {text}");
        Console.BackgroundColor = prev;
        Console.ForegroundColor = prevFor;
        Console.WriteLine();
    }
}
