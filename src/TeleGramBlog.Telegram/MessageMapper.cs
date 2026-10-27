using TL;
using TeleGramBlog.Core;
namespace TeleGramBlog.Telegram;
public static class MessageMapper
{
    public static bool InTopic(Message message,int topicId)
    {
        var reply=message.reply_to as MessageReplyHeader;
        var actual=reply?.flags.HasFlag(MessageReplyHeader.Flags.forum_topic) == true
            ? (reply.reply_to_top_id != 0 ? reply.reply_to_top_id : reply.reply_to_msg_id) : 1;
        return actual==topicId || message.id==topicId;
    }
    public static HistoryMessage Map(Message message,long chatId)
    {
        var text=message.message ?? "";
        var urls=new List<string>();
        foreach(var entity in message.entities ?? [])
        {
            if(entity is MessageEntityTextUrl url) urls.Add(url.url);
            if(entity is MessageEntityUrl plain && plain.offset>=0 && plain.length>0 && plain.offset<=text.Length-plain.length)
            {
                var value=text.Substring(plain.offset,plain.length);
                urls.Add(value.Contains("://") ? value : "https://"+value);
            }
        }
        var kind=message.media switch
        {
            MessageMediaPhoto {photo:Photo}=>"photo",
            MessageMediaDocument {document:Document d} when d.mime_type?.StartsWith("image/",StringComparison.OrdinalIgnoreCase)==true=>"image",
            MessageMediaDocument {document:Document d} when d.mime_type?.StartsWith("video/",StringComparison.OrdinalIgnoreCase)==true=>"video",
            _=>null
        };
        return new(message.id,message.grouped_id,message.date,text,LinkExtractor.Extract(text,urls),kind==null?null:new(chatId,message.id,kind));
    }
}
