using System.Security.Cryptography;
namespace TeleGramBlog.Telegram;
public sealed class ProtectedSessionStore(string path)
{
    readonly object gate=new();
    public byte[]? Load()
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        lock(gate) return File.Exists(path) ? ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser) : null;
    }
    public void Save(byte[] bytes)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        lock(gate) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path+".tmp",encrypted); File.Move(path+".tmp",path,true);
        }
    }
    public void Clear() { lock(gate) { File.Delete(path); File.Delete(path+".tmp"); } }
}
