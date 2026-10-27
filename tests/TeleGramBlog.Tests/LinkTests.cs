using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class LinkTests
{
    [Fact] public void OnlySafeHttpLinksAreAcceptedAndDeduplicated()
    {
        var links = LinkExtractor.Extract("อ่าน https://example.com/page และ https://example.com/page", ["javascript:alert(1)","file:///C:/test", "https://openai.com/"]);
        Assert.Equal(new[]{"https://openai.com/","https://example.com/page"},links);
    }
}
