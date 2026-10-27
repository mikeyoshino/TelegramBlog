using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace TeleGramBlog.App.WinUI;

// Explicit developer preview mode only. Captures the real native visual tree, not a mockup.
internal static class ShellCapture
{
    public static async Task SaveAsync(Window window,string file,VisualElement? content=null)
    {
        var native=(Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
        var root=(Microsoft.UI.Xaml.FrameworkElement)(content?.Handler?.PlatformView??native.Content);
        var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(root);
        if(bitmap.PixelWidth==0||bitmap.PixelHeight==0)throw new InvalidOperationException("Native render produced no pixels.");
        var buffer=await bitmap.GetPixelsAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var folder=await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(file)!);
        var output=await folder.CreateFileAsync(Path.GetFileName(file),CreationCollisionOption.ReplaceExisting);
        using var stream=await output.OpenAsync(FileAccessMode.ReadWrite);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,buffer.ToArray());
        await encoder.FlushAsync();
        await File.AppendAllTextAsync(Path.Combine(Path.GetDirectoryName(file)!,"capture-report.txt"),$"{Path.GetFileName(file)}: {bitmap.PixelWidth}x{bitmap.PixelHeight}px, native content {root.ActualWidth:F0}x{root.ActualHeight:F0} DIP, display scale {root.XamlRoot.RasterizationScale:P0}\n");
    }
}
