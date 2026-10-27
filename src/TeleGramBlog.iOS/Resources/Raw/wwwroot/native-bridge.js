// MAUI raw-message protocol for Windows WebView2 and iOS WKWebView.
// https://learn.microsoft.com/dotnet/maui/user-interface/controls/hybridwebview
(() => {
 if(window.HybridWebView)return;
 const windows=window.chrome?.webview,apple=window.webkit?.messageHandlers?.webwindowinterop;
 if(!windows&&!apple)return;
 const receive=message=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message}}));
 window.HybridWebView={SendRawMessage(message){(windows||apple).postMessage('__RawMessage|'+message)}};
 if(windows)windows.addEventListener('message',event=>receive(event.data));
 else window.external={receiveMessage:receive};
})();
