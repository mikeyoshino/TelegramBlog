const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];window.originalAttempts={};
  const image='data:image/svg+xml;base64,'+btoa('<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80"><rect width="80" height="80" fill="blue"/></svg>');
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});let result=null;
   const reply=(value,error)=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result:value,error})}}));
   if(action==='start')result={connected:true,account:'1',name:'Test',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Images',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[{id:'1:20',group:'1',topic:'0',title:'Album',date:'2026-10-27',media:Array.from({length:20},(_,i)=>({id:'1:'+i,kind:i===0?'video':'photo'}))}],page:1,knownPages:1,hasNext:false};
   if(action==='thumbnail'){if(data.index===0){window.finishPoster=()=>reply(image);return}result=image}
   if(action==='original'){
    window.originalAttempts[data.index]=(window.originalAttempts[data.index]||0)+1;
    if(data.index===1){window.finishOriginal=()=>reply(image);return}
    if(data.index===2&&window.originalAttempts[2]===1){queueMicrotask(()=>reply(null,'network failed'));return}
    result=image;
   }
   queueMicrotask(()=>reply(result));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();
 await page.waitForFunction(()=>!!window.finishPoster);assert.equal(await page.locator('.viewer-loading .media-spinner').count(),1);
 await page.evaluate(()=>window.finishPoster());await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src.startsWith('data:'));
 assert.equal(await page.locator('.viewer-loading').count(),0);
 assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.action==='thumbnail'&&c.data.index===0).length),1,'Poster and strip share one thumbnail request');
 assert.ok(await page.evaluate(()=>window.calls.filter(c=>c.action==='thumbnail').length)<10,'Do not load the entire filmstrip');
 await page.locator('#nextMedia').click();await page.waitForFunction(()=>!!window.finishOriginal);
 assert.equal(await page.locator('.viewer-loading .media-spinner').count(),1);
 assert.ok((await page.locator('#viewerStage .main-media').getAttribute('src')).startsWith('data:'),'Cached preview stays visible while original loads');
 await page.evaluate(()=>window.finishOriginal());await page.waitForFunction(()=>!document.querySelector('.viewer-loading'));
 await page.locator('#nextMedia').click();await page.waitForSelector('[data-retry-media]');await page.locator('[data-retry-media]').click();await page.waitForFunction(()=>!document.querySelector('.viewer-loading'));
 assert.equal(await page.evaluate(()=>window.originalAttempts[2]),2);
 await page.reload();await page.locator('[data-group="1"]').click();await page.locator('[data-post]').click();await page.waitForFunction(()=>!!window.finishPoster);
 await page.evaluate(()=>window.oldPoster=window.finishPoster);await page.locator('#closeViewer').click();await page.locator('[data-refresh]').click();await page.locator('[data-post]').click();
 await page.waitForFunction(()=>window.calls.filter(c=>c.action==='thumbnail'&&c.data.index===0).length===2);
 await page.evaluate(()=>window.oldPoster());await page.waitForTimeout(50);
 assert.ok(!(await page.evaluate(()=>document.querySelector('#viewerStage .main-media')?.getAttribute('src')||'')).startsWith('data:'),'Late canceled poster must not populate the new viewer');
 await page.screenshot({path:'/tmp/luma-loading-preview.png',animations:'disabled'});
 await page.evaluate(()=>window.finishPoster());await page.waitForFunction(()=>document.querySelector('#viewerStage .main-media')?.src.startsWith('data:'));
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: delayed video poster loads, deduplicated visible thumbnails, progressive original with animation, failed original retries.');
})().catch(e=>{console.error(e);process.exit(1)});
