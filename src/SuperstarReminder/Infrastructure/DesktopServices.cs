using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Avalonia.Controls;
namespace SuperstarReminder.Infrastructure;

public sealed class ArrivalAudio : IDisposable
{
    private Process? _process;
    public void Play(string? path)
    {
        Stop(); if(string.IsNullOrEmpty(path)||!File.Exists(path))return;
        try
        {
            if(OperatingSystem.IsWindows())
            {
                // MCI handles WAV/MP3 locally without a shell or additional media dependencies.
                var safe=path.Replace("\"","");
                _=mciSendString($"open \"{safe}\" type mpegvideo alias superstar_arrival",null,0,IntPtr.Zero);
                _=mciSendString("play superstar_arrival",null,0,IntPtr.Zero);
            }
            else if(OperatingSystem.IsMacOS())
            {
                var start=new ProcessStartInfo("/usr/bin/afplay"){UseShellExecute=false}; start.ArgumentList.Add(path);_process=Process.Start(start);
            }
        }
        catch (Exception e) when(e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { /* Visual prompt remains available. */ }
    }
    public void Stop()
    {
        if(OperatingSystem.IsWindows())_=mciSendString("close superstar_arrival",null,0,IntPtr.Zero);
        try{if(_process is {HasExited:false})_process.Kill();}catch(InvalidOperationException){}finally{_process?.Dispose();_process=null;}
    }
    public void Dispose()=>Stop();
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)]private static extern int mciSendString(string command,System.Text.StringBuilder? result,int length,IntPtr callback);
}

public static class LoginStartup
{
    public static void Set(bool enabled)
    {
        var executable=Environment.ProcessPath??throw new InvalidOperationException("Cannot find the installed application.");
        if(enabled && Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Launch at login requires the published application. Publish first, then enable this option.");
        if(OperatingSystem.IsWindows())
        {
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(enabled)key.SetValue("SuperstarReminder",$"\"{executable}\" --background");else key.DeleteValue("SuperstarReminder",false);
        }
        else if(OperatingSystem.IsMacOS())
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","LaunchAgents");Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,"com.superstarreminder.app.plist");
            if(enabled)File.WriteAllText(path,$"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict><key>Label</key><string>com.superstarreminder.app</string><key>ProgramArguments</key><array><string>{SecurityElement.Escape(executable)}</string><string>--background</string></array><key>RunAtLoad</key><true/></dict></plist>
                """);
            else if(File.Exists(path))File.Delete(path);
        }
    }
}

public static class OverlayInput
{
    public static void MakePassive(Window window)
    {
        var platform=window.TryGetPlatformHandle();
        var handle=platform?.Handle??IntPtr.Zero;
        if(handle==IntPtr.Zero)return;
        if(OperatingSystem.IsWindows())
        {
            var style=GetWindowLongPtr(handle,-20).ToInt64();
            SetWindowLongPtr(handle,-20,new IntPtr(style|0x20|0x08000000|0x80));
        }
        else if(OperatingSystem.IsMacOS())
        {
            if(platform?.HandleDescriptor=="NSView")handle=objc_msgSendWindow(handle,sel_registerName("window"));
            if(handle!=IntPtr.Zero && platform?.HandleDescriptor is "NSWindow" or "NSView")objc_msgSend(handle,sel_registerName("setIgnoresMouseEvents:"),true);
        }
    }
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("/usr/lib/libobjc.A.dylib")]private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")]private static extern IntPtr objc_msgSendWindow(IntPtr obj,IntPtr selector);
    [DllImport("/usr/lib/libobjc.A.dylib")]private static extern void objc_msgSend(IntPtr obj,IntPtr selector,[MarshalAs(UnmanagedType.I1)]bool value);
}
