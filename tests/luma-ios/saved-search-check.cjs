const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];const favorites=Array.from({length:60},(_,i)=>({id:'8:'+i,group:'8',topic:'0',groupTitle:i===59?'กลุ่มนักพัฒนา':'ทั่วไป',topicTitle:i===59?'Login':'ข่าว',title:i===59?'บทเรียน Swift':'โพสต์ '+i,text:i===59?'ตัวอย่าง C++ <img src=x onerror=alert(1)>':'เนื้อหา',date:'2026-10-27',media:[{id:'8:'+i,kind:'photo',thumb:'assets/placeholder.svg'}]}));
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);calls.push({action,data});let result=null;
   if(action==='start')result={connected:true,account:'1',name:'Search',favorites};
   if(action==='groups')result=[{id:'9',title:'Different group',isForum:false}];
   if(action==='organization')result={collections:[{id:'c',name:'เรียนวันหยุด'}],items:[{postId:'8:59',collectionId:'c',note:'ต้นเรื่อง '.repeat(70)+'closures สำคัญ',viewed:true}]};
   if(action==='original')result='assets/placeholder.svg';
   queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-tab="saved"]').click();await page.waitForFunction(()=>document.querySelectorAll('.saved-post-card').length===60);
 await page.locator('#search').fill('นักพัฒนา Login');assert.equal(await page.locator('.saved-post-card').count(),1,'Search includes saved groups/topics beyond Telegram history page');assert.equal(await page.locator('[data-saved-card="8:59"]').count(),1);assert.ok(await page.locator('.saved-post-card mark').count()>0);
 await page.locator('#search').fill('SWIFT วันหยุด');assert.equal(await page.locator('.saved-post-card').count(),1);
 await page.locator('#search').fill('closures');assert.match(await page.locator('.saved-post-card mark').textContent(),/closures/);await page.locator('[data-post="8:59"]').first().click();await page.waitForSelector('#viewer[open]');await page.locator('#sourceInfo').click();assert.ok(await page.evaluate(()=>calls.some(c=>c.action==='source'&&c.data.post==='8:59')));await page.locator('#closeViewer').click();
 await page.locator('#search').fill('C++');assert.equal(await page.locator('.saved-post-card').count(),1);assert.match(await page.locator('.saved-search-summary').textContent(),/<img/);assert.equal(await page.locator('.saved-search-summary img').count(),0);
 await page.locator('[data-view-filter="unseen"]').click();assert.equal(await page.locator('.saved-post-card').count(),0);await page.locator('[data-view-filter="all"]').click();assert.equal(await page.locator('.saved-post-card').count(),1);
 await page.locator('#search').fill('ไม่มีคำนี้แน่นอน');assert.equal(await page.locator('.saved-post-card').count(),0);await page.locator('#clearSearch').click();assert.equal(await page.locator('.saved-post-card').count(),60);
 await page.locator('#search').fill('closures');await page.screenshot({path:'/tmp/teleblog-saved-search.png'});assert.equal(await page.evaluate(()=>calls.some(c=>['history','search'].includes(c.action))),false,'Saved search must not request Telegram history/search');
 await page.reload();await page.locator('[data-tab="saved"]').click();await page.locator('#search').fill('นักพัฒนา');assert.equal(await page.locator('.saved-post-card').count(),1);assert.deepEqual(errors,[]);await browser.close();console.log('PASS: all saved records, group/topic/text/note/category search, highlighting, safe HTML, viewer/source, filters and reopen.');
})().catch(e=>{console.error(e);process.exit(1)});
