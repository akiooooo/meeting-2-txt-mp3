using System;
using System.IO;
using System.Reflection;
using System.Threading;

public class MNSmoke
{
    public static void Ignore<T>(T value) {}
    public static void Main()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var app = Assembly.LoadFrom(Path.Combine(root, "Meeting2Text.exe"));
        var notes = app.GetType("Meeting2Text.MeetingNotes");
        if (!(bool)notes.GetProperty("Ready").GetValue(null, null)) throw new Exception("Missing MN payload");
        var type = app.GetType("Meeting2Text.ExportProgress");
        var callback = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(type),
            typeof(MNSmoke).GetMethod("Ignore").MakeGenericMethod(type));
        string work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        string input = Path.Combine(work, "input.txt"), output = Path.Combine(work, "notes.txt");
        File.WriteAllText(input, "Анна: Как передадим отчёт? Иван: Договорились отправить отчёт в пятницу. Анна: Согласна, отправляем в пятницу.");
        notes.GetMethod("Write").Invoke(null, new object[]{input, output, new DateTime(2026,11,12), CancellationToken.None, callback});
        string text = File.ReadAllText(output);
        Console.WriteLine(text);
        if (!text.StartsWith("#MN") || !text.Contains("12.11.2026") || !text.Contains("Обсуждали:") ||
            !text.Contains("Решили:") || !text.Contains("1. ") || !text.ToLowerInvariant().Contains("пятниц"))
            throw new Exception("Expected explicit decision in MN output.");
    }
}
