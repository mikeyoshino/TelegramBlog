using AVFoundation;
using AVKit;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using TeleGramBlog.Telegram;
using UIKit;
namespace Luma.Services;

public sealed record VideoFrame(double X, double Y, double Width, double Height, double ViewportWidth);

public sealed class NativeMedia
{
    AVPlayerViewController? controller;
    AVPlayer? playback;
    UIViewController? parent;
    UIView? web;
    UIButton? collapseButton;
    UITapGestureRecognizer? doubleTap;
    StreamingResourceLoader? loader;
    AVUrlAsset? asset;
    VideoFrame? frame;
    bool expanded;
    bool audioActive;
    public void PlayStream(ChunkedMediaSource source, UIView host, VideoFrame bounds)
    {
        Close();
        web = host;
        parent = Platform.GetCurrentUIViewController() ?? throw new InvalidOperationException("Cannot open video.");
        try
        {
            var audio = AVAudioSession.SharedInstance();
            if(!audio.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionMode.MoviePlayback, default, out var categoryError))
            { categoryError?.Dispose(); throw new IOException("ไม่สามารถตั้งค่าเสียงวิดีโอได้ กรุณาลองอีกครั้ง"); }
            if(!audio.SetActive(true, out var activationError))
            { activationError?.Dispose(); throw new IOException("ไม่สามารถเปิดเสียงได้ กรุณาหยุดสายโทรศัพท์หรือแอปที่ใช้เสียงแล้วลองอีกครั้ง"); }
            audioActive = true;
            loader = new StreamingResourceLoader(source);
            asset = new AVUrlAsset(new NSUrl("luma-media://telegram/video"), new AVUrlAssetOptions());
            asset.ResourceLoader.SetDelegate(loader, DispatchQueue.MainQueue);
            playback = new AVPlayer(new AVPlayerItem(asset)) { Muted = false, Volume = 1.0f };
            controller = new AVPlayerViewController { Player = playback, ShowsPlaybackControls = true,
                AllowsPictureInPicturePlayback = false };
            controller.View!.BackgroundColor = UIColor.Black;
            doubleTap = new UITapGestureRecognizer(() => { if(expanded) Collapse(); else Expand(); })
            {
                NumberOfTapsRequired = 2,
                CancelsTouchesInView = false,
                ShouldRecognizeSimultaneously = (_, _) => true,
                ShouldReceiveTouch = (_, touch) =>
                {
                    for(var view = touch.View; view != null; view = view.Superview)
                        if(view is UIControl) return false;
                    return true;
                }
            };
            controller.View.AddGestureRecognizer(doubleTap);
            parent.AddChildViewController(controller);
            parent.View!.AddSubview(controller.View);
            controller.DidMoveToParentViewController(parent);
            UpdateFrame(bounds);
            // The user's tap is the play action. No presentation or second Play tap.
            playback.Play();
        }
        catch { Close(); throw; }
    }
    public void UpdateFrame(VideoFrame bounds)
    {
        if(!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) ||
            !double.IsFinite(bounds.Height) || !double.IsFinite(bounds.ViewportWidth) || bounds.Width <= 0 || bounds.Height <= 0 || bounds.ViewportWidth <= 0)
            throw new ArgumentException("Invalid video frame.");
        frame = bounds;
        Layout();
    }
    void Layout()
    {
        if(controller?.View == null || parent?.View == null || web == null || frame == null) return;
        if(expanded)
        {
            controller.View.Frame = parent.View.Bounds;
            if(collapseButton != null) collapseButton.Frame = new CGRect(parent.View.SafeAreaInsets.Left + 16, parent.View.SafeAreaInsets.Top + 8, 78, 44);
        }
        else
        {
            var scale = (double)web.Bounds.Width / frame.ViewportWidth;
            var rect = new CGRect(frame.X * scale, frame.Y * scale, frame.Width * scale, frame.Height * scale);
            controller.View.Frame = web.ConvertRectToView(rect, parent.View);
        }
    }
    public void Expand()
    {
        if(controller == null || parent?.View == null) return;
        expanded = true;
        parent.View.BringSubviewToFront(controller.View!);
        collapseButton ??= UIButton.FromType(UIButtonType.System);
        collapseButton.SetTitle("ย่อหน้าจอ", UIControlState.Normal);
        collapseButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        collapseButton.BackgroundColor = UIColor.FromWhiteAlpha(0.1f, 0.85f);
        collapseButton.Layer.CornerRadius = 12;
        collapseButton.AccessibilityLabel = "ย่อวิดีโอกลับหน้าดูสื่อ";
        collapseButton.TouchUpInside -= CollapseTapped;
        collapseButton.TouchUpInside += CollapseTapped;
        parent.View.AddSubview(collapseButton);
        Layout();
    }
    void CollapseTapped(object? sender, EventArgs e) => Collapse();
    public void Collapse()
    {
        expanded = false;
        collapseButton?.RemoveFromSuperview();
        Layout();
    }
    public void Close()
    {
        expanded = false;
        collapseButton?.RemoveFromSuperview(); collapseButton?.Dispose(); collapseButton = null;
        var previous = controller; controller = null;
        if(doubleTap != null)
        {
            previous?.View?.RemoveGestureRecognizer(doubleTap);
            doubleTap.Dispose(); doubleTap = null;
        }
        playback?.Pause(); playback?.ReplaceCurrentItemWithPlayerItem(null);
        if(previous != null)
        {
            previous.DismissViewController(false, null);
            previous.WillMoveToParentViewController(null);
            previous.View?.RemoveFromSuperview();
            previous.RemoveFromParentViewController();
            previous.Player = null;
            previous.Dispose();
        }
        playback?.Dispose(); playback = null;
        loader?.Dispose(); loader = null; asset?.Dispose(); asset = null;
        parent = null; web = null; frame = null;
        if(audioActive)
        {
            audioActive = false;
            AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out var error);
            error?.Dispose();
        }
    }
#if DEBUG
    internal bool AudioReady => audioActive && playback is { Muted:false, Volume: > 0 }
        && AVAudioSession.SharedInstance().Category == AVAudioSessionCategory.Playback.GetConstant();
    internal double Position => playback?.CurrentTime.Seconds ?? 0;
    internal bool IsEmbedded => controller?.ParentViewController != null && controller.View?.Superview != null;
    internal bool IsExpanded => expanded;
    internal CGRect? PlayerFrame => controller?.View?.Frame;
    internal Task<bool> SeekAsync(double seconds) => playback?.SeekAsync(CoreMedia.CMTime.FromSeconds(seconds, 600), CoreMedia.CMTime.Zero, CoreMedia.CMTime.Zero) ?? Task.FromResult(false);
#endif
}
