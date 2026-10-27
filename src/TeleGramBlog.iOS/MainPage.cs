using System.Text.Json;
using Luma.Services;
namespace Luma;

public sealed class MainPage : ContentPage, IDisposable
{
    readonly HybridWebView web = new() { HybridRoot = "wwwroot", DefaultFile = "index.html" };
    readonly MobileBackend backend = new();
    bool disposed;
    bool suspended;
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public MainPage()
    {
        BackgroundColor = Colors.White;
        SafeAreaEdges = SafeAreaEdges.All;
        Content = web;
#if WINDOWS
        backend.MediaHost = () => web.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
#else
        backend.MediaHost = () => web.Handler?.PlatformView as UIKit.UIView;
#endif
        web.RawMessageReceived += Receive;
        backend.Changed += SendEvent;
        Connectivity.Current.ConnectivityChanged += NetworkChanged;
    }
    void SendEvent(string name, object data)
    {
        if(!disposed) web.SendRawMessage(JsonSerializer.Serialize(new { @event = name, data }, Json));
    }
    async void Receive(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Message) || e.Message.Length > 100_000) return;
        string? id = null;
        try
        {
            using var document = JsonDocument.Parse(e.Message);
            var request = document.RootElement;
            id = request.GetProperty("id").GetString();
            var action = request.GetProperty("action").GetString() ?? "";
            var data = request.GetProperty("data").Clone();
            var result = await backend.DispatchAsync(action, data);
            if (!disposed) web.SendRawMessage(JsonSerializer.Serialize(new { id, result }, Json));
        }
        catch (OperationCanceledException)
        {
            if (!disposed) web.SendRawMessage(JsonSerializer.Serialize(new { id, error = "ยกเลิกการโหลดแล้ว" }, Json));
        }
        catch (Exception error)
        {
            // Never include request contents (phone/OTP/password) in logs or replies.
            var message = error is InvalidOperationException or IOException or ArgumentException
                ? error.Message : "เชื่อมต่อไม่สำเร็จ กรุณาตรวจสอบอินเทอร์เน็ตแล้วลองอีกครั้ง";
            if (!disposed) web.SendRawMessage(JsonSerializer.Serialize(new { id, error = message }, Json));
        }
    }
    public void Suspend()
    {
        if(disposed || suspended)return;
        suspended = true;
        backend.Suspend();
        if (!disposed) web.SendRawMessage("{\"event\":\"suspend\"}");
    }
    public void Resume()
    {
        if(disposed)return;
        suspended = false;
        LifecycleDiagnostics.Record("foreground", backend.IsAuthenticated);
        SendEvent("resume", new { });
    }
    void NetworkChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if(e.NetworkAccess == NetworkAccess.Internet)
            MainThread.BeginInvokeOnMainThread(() => { if(!disposed && !suspended)SendEvent("network", new { internet = true }); });
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        web.RawMessageReceived -= Receive;
        backend.Changed -= SendEvent;
        Connectivity.Current.ConnectivityChanged -= NetworkChanged;
        backend.Dispose();
        web.Handler?.DisconnectHandler();
    }
}
