const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  const realNow=Date.now;window.timeOffset=0;Date.now=()=>realNow()+window.timeOffset;
  const posts=[1,2,3,4,5].map(n=>({id:'1:'+n,group:'1',topic:'0',title:'Post '+n,date:'2026-10-27',media:[{id:'1:'+n,kind:'photo',thumb:'assets/placeholder.svg'}]}));
  let org=JSON.parse(localStorage.getItem('test-org')||'{"collections":[],"items":[]}'),saved=JSON.parse(localStorage.getItem('test-saved')||'[]');window.calls=[];
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Save',favorites:posts.filter(p=>saved.includes(p.id))};
   if(action==='groups')result=[{id:'1',title:'Group',isForum:false}];if(action==='topics')result=[{id:'0',title:'All'}];if(action==='history')result={version:data.version,posts,page:1,knownPages:1,hasNext:false};
   if(action==='original')result='assets/placeholder.svg';
   if(action==='favorite'){for(const c of data.changes){if(c.saved){saved.push(c.id);if(data.collectionId)org.items.push({postId:c.id,collectionId:data.collectionId,note:'',viewed:false})}else{saved=saved.filter(x=>x!==c.id);org.items=org.items.filter(x=>x.postId!==c.id)}}result=posts.filter(p=>saved.includes(p.id));}
   if(action==='organization'){
    if(data.operation==='collection')org.collections.push({id:'c'+(org.collections.length+1),name:data.name});
    if(data.operation==='item'){org.items=org.items.filter(x=>x.postId!==data.post);org.items.push({postId:data.post,collectionId:data.collectionId,note:data.note,viewed:data.viewed})}
    if(data.operation==='delete'){org.collections=org.collections.filter(x=>x.id!==data.id);org.items.forEach(x=>{if(x.collectionId===data.id)x.collectionId=null})}
    result=JSON.parse(JSON.stringify(org));
   }
   localStorage.setItem('test-org',JSON.stringify(org));localStorage.setItem('test-saved',JSON.stringify(saved));queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 const open=async id=>{await page.locator(`[data-post="1:${id}"]`).click();await page.locator('#favorite').click()};
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();await open(1);await page.waitForSelector('#newCollection');
 assert.equal(await page.evaluate(()=>calls.filter(c=>c.action==='favorite').length),0,'Ask before persisting');
 await page.locator('[data-cancel-organization]').click();assert.equal(await page.locator('#favorite').getAttribute('aria-pressed'),'false');await page.locator('#favorite').click();await page.locator('#newCollection').fill('Learning');await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});await page.locator('#closeViewer').click();
 await open(2);await page.waitForFunction(()=>document.getElementById('favorite').getAttribute('aria-pressed')==='true');assert.equal(await page.locator('#sheet[open]').count(),0);assert.equal(await page.evaluate(()=>calls.filter(c=>c.action==='favorite').at(-1).data.collectionId),'c1');await page.locator('#closeViewer').click();
 await page.evaluate(()=>window.timeOffset=31*60*1000);await open(3);await page.waitForSelector('#editCollection');await page.locator('#editCollection').click();await page.locator('[data-choice-value="c1"]:visible').click();await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});await page.locator('#closeViewer').click();
 await page.reload();await page.locator('[data-group="1"]').click();await open(4);await page.waitForSelector('#editCollection');assert.equal(await page.evaluate(()=>calls.filter(c=>c.action==='favorite').length),0,'Restart asks even with persisted categories');
 await page.locator('[data-cancel-organization]').click();await page.locator('#closeViewer').click();await page.locator('#selectButton').click();await page.locator('[data-post="1:4"]').click();await page.locator('[data-post="1:5"]').click();await page.locator('#saveSelection').click();await page.waitForSelector('#editCollection');await page.locator('#editCollection').click();await page.locator('[data-choice-value="c1"]:visible').click();await page.locator('[data-save-organization]').click();await page.waitForSelector('#sheet:not([open])',{state:'attached'});assert.deepEqual(await page.evaluate(()=>calls.filter(c=>c.action==='favorite').at(-1).data.changes.map(x=>x.id).sort()),['1:4','1:5']);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: first save prompts, cancel writes nothing, create inline, next save uses category, idle expiry and restart prompt.');
})().catch(e=>{console.error(e);process.exit(1)});
