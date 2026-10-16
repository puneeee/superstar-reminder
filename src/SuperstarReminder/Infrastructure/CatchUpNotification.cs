using System.Diagnostics;
using System.Text;
namespace SuperstarReminder.Infrastructure;
public static class CatchUpNotification
{
    public static async Task<bool> ShowAsync(int count,CancellationToken ct)
    {
        ProcessStartInfo start;
        if(OperatingSystem.IsWindows())
        {
            // Static text and an integer only. No reminder content is inserted into a script.
            var script=$"[Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] > $null; [Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom.XmlDocument,ContentType=WindowsRuntime] > $null; $xml=New-Object Windows.Data.Xml.Dom.XmlDocument; $xml.LoadXml('<toast><visual><binding template=\"ToastGeneric\"><text>Superstar Reminder</text><text>{count} reminders were deferred. Open the app to review history.</text></binding></visual></toast>'); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Superstar Reminder').Show([Windows.UI.Notifications.ToastNotification]::new($xml))";
            start=new("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-NonInteractive");start.ArgumentList.Add("-EncodedCommand");start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        }
        else if(OperatingSystem.IsMacOS())
        {
            start=new("/usr/bin/osascript"){UseShellExecute=false};start.ArgumentList.Add("-e");start.ArgumentList.Add($"display notification \"{count} reminders were deferred. Open the app to review history.\" with title \"Superstar Reminder\"");
        }
        else return false;
        start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        try
        {
            using var process=Process.Start(start);if(process is null)return false;
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(5000);
            var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var error=process.StandardError.ReadToEndAsync(timeout.Token);
            try{await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(output,error);return process.ExitCode==0;}
            catch(OperationCanceledException){try{process.Kill();}catch(InvalidOperationException){}return false;}
        }
        catch(Exception e)when(e is System.ComponentModel.Win32Exception or IOException){return false;}
    }
}
