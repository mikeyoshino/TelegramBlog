const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  const post={id:'1:7',group:'1',topic:'0',title:'My content',text:'My caption',date:'2026-10-27',media:[{id:'1:7',kind:'photo',thumb:'assets/placeholder.svg'}]};
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Test',favorites:[post]};
   if(action==='groups')result=[{id:'1',title:'Design',isForum:false,unreadCount:7,lastMessageAt:'2026-10-27T12:00:00Z'},{id:'2',title:'Code',isForum:true,unreadCount:0},{id:'3',title:'Travel',isForum:true,unreadCount:null}];
   if(action==='topics')result=[{id:'0',title:'All'}];if(action==='history')result={version:data.version,posts:[post],page:1,knownPages:1,hasNext:false,fetchedAt:'2026-10-27T12:00:00Z'};
   if(action==='organization')result={collections:[{id:'a',name:'Ideas'}],items:[{postId:post.id,collectionId:'a',note:'My personal note',viewed:true}]};
   if(action==='original'){window.finishImage=()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result:'assets/placeholder.svg'})}}));return}
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 const quiet=async()=>{assert.equal(await page.locator('#pageSubtitle').isVisible(),false);assert.equal(await page.locator('.end-note,.page-caption,.groups-summary,.unread-caption,.unread-sync').count(),0)};
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.waitForSelector('.group-card');await quiet();assert.equal(await page.locator('.unread-badge').count(),1);assert.equal(await page.locator('.unread-badge').textContent(),'7');assert.equal(await page.locator('.group-description p,.group-activity time').count(),0);assert.doesNotMatch(await page.locator('#content').innerText(),/ตรวจล่าสุด|มีหัวข้อ|ยังไม่ทราบยอด|ล่าสุดขึ้นก่อน|ยังไม่อ่าน/);await page.screenshot({path:'/tmp/teleblog-quiet-groups.png'});
 await page.locator('[data-group="1"]').click();await page.waitForSelector('[data-post="1:7"]');await quiet();assert.equal(await page.locator('.status-strip').isVisible(),false);await page.locator('#groupPicker').click();assert.equal(await page.locator('#sheet .sheet-subtitle').count(),0);assert.doesNotMatch(await page.locator('#sheetBody').innerText(),/มีหัวข้อ|เลือกกลุ่มที่/);await page.locator('#closeSheet').click();
 await page.locator('[data-post="1:7"]').click();await page.waitForSelector('.viewer-loading');assert.equal(await page.locator('.viewer-loading').innerText(),'');assert.equal(await page.locator('#viewerDate').isVisible(),false);await page.evaluate(()=>finishImage());await page.locator('#closeViewer').click();
 await page.locator('[data-tab="saved"]').click();await page.waitForFunction(()=>document.querySelector('.saved-card-info')?.textContent.includes('My personal note'));await quiet();assert.match(await page.locator('.saved-card-info').innerText(),/Ideas.*ดูแล้ว.*My personal note/s);await page.screenshot({path:'/tmp/teleblog-quiet-saved.png'});await page.locator('#options').click();assert.equal(await page.locator('#sheet .sheet-subtitle').count(),0);assert.equal(await page.locator('[data-page-size]').count(),0);await page.locator('#closeSheet').click();await page.locator('#profile').click();assert.equal(await page.locator('#sheet .sheet-tip').count(),0);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: groups numeric badge only, quiet library/saved/viewer/profile/options, personal metadata preserved.');
})().catch(e=>{console.error(e);process.exit(1)});
