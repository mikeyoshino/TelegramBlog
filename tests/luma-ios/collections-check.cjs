const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  let org={collections:[],items:[]};window.calls=[];window.failItem=false;
  const post={id:'1:7',group:'1',topic:'0',title:'เรียนเขียนโปรแกรม',text:'ทบทวน Swift',date:'2026-10-27',media:[{id:'1:7',kind:'photo',thumb:'assets/placeholder.svg'}]};
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});let result=null,error=null;
   if(action==='start')result={connected:true,account:'1',name:'Collections',favorites:[post]};
   if(action==='groups')result=[];
   if(action==='original')result='assets/placeholder.svg';
   if(action==='favorite'){if(data.changes[0].saved)result=[post];else{org.items=[];result=[];}}
   if(action==='organization'){
    if(data.operation==='collection'){const name=data.name.trim();if(org.collections.some(c=>c.name===name&&c.id!==data.id))error='มีหมวดชื่อนี้แล้ว';else if(data.id)org.collections.find(c=>c.id===data.id).name=name;else org.collections.push({id:String(org.collections.length+1),name});}
    if(data.operation==='item'){if(window.failItem)error='บันทึกไม่สำเร็จ';else org.items=[{postId:data.post,collectionId:data.collectionId,note:data.note,viewed:data.viewed}];}
    if(data.operation==='delete'){org.collections=org.collections.filter(c=>c.id!==data.id);org.items.forEach(x=>{if(x.collectionId===data.id)x.collectionId=null})}
    result=JSON.parse(JSON.stringify(org));
   }
   const deliver=()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result,error})}}));if(action==='organization'&&data.operation==='collection'&&window.delayCollection)window.releaseOrganization=deliver;else queueMicrotask(deliver);
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-tab="saved"]').click();
 await page.locator('[data-organize="1:7"]').click();await page.locator('#newCollection').fill('เรียน');await page.locator('#collectionNote').fill('ทบทวน closures');await page.locator('#collectionViewed').check();await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});
 assert.match(await page.locator('.saved-card-info').textContent(),/เรียน.*ดูแล้ว.*ทบทวน closures/s);
 await page.locator('#search').fill('closures');assert.equal(await page.locator('.saved-post-card').count(),1);await page.locator('#clearSearch').click();
 await page.locator('[data-view-filter="unseen"]').click();assert.equal(await page.locator('.saved-post-card').count(),0);await page.locator('[data-view-filter="seen"]').click();assert.equal(await page.locator('.saved-post-card').count(),1);
 await page.locator('[data-organize="1:7"]').click();await page.locator('#newCollection').fill('ดูวันหยุด');await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});
 await page.locator('[data-collection-filter]').click();await page.locator('[data-choice-value="1"]:visible').click();assert.equal(await page.locator('.saved-post-card').count(),0);await page.locator('[data-collection-filter]').click();await page.locator('[data-choice-value="2"]:visible').click();assert.equal(await page.locator('.saved-post-card').count(),1);
 await page.locator('[data-organize="1:7"]').click();await page.locator('[data-post-source]').click();assert.ok(await page.evaluate(()=>calls.some(c=>c.action==='source'&&c.data.post==='1:7')));
 await page.locator('#collectionNote').fill('keep unsaved draft');await page.evaluate(()=>window.failItem=true);await page.locator('[data-save-organization]').click();await page.waitForTimeout(100);assert.equal(await page.locator('#collectionNote').inputValue(),'keep unsaved draft');assert.equal(await page.locator('#sheet[open]').count(),1);await page.locator('[data-cancel-organization]').click();await page.evaluate(()=>window.failItem=false);
 await page.locator('[data-manage-collections]').click();await page.locator('[data-delete-collection="2"]').click();await page.locator('[data-confirm-delete-collection="2"]').click();await page.locator('#closeSheet').click();assert.match(await page.locator('.saved-card-info').textContent(),/ดูแล้ว.*ทบทวน closures/s);
 await page.locator('[data-manage-collections]').click();await page.locator('[data-rename-collection="1"]').click();await page.locator('#collectionName').fill('เรียน Swift');await page.locator('[data-update-collection="1"]').click();assert.match(await page.locator('#sheetBody').textContent(),/เรียน Swift/);await page.locator('#closeSheet').click();
 await page.locator('[data-view-filter="all"]').click();await page.locator('[data-post="1:7"]').click();await page.locator('#favorite').click();await page.waitForFunction(()=>document.getElementById('favorite').getAttribute('aria-pressed')==='false');await page.locator('#favorite').click();await page.waitForSelector('#editCollection');assert.equal(await page.locator('#collectionNote').inputValue(),'');await page.locator('#editCollection').click();await page.locator('[data-choice-value="1"]:visible').click();await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});await page.locator('#closeViewer').click();assert.match(await page.locator('.saved-card-info').textContent(),/เรียน Swift.*ยังไม่ได้ดู/s);assert.doesNotMatch(await page.locator('.saved-card-info').textContent(),/closures/);
 await page.locator('[data-manage-collections]').click();await page.locator('#collectionName').fill('Delayed');await page.evaluate(()=>window.delayCollection=true);await page.locator('[data-create-collection]').click();await page.locator('#closeSheet').click();await page.locator('[data-organize="1:7"]').click();await page.locator('#collectionNote').fill('new draft');await page.evaluate(()=>window.releaseOrganization());await page.waitForTimeout(100);assert.equal(await page.locator('#collectionNote').inputValue(),'new draft');await page.locator('[data-cancel-organization]').click();
 await page.setViewportSize({width:320,height:568});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 await page.screenshot({path:'/tmp/luma-collections.png'});assert.deepEqual(errors,[]);await browser.close();console.log('PASS: collections, single assignment, notes, viewed/search filters, source, failed save retention and safe category deletion.');
})().catch(e=>{console.error(e);process.exit(1)});
