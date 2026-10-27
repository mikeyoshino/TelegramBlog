using TL;
using TeleGramBlog.Telegram;
namespace TeleGramBlog.Tests;
public class TelegramMappingTests
{
    [Fact] public void GeneralDoesNotLeakOtherTopics()
    {
        Assert.True(MessageMapper.InTopic(new Message { id=55 },1));
        var other = new Message {id=57, reply_to=new MessageReplyHeader { flags=MessageReplyHeader.Flags.forum_topic, reply_to_msg_id=50, reply_to_top_id=50 }};
        Assert.False(MessageMapper.InTopic(other,1)); Assert.True(MessageMapper.InTopic(other,50));
        Assert.False(MessageMapper.InTopic(other,51));
    }
    [Fact] public void EntityOffsetsSupportEmojiAndThai()
    {
        var message = new Message {id=3,message="😀 ไทย link", entities=[new MessageEntityTextUrl {offset=7,length=4,url="https://example.com/ไทย"}]};
        var mapped = MessageMapper.Map(message,9);
        Assert.Single(mapped.Links); Assert.StartsWith("https://example.com/",mapped.Links[0]);
    }
    [Fact] public void UnsupportedDocumentDoesNotPretendToBePhoto()
    {
        var mapped = MessageMapper.Map(new Message { id=3, message="", media=new MessageMediaDocument {document=new Document {mime_type="application/pdf"}} },9);
        Assert.Null(mapped.Media);
    }
}
