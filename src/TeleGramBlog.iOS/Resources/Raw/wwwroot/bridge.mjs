const pending = new Map();
let sequence = 0;
export const isNative = () => !!window.webkit?.messageHandlers?.webwindowinterop || !!window.HybridWebView;
export function request(action, data = {}) {
 return new Promise((resolve, reject) => {
  if (!window.HybridWebView) return reject(new Error('กรุณาเปิดผ่านแอป TeleBlog เพื่อเชื่อมต่อ Telegram'));
  const id = String(++sequence);
  const timer = ['qrStart','favorite'].includes(action) ? null : setTimeout(() => { pending.delete(id); reject(new Error('ใช้เวลานานกว่าปกติ กรุณาลองใหม่')); }, 180000);
  pending.set(id, {resolve, reject, timer});
  window.HybridWebView.SendRawMessage(JSON.stringify({id, action, data}));
 });
}
window.addEventListener('HybridWebViewMessageReceived', e => {
 const reply = JSON.parse(e.detail.message);
 if (reply.event) { window.dispatchEvent(new CustomEvent('luma:' + reply.event, {detail:reply.data})); return; }
 const job = pending.get(reply.id); if (!job) return;
 clearTimeout(job.timer); pending.delete(reply.id);
 reply.error ? job.reject(new Error(reply.error)) : job.resolve(reply.result);
});
