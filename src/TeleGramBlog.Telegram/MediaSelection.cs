using TL;
namespace TeleGramBlog.Telegram;
public static class MediaSelection
{
    public static PhotoSizeBase? SelectPhotoSize(Photo photo,bool fullSize)
    {
        static int Width(PhotoSizeBase s)=>s switch {PhotoSize p=>p.w,PhotoSizeProgressive p=>p.w,PhotoCachedSize p=>p.w,_=>0};
        var sizes=photo.sizes.Where(s=>Width(s)>0).OrderBy(Width).ToArray();
        return fullSize?sizes.LastOrDefault():sizes.FirstOrDefault(s=>Width(s)>=400)??sizes.LastOrDefault();
    }
}
