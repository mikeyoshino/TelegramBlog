using TL;
using TeleGramBlog.Telegram;
namespace TeleGramBlog.Tests;
public class UnreadSnapshotTests
{
    static ForumTopic Topic(int id,int count,int read=10,int top=100)=>new(){id=id,unread_count=count,read_inbox_max_id=read,top_message=top};
    [Fact] public void UsesTelegramCountRatherThanMessageIdDifference()
    {
        Assert.Equal(7,UnreadSnapshot.Count(7,10,9000));
        Assert.Equal(0,UnreadSnapshot.Count(4000,100,100));
        Assert.Equal(0,UnreadSnapshot.Count(-1,10,100));
        Assert.Equal(0,UnreadSnapshot.Count(0,10,100));
    }
    [Fact] public void CountsAllUniqueTopicsIncludingGeneralAndUsesLatestReadState()
    {
        var snapshot=new UnreadSnapshot();snapshot.Add([Topic(1,5),Topic(2,7)]);
        snapshot.Add([Topic(2,0,100),Topic(3,4)]);
        Assert.Equal(3,snapshot.TopicCount);Assert.Equal(9,snapshot.Complete(3));
        Assert.Throws<IOException>(()=>snapshot.Complete(2));
        snapshot.Add([Topic(1,5,100),Topic(3,4,100)]);
        Assert.Equal(0,snapshot.Complete(3));
    }
    [Fact] public void RejectsIncompleteAndReducedTopicDataInsteadOfReturningFalseZero()
    {
        var snapshot=new UnreadSnapshot();snapshot.Add([Topic(1,5)]);
        Assert.Throws<IOException>(()=>snapshot.Complete(2));
        var shortTopic=Topic(2,0);shortTopic.flags=ForumTopic.Flags.short_;
        Assert.Throws<IOException>(()=>snapshot.Add([shortTopic]));
        Assert.Equal(0,new UnreadSnapshot().Complete(0));
        Assert.Null(new TeleGramBlog.Core.GroupInfo(1,"Unknown",false).UnreadCount);
    }
}
