const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  const svg=color=>'data:image/svg+xml;base64,'+btoa(`<svg xmlns="http://www.w3.org/2000/svg" width="90" height="100"><rect width="90" height="100" fill="${color}"/></svg>`);
  window.red=svg('red');window.green=svg('green');window.calls=[];window.heldDecodes=[];
  const decode=HTMLImageElement.prototype.decode;
  HTMLImageElement.prototype.decode=function(){const image=this;if(window.holdGreen&&image.src===window.green)return new Promise((resolve,reject)=>window.heldDecodes.push(()=>decode.call(image).then(resolve,reject)));return decode.call(this)};
  window.releaseDecodes=()=>{window.holdGreen=false;for(const release of window.heldDecodes.splice(0))release()};
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Switch',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Album',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[{id:'1:2',group:'1',topic:'0',title:'Album',date:'2026-10-27',media:[{id:'1:2',kind:'photo'},{id:'1:3',kind:'photo'}]}],page:1,knownPages:1,hasNext:false};
   if(action==='thumbnail'||action==='original')result=data.index===0?window.red:window.green;
   if(action==='original'&&window.badOriginal){window.badOriginal=false;result='data:image/png;base64,AA=='}
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src===window.red&&!document.querySelector('.viewer-loading'));
 await page.evaluate(()=>{window.originalNode=document.querySelector('#viewerStage .main-media');window.stripNode=document.querySelector('#filmstrip [data-media-index="0"]');window.holdGreen=true});
 const before=await page.evaluate(()=>window.calls.filter(c=>c.action==='original').length);await page.locator('[data-media-index="0"]').click();assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='original').length),before,'Selected thumbnail is a no-op');
 await page.locator('[data-media-index="1"]').click();await page.waitForFunction(()=>window.heldDecodes.length>0);
 assert.equal(await page.evaluate(()=>window.originalNode.isConnected&&document.querySelector('#viewerStage .main-media')===window.originalNode),true,'Keep visible image until the replacement has decoded');
 assert.equal(await page.evaluate(()=>document.querySelector('#filmstrip [data-media-index="0"]')===window.stripNode),true,'Do not rebuild loaded thumbnail DOM');
 await page.evaluate(()=>window.releaseDecodes());await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src===window.green);
 await page.locator('[data-media-index="0"]').click();await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src===window.red);
 assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='original'&&c.data.index===0).length),1,'Revisited originals come from the viewer cache');
 await page.evaluate(()=>window.holdGreen=true);await page.locator('[data-media-index="1"]').click();await page.waitForFunction(()=>window.heldDecodes.length>0);await page.locator('[data-media-index="0"]').click();await page.evaluate(()=>window.releaseDecodes());await page.waitForTimeout(50);
 assert.equal(await page.evaluate(()=>document.querySelector('#viewerStage .main-media')?.src===window.red),true,'Late decoded image cannot replace the current selection');
 await page.locator('#closeViewer').click();assert.equal(await page.locator('#viewerStage .main-media').count(),0);await page.locator('[data-post]').click();await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src===window.red);assert.equal(await page.locator('#filmstrip [data-media-index]').count(),2);
 await page.reload();await page.locator('[data-group="1"]').click();await page.evaluate(()=>window.badOriginal=true);await page.locator('[data-post]').click();await page.waitForSelector('[data-retry-media]');await page.locator('[data-retry-media]').click();await page.waitForFunction(()=>!document.querySelector('.viewer-loading'));
 assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='original'&&c.data.index===0).length),2,'Failed decode must not poison the original cache');
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: no blank frame during decode, stable filmstrip, same-thumbnail no-op, cached originals and stale decode protection.');
})().catch(e=>{console.error(e);process.exit(1)});
