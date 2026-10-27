using QRCoder;
namespace TeleGramBlog.Telegram;
public static class LoginQrImage
{
    public static byte[] Encode(string url)
    {
        if(!url.StartsWith("tg://login?token=",StringComparison.Ordinal)||url.Length<=17)
            throw new ArgumentException("Invalid Telegram login token.");
        using var data=QRCodeGenerator.GenerateQrCode(url,QRCodeGenerator.ECCLevel.M);
        using var png=new PngByteQRCode(data);
        return png.GetGraphic(8);
    }
}
