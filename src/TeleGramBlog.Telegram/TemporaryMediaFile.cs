namespace TeleGramBlog.Telegram;
public sealed class TemporaryMediaFile : IDisposable
{
    public string Path {get;}
    TemporaryMediaFile(string path)=>Path=path;
    public static async Task<TemporaryMediaFile> DownloadAsync(string directory,string extension,Func<Stream,CancellationToken,Task> download,CancellationToken ct)
    {
        if(extension is not (".mp4" or ".webm" or ".mov" or ".mkv"))throw new ArgumentException("Unsupported media extension.");
        ct.ThrowIfCancellationRequested();Directory.CreateDirectory(directory);
        var path=System.IO.Path.Combine(directory,Guid.NewGuid().ToString("N")+extension);
        var partial=path+".partial";
        try
        {
            await using(var stream=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous))
            {await download(stream,ct);ct.ThrowIfCancellationRequested();await stream.FlushAsync(ct);}
            ct.ThrowIfCancellationRequested();File.Move(partial,path);return new(path);
        }
        catch {File.Delete(partial);File.Delete(path);throw;}
    }
    public void Dispose()
    {
        try{File.Delete(Path);}catch(IOException){_=DeleteWhenReleasedAsync(Path);}catch(UnauthorizedAccessException){}
    }
    static async Task DeleteWhenReleasedAsync(string path)
    {
        for(var attempt=0;attempt<10;attempt++)
        {
            await Task.Delay(200);
            try{File.Delete(path);return;}catch(IOException){}catch(UnauthorizedAccessException){return;}
        }
        // OS shutdown/crashes can leave a file; account cleanup also removes playback files.
    }
}
