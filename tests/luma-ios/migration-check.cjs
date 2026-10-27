const {webkit}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await webkit.launch();const page=await browser.newPage({viewport:{width:393,height:759},isMobile:true});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  window.calls=[];
  const post={id:'1:9',group:'1',topic:'2',title:'Photo',text:'Full caption with searchable needle',date:'2026-10-27',media:[{id:'a',kind:'photo',thumb:'assets/placeholder.svg'},{id:'b',kind:'image',thumb:'assets/placeholder.svg'},{id:'c',kind:'video',thumb:'assets/placeholder.svg'}],links:['https://example.com/article']};
  window.HybridWebView={SendRawMessage(raw){const {id,action,data}=JSON.parse(raw);window.calls.push({action,data});let result=null;
   if(action==='start')result={connected:true,account:'123',name:'Migration',favorites:[]};
   if(action==='groups')result=[{id:'1',title:'Desktop forum',isForum:true}];
   if(action==='topics')result=[{id:'2',title:'Design'},{id:'3',title:'Development'}];
   if(action==='history'||action==='search')result={version:data.version,posts:[post],page:data.page||2,knownPages:3,pageSize:data.pageSize||24,loadedCount:54,hasNext:true,query:data.query||'',cached:true,fetchedAt:'2026-10-27T10:00:00Z',busy:false,error:''};
   if(action==='history'&&data.refresh){window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({event:'history',data:{...result,busy:true}})}}));result.error='Offline · showing saved posts';}
   (action==='history'&&data.refresh?fn=>setTimeout(fn,450):queueMicrotask)(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})));
  }};
 });
 await page.goto(process.env.LUMA_URL||'http://127.0.0.1:8765');await page.locator('[data-group="1"]').click();
 await page.waitForSelector('#topicSearch');await page.locator('#topicSearch').fill('Design');assert.equal(await page.locator('[data-topic]:visible').count(),1);
 await page.locator('[data-topic="2"]').click();await page.waitForSelector('[data-post]');
 assert.equal(await page.locator('.message-pagination [aria-current="page"]').textContent(),'2');
 assert.equal(await page.locator('#pageTitle').textContent(),'ข้อความ');
 assert.equal(await page.locator('.large-media-grid').count(),1);
 assert.equal(await page.locator('.media-counts').innerText(),'2 รูป\n1 วิดีโอ');
 const cover=await page.locator('.media-card-cover').boundingBox();assert.ok(cover.width>160&&cover.height>200);
 assert.equal(await page.locator('.message-list').count(),0);
 assert.equal(await page.locator('.group-heading,.history-note').count(),0);
 await page.locator('[aria-label="หน้าถัดไป"]').click();await page.waitForFunction(()=>document.querySelector('.message-pagination [aria-current="page"]')?.textContent==='3');
 await page.locator('[aria-label="หน้าก่อนหน้า"]').click();await page.waitForFunction(()=>document.querySelector('.message-pagination [aria-current="page"]')?.textContent==='2');
 await page.screenshot({path:'/tmp/luma-large-grid.png'});
 assert.equal(await page.locator('#search').isVisible(),false);assert.equal(await page.locator('[data-filter]').count(),0);await page.locator('#options').click();await page.locator('[data-page-size="48"]').click();await page.waitForFunction(()=>window.calls.some(c=>c.action==='history'&&c.data.pageSize===48));
 await page.locator('[data-refresh]').click();await page.waitForSelector('.status-strip.error');assert.equal(await page.locator('[data-post]').count(),1,'Offline refresh keeps cached cards');
 await page.locator('[data-post]').click();await page.locator('#viewerInfo').click();await page.locator('[data-link-index="0"]').click();assert.ok(await page.evaluate(()=>window.calls.some(c=>c.action==='source'&&c.data.linkIndex===0)));
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: desktop topic flow, restored page, compact gallery, 24/48 page size, cached offline refresh, original links.');
})().catch(e=>{console.error(e);process.exit(1)});
