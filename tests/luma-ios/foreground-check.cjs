const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];window.failResume=false;window.label='Group before lock';
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push(action);let result=null,error;
   if(action==='start')result={connected:true,account:'1',name:'Foreground',favorites:[]};
   if(action==='resume'){if(window.failResume)error='Network unavailable';else result={connected:true};}
   if(action==='groups')result=[{id:'1',title:window.label,isForum:false}];
   if(action==='topics'){if(window.delayTopics){window.delayTopics=false;window.finishTopics=()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result:[{id:'0',title:'All'}]})}}));return}result=[{id:'0',title:'All'}];}
   if(action==='history')result={version:data.version,posts:[{id:'1:2',group:'1',topic:'0',title:'Video',date:'2026-10-27',media:[{id:'1:2',kind:'video',thumb:'assets/placeholder.svg'}]}],page:1,knownPages:1,hasNext:false};
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result,error})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();await page.locator('.play-native').click();
 await page.evaluate(()=>{window.dispatchEvent(new CustomEvent('luma:suspend'));window.dispatchEvent(new CustomEvent('luma:resume'))});
 await page.waitForTimeout(150);
 assert.ok(await page.evaluate(()=>window.calls.includes('resume')),'Foreground must reconnect Telegram transport');
 assert.equal(await page.locator('#viewer[open]').count(),1,'Lock/unlock must retain the media viewer');
 await page.locator('.play-native').click();await page.waitForFunction(()=>window.calls.filter(a=>a==='play').length===2);
 await page.locator('#closeViewer').click();await page.locator('[data-tab="groups"]').click();await page.waitForSelector('[data-group="1"]');
 await page.evaluate(()=>{window.failResume=true;window.dispatchEvent(new CustomEvent('luma:suspend'));window.dispatchEvent(new CustomEvent('luma:resume'))});await page.waitForSelector('[data-status="retry"]');
 assert.equal(await page.locator('[data-group="1"]').count(),1,'Network failure must retain previous groups');
 await page.evaluate(()=>{window.failResume=false;window.label='Group after reconnect'});await page.locator('[data-status="retry"]').click();await page.waitForFunction(()=>document.querySelector('.group-description h3')?.textContent==='Group after reconnect');
 await page.evaluate(()=>window.delayTopics=true);await page.locator('[data-group="1"]').click();await page.waitForFunction(()=>!!window.finishTopics);
 await page.evaluate(()=>{window.dispatchEvent(new CustomEvent('luma:suspend'));window.dispatchEvent(new CustomEvent('luma:resume'))});
 await page.waitForSelector('[data-post]');assert.ok(await page.evaluate(()=>window.calls.filter(a=>a==='topics').length)>=3,'Interrupted group navigation must fetch topics again');
 await page.evaluate(()=>window.finishTopics());assert.equal(await page.locator('[data-post]').count(),1);
 assert.deepEqual(await page.evaluate(()=>window.calls.filter(a=>['login','qrStart','logout','authCancel'].includes(a))),[]);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: foreground reconnect, retained video viewer, playback after unlock, offline group retention and retry.');
})().catch(e=>{console.error(e);process.exit(1)});
