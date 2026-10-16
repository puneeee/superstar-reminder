using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace SuperstarReminder;
public partial class App : Application
{
    private TrayIcon? _tray;
    public override void Initialize()=>AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var main=new MainWindow();desktop.MainWindow=main;
            var menu=new NativeMenu();
            var open=new NativeMenuItem("Open reminders");open.Click+=(_,_)=>main.ShowDashboard();menu.Items.Add(open);
            var pause=new NativeMenuItem("Pause / resume");pause.Click+=(_,_)=>main.TogglePause();menu.Items.Add(pause);
            menu.Items.Add(new NativeMenuItemSeparator());var quit=new NativeMenuItem("Quit");quit.Click+=(_,_)=>main.Quit();menu.Items.Add(quit);
            _tray=new TrayIcon{Icon=new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://SuperstarReminder/Assets/app.ico"))),ToolTipText="Superstar Reminder",Menu=menu};
            _tray.Clicked+=(_,_)=>main.ShowDashboard();TrayIcon.SetIcons(this,new TrayIcons{_tray});
            desktop.Exit+=(_,_)=>_tray.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
