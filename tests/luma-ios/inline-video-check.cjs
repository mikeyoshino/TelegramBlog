const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});
   let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Video',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Video group',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[{id:'1:2',group:'1',topic:'0',title:'Video',date:'2026-10-27',media:[{id:'1:2',kind:'video',thumb:'assets/placeholder.svg'},{id:'1:3',kind:'video',thumb:'assets/placeholder.svg'}]}],page:1,knownPages:1,hasNext:false};
   const reply=()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}}));
   if(action==='play'&&window.delayPlay)window.finishPlay=reply;else queueMicrotask(reply);
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();
 await page.locator('.play-native').click();await page.waitForFunction(()=>window.calls.some(c=>c.action==='videoLayout'));
 const plays=await page.evaluate(()=>window.calls.filter(c=>c.action==='play'));assert.equal(plays.length,1);assert.ok(plays[0].data.frame.width>200&&plays[0].data.frame.height>100);
 for(const id of ['prevMedia','nextMedia','viewerCount']){const rect=await page.locator('#'+id).boundingBox();assert.ok(rect.y+rect.height<=plays[0].data.frame.y+1,'Media navigation must remain outside native overlay: '+id)}
 assert.equal(await page.locator('#zoomButton').isVisible(),true);await page.locator('#zoomButton').click();await page.waitForFunction(()=>window.calls.some(c=>c.action==='expandVideo'));
 assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='play').length),1,'Expanding must reuse the playing video');
 const layouts=await page.evaluate(()=>window.calls.filter(c=>c.action==='videoLayout').length);await page.setViewportSize({width:759,height:393});await page.waitForFunction(n=>window.calls.filter(c=>c.action==='videoLayout').length>n,layouts);
 await page.setViewportSize({width:393,height:759});await page.locator('#nextMedia').click();
 await page.evaluate(()=>window.delayPlay=true);await page.locator('.play-native').click();await page.waitForFunction(()=>!!window.finishPlay);await page.locator('#closeViewer').click();
 const expands=await page.evaluate(()=>window.calls.filter(c=>c.action==='expandVideo').length);await page.evaluate(()=>window.finishPlay());await page.waitForTimeout(100);
 assert.equal(await page.locator('#viewer[open]').count(),0);assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='expandVideo').length),expands);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: one-tap inline request, expand reuses player, resize, late play cannot reopen a closed viewer.');
})().catch(e=>{console.error(e);process.exit(1)});
