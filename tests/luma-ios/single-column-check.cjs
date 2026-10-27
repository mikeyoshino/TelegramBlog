const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);let result=null;
   const make=(count,id)=>({id:'1:'+id,group:'1',topic:'0',title:'Album '+count,date:'2026-10-'+id,media:Array.from({length:count},(_,i)=>({id:id+':'+i,kind:i===2?'video':'photo',thumb:'assets/placeholder.svg'}))});
   if(action==='start')result={connected:true,account:'1',name:'Layout',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Albums',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[make(9,29),make(5,28),make(1,27)],page:1,knownPages:1,hasNext:false};
   if(action==='original')result='assets/placeholder.svg';
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await page.waitForSelector('.single-column');
 const album=page.locator('[data-post-card="1:29"]');assert.equal(await album.locator('.post-thumb').count(),5);assert.equal(await album.locator('.post-overflow').textContent(),'+4');
 assert.equal(await page.locator('[data-post-card="1:28"] .post-overflow').count(),0,'Five attachments fit without overflow');
 assert.equal(await page.locator('[data-post-card="1:27"] .post-thumbnails').count(),0,'Single attachment does not need duplicate thumbnails');
 const cover=await album.locator('.media-card-cover').boundingBox();assert.ok(cover.width>340);
 await album.locator('.post-thumb').last().click();await page.waitForSelector('#viewer[open]');assert.equal(await page.locator('#viewerCount').textContent(),'5 / 9');assert.equal(await page.locator('#filmstrip [data-media-index]').count(),9);await page.locator('#closeViewer').click();
 await page.locator('#options').click();await page.locator('[data-library-layout="2"]').click();await page.waitForSelector('.two-columns');assert.equal(await page.locator('.post-thumbnails').count(),0);
 await page.reload();await page.locator('[data-group="1"]').click();await page.waitForSelector('.two-columns');
 await page.locator('#options').click();await page.locator('[data-library-layout="1"]').click();await page.locator('#selectButton').click();await page.locator('[data-post-card="1:29"] .post-thumb').first().click();assert.match(await page.locator('#selectionCount').textContent(),/1/);assert.equal(await page.locator('#viewer[open]').count(),0);await page.locator('#selectButton').click();
 await page.setViewportSize({width:320,height:568});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: one-row covers, bounded thumbnails/+count, attachment opening, layout persistence and selection.');
})().catch(e=>{console.error(e);process.exit(1)});
