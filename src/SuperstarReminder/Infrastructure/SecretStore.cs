using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace SuperstarReminder.Infrastructure;
public static class SecretStore
{
    private const string Service="SuperstarReminder";
    public static string? Load()
    {
        var configured=Environment.GetEnvironmentVariable("SUPERSTAR_MYSQL_CONNECTION");
        if(!string.IsNullOrWhiteSpace(configured)) return configured;
        if(OperatingSystem.IsWindows())
        {
            var path=Path.Combine(LocalSettings.DataDirectory,"database.enc");
            return File.Exists(path)?Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)):null;
        }
        if(OperatingSystem.IsMacOS())
        {
            var status=SecKeychainFindGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,8,"database",out var length,out var data,out var item);
            if(status!=0)return null;
            try{return Marshal.PtrToStringUTF8(data,(int)length);}finally{SecKeychainItemFreeContent(IntPtr.Zero,data);if(item!=IntPtr.Zero)CFRelease(item);}
        }
        return null;
    }
    public static void Save(string value)
    {
        if(OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(LocalSettings.DataDirectory);
            File.WriteAllBytes(Path.Combine(LocalSettings.DataDirectory,"database.enc"),ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));
        }
        else if(OperatingSystem.IsMacOS())
        {
            var data=Encoding.UTF8.GetBytes(value);
            var found=SecKeychainFindGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,8,"database",out _,out var previous,out var item);
            int result;
            if(found==0)
            {
                SecKeychainItemFreeContent(IntPtr.Zero,previous);
                try{result=SecKeychainItemModifyAttributesAndData(item,IntPtr.Zero,(uint)data.Length,data);}finally{CFRelease(item);}
            }
            else
            {
                result=SecKeychainAddGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,8,"database",(uint)data.Length,data,out var created);
                if(created!=IntPtr.Zero)CFRelease(created);
            }
            CryptographicOperations.ZeroMemory(data);
            if(result!=0)throw new InvalidOperationException($"Keychain could not save the database login (status {result}).");
        }
        // Linux is development-only: do not persist unprotected credentials.
    }
    [DllImport("/System/Library/Frameworks/Security.framework/Security")]private static extern int SecKeychainFindGenericPassword(IntPtr keychain,uint serviceLength,[MarshalAs(UnmanagedType.LPUTF8Str)]string service,uint accountLength,[MarshalAs(UnmanagedType.LPUTF8Str)]string account,out uint length,out IntPtr data,out IntPtr item);
    [DllImport("/System/Library/Frameworks/Security.framework/Security")]private static extern int SecKeychainAddGenericPassword(IntPtr keychain,uint serviceLength,[MarshalAs(UnmanagedType.LPUTF8Str)]string service,uint accountLength,[MarshalAs(UnmanagedType.LPUTF8Str)]string account,uint length,byte[] data,out IntPtr item);
    [DllImport("/System/Library/Frameworks/Security.framework/Security")]private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item,IntPtr attributes,uint length,byte[] data);
    [DllImport("/System/Library/Frameworks/Security.framework/Security")]private static extern int SecKeychainItemFreeContent(IntPtr attributes,IntPtr data);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]private static extern void CFRelease(IntPtr value);
}
