using TeleGramBlog.Core;
using TL;
using WTelegram;
namespace TeleGramBlog.Telegram;
internal sealed class TelegramHistorySource(Client client,InputPeer peer,GroupInfo group,TopicInfo? topic,TelegramRequestGate requests):IHistorySource
{
    public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken cancellationToken)=>requests.RunAsync(()=>FetchCoreAsync(beforeId,cancellationToken),cancellationToken,true);
    async Task<HistoryBatch> FetchCoreAsync(int beforeId,CancellationToken cancellationToken)
    {
        var result=topic is {Id:>1}
            ? await client.Messages_GetReplies(peer,topic.Id,offset_id:beforeId,limit:100).WaitAsync(cancellationToken)
            : await client.Messages_GetHistory(peer,offset_id:beforeId,limit:100).WaitAsync(cancellationToken);
        var raw=result.Messages;
        var next=raw.Length==0?beforeId:raw.Min(m=>m.ID);
        var messages=raw.OfType<Message>().Where(m=>!group.IsForum || topic==null || MessageMapper.InTopic(m,topic.Id))
            .Select(m=>MessageMapper.Map(m,group.Id)).ToArray();
        return new(messages,next,raw.Length==0);
    }
}
