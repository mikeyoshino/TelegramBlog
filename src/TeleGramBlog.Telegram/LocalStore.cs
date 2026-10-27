using System.Text.Json;
using TeleGramBlog.Core;
namespace TeleGramBlog.Telegram;
public sealed class LocalStore(string root)
{
    public string Root {get;}=root;
    public bool ClearLocalData(ISessionStore session)
    {
        var success=true;
        try {session.Clear();} catch(Exception e) when(e is IOException or UnauthorizedAccessException){success=false;}
        var accounts=Path.Combine(Root,"accounts");
        try {if(Directory.Exists(accounts))Directory.Delete(accounts,true);} catch(Exception e) when(e is IOException or UnauthorizedAccessException){success=false;}
        return success;
    }
    public string AccountPath(long accountId)=>Path.Combine(Root,"accounts",accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public string HistoryPath(long accountId,long chatId,int topicId)=>Path.Combine(AccountPath(accountId),$"{chatId}_{topicId}.json");
    public HistorySnapshot? ReadHistory(long accountId,long chatId,int topicId)
    {
        try { var path=HistoryPath(accountId,chatId,topicId); if(!File.Exists(path))return null;using var stream=File.OpenRead(path);return JsonSerializer.Deserialize<HistorySnapshot>(stream); }
        catch(Exception e) when(e is IOException or JsonException) {return null;}
    }
    public void SaveHistory(long accountId,long chatId,int topicId,HistorySnapshot snapshot)
    {
        var path=HistoryPath(accountId,chatId,topicId); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using(var stream=File.Create(path+".tmp"))JsonSerializer.Serialize(stream,snapshot);File.Move(path+".tmp",path,true);
    }
    public void ClearAccount(long accountId)
    {
        var path=AccountPath(accountId); if(Directory.Exists(path)) Directory.Delete(path,true);
    }
    public void TrimMedia(long accountId)
    {
        var path=Path.Combine(AccountPath(accountId),"media"); if(!Directory.Exists(path)) return;
        long size=0;
        foreach(var f in new DirectoryInfo(path).GetFiles().OrderByDescending(f=>f.LastWriteTimeUtc))
        { size+=f.Length; if(size>256L*1024*1024) try {f.Delete();} catch(IOException) {} }
    }
}
