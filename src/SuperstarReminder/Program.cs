using Avalonia;
namespace SuperstarReminder;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance=new Mutex(true,"SuperstarReminder.Desktop.SingleInstance",out var first);
        if(!first)return;
        try{BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);}finally{instance.ReleaseMutex();}
    }
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
