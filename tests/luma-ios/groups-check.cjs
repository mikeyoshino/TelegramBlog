const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.groupRefreshes=0;
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);let result=null,error=null;
   if(action==='start')result={connected:true,account:'123',name:'Test',favorites:[]};
   if(action==='groups'){
    if(window.failGroups)error='offline';if(data.refresh)window.groupRefreshes++;
    const recent={id:'2',title:'กลุ่มที่มีข้อความล่าสุด ชื่อยาวสำหรับตรวจหน้าจอ',isForum:true,unreadCount:window.allRead?0:128,lastMessageAt:'2026-10-27T08:45:00Z'};
    const older={id:'1',title:'กลุ่มก่อนหน้า',isForum:false,unreadCount:0,lastMessageAt:'2026-10-26T08:00:00Z'};
    result=data.refresh?[{...older,unreadCount:window.allRead?0:1,lastMessageAt:'2026-10-27T09:00:00Z'},recent]:[recent,older];
   }
   if(action==='avatar'&&!(data.group==='1'&&window.groupRefreshes))result='data:image/svg+xml;base64,'+btoa('<svg xmlns="http://www.w3.org/2000/svg" width="52" height="52"><rect width="52" height="52" fill="#187be8"/></svg>');
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result,error})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.waitForSelector('.group-avatar img');
 assert.equal(await page.locator('.group-mosaic').count(),0);
 assert.equal(await page.locator('.group-card').first().getAttribute('data-group'),'2');
 assert.equal(await page.locator('[aria-label="128 ข้อความที่ยังไม่อ่าน"]').textContent(),'128');
 assert.equal(await page.locator('[data-group="1"] .unread-badge').count(),0);
 await page.waitForFunction(()=>[...document.querySelectorAll('.group-avatar img')].every(i=>i.complete&&i.naturalWidth>0));
 await page.screenshot({path:'/tmp/luma-groups-list.png'});
 await page.locator('[data-refresh-groups]').click();await page.waitForFunction(()=>document.querySelector('.group-card')?.dataset.group==='1');
 assert.equal(await page.locator('[data-group="1"] .unread-badge').textContent(),'1');
 assert.equal(await page.locator('[data-group="1"] .group-avatar img').count(),0,'Removed group photo must not survive refresh');
 await page.evaluate(()=>window.failGroups=true);await page.locator('[data-refresh-groups]').click();await page.waitForSelector('.status-strip.error');assert.equal(await page.locator('.unread-badge').count(),0);assert.equal(await page.locator('.unread-sync').count(),0);
 await page.evaluate(()=>{window.failGroups=false;window.allRead=true});await page.locator('[data-refresh-groups]').click();await page.waitForFunction(()=>document.querySelectorAll('.unread-badge').length===0);assert.equal(await page.locator('.unread-caption,.unread-sync,.groups-summary,.group-description p').count(),0);
 await page.setViewportSize({width:320,height:568});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: real-avatar bridge, unread badge/zero state, latest-first service order and refresh, narrow screen.');
})().catch(e=>{console.error(e);process.exit(1)});
