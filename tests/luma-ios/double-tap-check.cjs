const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});
   let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Video',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Video group',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[{id:'1:2',group:'1',topic:'0',title:'Video',date:'2026-10-27',media:[{id:'1:2',kind:'photo',thumb:'assets/placeholder.svg'},{id:'1:3',kind:'video',thumb:'assets/placeholder.svg'}]}],page:1,knownPages:1,hasNext:false};
   const reply=()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}}));
   if(action==='play'&&window.delayPlay)window.finishPlay=reply;else queueMicrotask(reply);
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();
 await page.locator('#viewerStage').dispatchEvent('dblclick');assert.equal(await page.locator('#viewer.media-fullscreen').count(),1);assert.equal(await page.locator('.viewer-bottom').isVisible(),false);assert.equal(await page.locator('.viewer-head').isVisible(),false);await page.locator('#exitMediaFullscreen').click();assert.equal(await page.locator('.viewer-bottom').isVisible(),true);
 await page.locator('#nextMedia').click();await page.locator('#viewerStage').dispatchEvent('dblclick');await page.waitForFunction(()=>calls.some(c=>c.action==='expandVideo'));assert.equal(await page.evaluate(()=>calls.filter(c=>c.action==='play').length),1);
 await page.locator('#viewerStage').dispatchEvent('dblclick');await page.waitForFunction(()=>calls.filter(c=>c.action==='expandVideo').length===2);assert.equal(await page.evaluate(()=>calls.filter(c=>c.action==='play').length),1,'Double tap reuses video');
 await page.locator('#prevMedia').click();await page.locator('#viewerStage').dispatchEvent('dblclick');await page.locator('#exitMediaFullscreen').click();await page.locator('#nextMedia').click();await page.evaluate(()=>window.delayPlay=true);await page.locator('.play-native').click();await page.waitForFunction(()=>!!window.finishPlay);await page.locator('#viewerStage').dispatchEvent('dblclick');await page.evaluate(()=>window.finishPlay());await page.waitForFunction(()=>calls.filter(c=>c.action==='expandVideo').length===3);
 await page.locator('#closeViewer').click();await page.locator('[data-post]').click();assert.equal(await page.locator('#viewer.media-fullscreen').count(),0);assert.deepEqual(errors,[]);await browser.close();console.log('PASS: double tap photo fullscreen/exit, video reuse, expansion while loading and reset on reopen.');
})().catch(e=>{console.error(e);process.exit(1)});
