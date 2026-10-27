const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);let result=null;
   const make=(count,id)=>({id:'1:'+id,group:'1',topic:'0',title:'Album '+count,date:'2026-10-'+id,media:Array.from({length:count},(_,i)=>({id:id+':'+i,kind:i===2?'video':'photo',thumb:'assets/placeholder.svg'}))});
   if(action==='start')result={connected:true,account:'1',name:'Layout',favorites:[make(5,28)]};
   if(action==='groups')result=[{id:'1',title:'Albums',isForum:false}];
   if(action==='topics')result=[{id:'0',title:'All'}];
   if(action==='history')result={version:data.version,posts:[make(9,29),make(5,28),make(1,27)],page:1,knownPages:1,hasNext:false};
   if(action==='organization')result={collections:[{id:'a',name:'Learning'}],items:[]};
   if(action==='original')result='assets/placeholder.svg';
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();
 const saved=page.locator('[data-post="1:28"]');await saved.click({button:'right'});await page.waitForSelector('[data-context-action="remove"]');await page.locator('[data-context-action="note"]').click();await page.waitForSelector('#collectionNote');await page.locator('[data-cancel-organization]').click();
 const cover=page.locator('[data-post="1:29"]');await cover.waitFor();
 const hold=async target=>{const r=await target.boundingBox();await target.dispatchEvent('pointerdown',{pointerId:1,isPrimary:true,button:0,clientX:r.x+15,clientY:r.y+15});await page.waitForTimeout(650);await target.dispatchEvent('pointerup',{pointerId:1});await target.dispatchEvent('click')};
 await hold(cover);assert.equal(await page.locator('.saved-context-layer[open]').count(),1,'Library image hold opens menu');assert.equal(await page.locator('#viewer[open]').count(),0,'Hold does not also open image');assert.equal(await page.locator('[data-context-action="remove"]').count(),0,'Unsaved posts have no removal action');
 await page.locator('[data-context-action="collection"]').click();await page.waitForSelector('#editCollection');assert.equal(await page.locator('#sheet[open]').count(),1);await page.locator('[data-cancel-organization]').click();
 const thumb=page.locator('[data-post-card="1:29"] .post-thumb').nth(1);await hold(thumb);await page.locator('[data-context-action="open"]').click();await page.waitForSelector('#viewer[open]');assert.equal(await page.locator('#filmstrip [data-media-index]').count(),9);await page.locator('#closeViewer').click();
 const r=await cover.boundingBox();await cover.dispatchEvent('pointerdown',{pointerId:2,isPrimary:true,button:0,clientX:r.x+15,clientY:r.y+15});await cover.dispatchEvent('pointermove',{pointerId:2,clientX:r.x+15,clientY:r.y+50});await page.waitForTimeout(650);await cover.dispatchEvent('pointerup',{pointerId:2});assert.equal(await page.locator('.saved-context-layer[open]').count(),0,'Scrolling cancels menu');
 await page.locator('#options').click();await page.locator('[data-library-layout="2"]').click();await cover.click({button:'right'});await page.waitForSelector('.saved-context-layer[open]');await page.keyboard.press('Escape');
 await page.locator('#selectButton').click();await hold(cover);assert.equal(await page.locator('.saved-context-layer[open]').count(),0,'Selection mode does not open menu');await page.locator('#selectButton').click();
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: library hold on covers/thumbnails, collection editor, two columns, scroll cancellation and selection mode.');
})().catch(e=>{console.error(e);process.exit(1)});
