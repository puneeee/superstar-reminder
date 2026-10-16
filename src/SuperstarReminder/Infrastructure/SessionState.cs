using System.Runtime.InteropServices;
using Avalonia;
namespace SuperstarReminder.Infrastructure;
public static class SessionState
{
    public static bool CanPresent(PixelRect workArea,PixelRect screenArea)
    {
        if(OperatingSystem.IsWindows())
        {
            var desktop=OpenInputDesktop(0,false,0x100); if(desktop==IntPtr.Zero)return false; CloseDesktop(desktop);
            var window=GetForegroundWindow(); if(window==IntPtr.Zero)return true;
            var name=new System.Text.StringBuilder(256);GetClassName(window,name,256);
            if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd")return true;
            if(!GetWindowRect(window,out var r))return true;
            return !(r.Left<=screenArea.X && r.Top<=screenArea.Y && r.Right>=screenArea.Right && r.Bottom>=screenArea.Bottom);
        }
        if(OperatingSystem.IsMacOS())return MacCanPresent(screenArea);
        return true;
    }
    private static bool MacCanPresent(PixelRect screen)
    {
        var session=CGSessionCopyCurrentDictionary();
        if(session!=IntPtr.Zero)
        {
            try
            {
                var locked=Value(session,"CGSSessionScreenIsLocked");if(locked!=IntPtr.Zero&&CFBooleanGetValue(locked))return false;
                var console=Value(session,"kCGSessionOnConsoleKey");if(console!=IntPtr.Zero&&!CFBooleanGetValue(console))return false;
            }finally{CFRelease(session);}
        }
        var windows=CGWindowListCopyWindowInfo(17,0);if(windows==IntPtr.Zero)return true;
        try
        {
            for(nint i=0;i<CFArrayGetCount(windows);i++)
            {
                var w=CFArrayGetValueAtIndex(windows,i);
                var layer=Value(w,"kCGWindowLayer");if(layer==IntPtr.Zero||!CFNumberGetValue(layer,4,out long level)||level!=0)continue;
                var bounds=Value(w,"kCGWindowBounds");if(bounds==IntPtr.Zero)continue;
                double Number(string key){var n=Value(bounds,key);return n!=IntPtr.Zero&&CFNumberGetValue(n,13,out double d)?d:0;}
                // First layer-zero window is the foreground app. CG coordinates are logical points.
                var width=Number("Width");var height=Number("Height");var x=Number("X");var y=Number("Y");
                var display=CGMainDisplayID();var full=CGDisplayBounds(display);
                return !(x<=full.X&&y<=full.Y&&width>=full.Width&&height>=full.Height);
            }
        }finally{CFRelease(windows);}
        return true;
    }
    private static IntPtr Value(IntPtr dictionary,string key)
    {
        var s=CFStringCreateWithCString(IntPtr.Zero,key,0x08000100);try{return CFDictionaryGetValue(dictionary,s);}finally{CFRelease(s);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct RECT{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll")]private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder name,int length);
    private const string Core="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string Graphics="/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    [StructLayout(LayoutKind.Sequential)]private struct CGRect{public double X,Y,Width,Height;}
    [DllImport(Graphics)]private static extern IntPtr CGSessionCopyCurrentDictionary();
    [DllImport(Graphics)]private static extern IntPtr CGWindowListCopyWindowInfo(uint option,uint relative);
    [DllImport(Graphics)]private static extern uint CGMainDisplayID();
    [DllImport(Graphics)]private static extern CGRect CGDisplayBounds(uint display);
    [DllImport(Core)]private static extern IntPtr CFStringCreateWithCString(IntPtr allocator,[MarshalAs(UnmanagedType.LPUTF8Str)]string text,uint encoding);
    [DllImport(Core)]private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary,IntPtr key);
    [DllImport(Core)]private static extern nint CFArrayGetCount(IntPtr array);
    [DllImport(Core)]private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array,nint index);
    [DllImport(Core)]private static extern bool CFBooleanGetValue(IntPtr value);
    [DllImport(Core)]private static extern bool CFNumberGetValue(IntPtr value,int type,out long result);
    [DllImport(Core)]private static extern bool CFNumberGetValue(IntPtr value,int type,out double result);
    [DllImport(Core)]private static extern void CFRelease(IntPtr value);
}
