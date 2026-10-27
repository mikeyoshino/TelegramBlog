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
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-tab="saved"]').click();await page.locator('[data-organize="1:7"]').click();
 assert.equal(await page.locator('#editCollection').evaluate(e=>e.tagName),'BUTTON','Category picker must use app UI rather than native select');
 for(const selector of ['#newCollection','#collectionNote'])assert.ok(await page.locator(selector).evaluate(e=>parseFloat(getComputedStyle(e).fontSize)>=16),'Editable fields must not trigger small-text focus zoom');
 await page.locator('#newCollection').fill('หมวดทดสอบ');await page.locator('#collectionNote').fill('โน้ตที่ต้องคงอยู่');await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});await page.locator('[data-organize="1:7"]').click();
 await page.locator('#collectionNote').fill('draft');await page.locator('#editCollection').click();await page.screenshot({path:'/tmp/teleblog-collection-picker.png'});await page.locator('[data-choice-value=""]:visible').click();assert.equal(await page.locator('#collectionNote').inputValue(),'draft');assert.equal(await page.locator('#editCollection').getAttribute('aria-expanded'),'false');
 await page.locator('#editCollection').click();await page.keyboard.press('Escape');assert.equal(await page.locator('#sheet[open]').count(),1);assert.equal(await page.locator('#editCollection').getAttribute('aria-expanded'),'false');
 await page.locator('#editCollection').click();await page.locator('[data-choice-value="1"]:visible').click();await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});assert.ok(await page.evaluate(()=>calls.some(c=>c.action==='organization'&&c.data.operation==='item'&&c.data.collectionId==='1'&&c.data.note==='draft')));
 await page.locator('[data-collection-filter]').click();await page.locator('[data-choice-value="none"]:visible').click();assert.equal(await page.locator('.saved-post-card').count(),0);await page.locator('[data-collection-filter]').click();await page.locator('[data-choice-value="all"]:visible').click();
 await page.locator('[data-organize="1:7"]').click();await page.setViewportSize({width:320,height:360});await page.locator('#newCollection').focus();assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);const sheet=await page.locator('#sheet').boundingBox();assert.ok(sheet.x>=0&&sheet.width<=320);await page.locator('[data-save-organization]').scrollIntoViewIfNeeded();assert.ok(await page.locator('[data-save-organization]').isVisible());
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: app category picker, preserved draft, Escape, filter/save and narrow form with zoom-safe text.');
})().catch(e=>{console.error(e);process.exit(1)});
