// Run against a local server with Playwright installed (see docs/IOS-HANDOFF.md).
const { webkit } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const url = process.env.LUMA_URL || 'http://127.0.0.1:8765';
const screenshots = process.env.LUMA_SCREENSHOTS;
(async () => {
 const browser = await webkit.launch();
 const page = await browser.newPage({viewport:{width:393,height:759},deviceScaleFactor:2,isMobile:true,hasTouch:true});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 async function capture(name){if(screenshots){fs.mkdirSync(screenshots,{recursive:true});await page.screenshot({path:`${screenshots}/${name}.png`,animations:'disabled'})}}
 await page.goto(url+'/?preview');
 await page.waitForSelector('[data-post]');
 assert.equal(await page.locator('[data-post]').count(),24);
 await capture('library');
 await page.locator('[data-post="1"]').click();
 await page.waitForSelector('#viewer[open]');
 await page.locator('#viewer').evaluate(el=>el.getAnimations().forEach(a=>a.finish()));
 assert.equal(await page.locator('#viewerStage .main-media').count(),1);
 await capture('viewer');assert.equal(await page.locator('#filmstrip [data-media-index]').count(),3,'Only selected message attachments belong in the strip');
 await page.locator('#favorite').click();
 assert.equal(await page.locator('#favorite').getAttribute('aria-pressed'),'true');
 await page.locator('#nextMedia').click();
 assert.equal(await page.locator('#viewerCount').textContent(),'2 / 3');
 await page.locator('#closeViewer').click();
 assert.equal(await page.locator('#viewerStage .main-media').count(),0);
 await page.locator('[data-post="2"]').click();assert.equal(await page.locator('#viewerCount').textContent(),'1 / 1');assert.equal(await page.locator('#filmstrip').isVisible(),false);assert.equal(await page.locator('#nextMedia').isDisabled(),true);await page.locator('#closeViewer').click();
 await page.locator('[data-tab="saved"]').click();await page.locator('#options').click();await page.locator('[data-density="4"]').click();
 assert.match(await page.locator('.photo-grid').first().getAttribute('style'),/4/);
 await capture('options');await page.locator('#closeSheet').click();
 await page.locator('[data-tab="groups"]').click();assert.equal(await page.locator('.group-card').count(),3);await capture('groups');
 await page.locator('#search').fill('no match');assert.equal(await page.locator('.group-card').count(),0);await page.locator('#search').fill('');
 await page.locator('[data-group="travel"]').click();assert.equal(await page.locator('#search').isVisible(),false);assert.equal(await page.locator('[data-filter]').count(),0);
 await page.locator('[data-tab="saved"]').click();assert.equal(await page.locator('[data-post]').count(),1);
 await page.reload();await page.waitForSelector('[data-post]');await page.locator('[data-tab="saved"]').click();assert.equal(await page.locator('[data-post]').count(),1);
 for(const [width,height] of [[320,568],[852,393]]){await page.setViewportSize({width,height});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);await capture(`${width}x${height}`)}
 // A new signed-in account must never inherit preview favorites or fixture photos.
 await page.setViewportSize({width:393,height:759});
 await page.addInitScript(() => {
  window.HybridWebView = {SendRawMessage(raw){const {id,action}=JSON.parse(raw);const result=action==='start'?{connected:true,account:'123',name:'Test account',favorites:[]}:action==='groups'?[{id:'9',title:'Real fixture group',isForum:false}]:null;queueMicrotask(()=>window.dispatchEvent(new CustomEvent('HybridWebViewMessageReceived',{detail:{message:JSON.stringify({id,result})}})))}};
 });
 await page.goto(url);await page.waitForSelector('[data-group="9"]');assert.equal(await page.locator('.group-card').count(),1);
 await page.locator('[data-tab="saved"]').click();assert.equal(await page.locator('[data-post]').count(),0);
 assert.deepEqual(errors,[]);await browser.close();console.log('PASS: preview navigation, gallery cleanup, favorites persistence, group search, density, compact/landscape layout, account isolation.');
})().catch(error=>{console.error(error);process.exit(1)});
