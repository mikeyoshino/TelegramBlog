using Windows.Storage.Pickers;
namespace Luma.Services;
internal static class DesktopFileExport
{
    public static async Task SaveAsync(string path,string title)
    {
        var extension=Path.GetExtension(path);
        if(string.IsNullOrEmpty(extension))extension=".bin";
        var invalid=Path.GetInvalidFileNameChars();
        var name=new string(title.Where(c=>!invalid.Contains(c)).Take(80).ToArray()).Trim().TrimEnd('.');
        var picker=new FileSavePicker {SuggestedStartLocation=PickerLocationId.Downloads,SuggestedFileName=string.IsNullOrWhiteSpace(name)?"TeleBlog":name};
        picker.FileTypeChoices.Add("Media",new List<string>{extension});
        var window=Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("Window is not ready.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(window));
        var selected=await picker.PickSaveFileAsync();if(selected==null)return;
        await using var input=File.OpenRead(path);
        await using var output=await selected.OpenStreamForWriteAsync();
        output.SetLength(0);await input.CopyToAsync(output);
    }
}
