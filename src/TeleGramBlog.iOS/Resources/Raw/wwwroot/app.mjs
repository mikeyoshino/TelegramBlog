import {dismissOnBackdrop} from './modal-dismiss.mjs';
import {installSavedContextMenu} from './saved-context-menu.mjs';
import {createCollections} from './collections.mjs';
import {filterPosts, RequestEpoch} from './model.mjs';
import {request} from './bridge.mjs';
const safe = value => String(value ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const state = {tab:'library',group:'all',topic:'all',query:'',type:'all',columns:2,libraryColumns:1,sort:'new',selecting:false,selected:new Set(),saved:new Set()};
try{const layout=Number(localStorage.getItem('luma-library-columns'));if(layout===1||layout===2)state.libraryColumns=layout}catch{}
let groups=[], posts=[], favorites=[], topics=[], account=null, preview=false, busy=false, page=1, hasNext=false;
let starting=!!window.HybridWebView,restorePending=false,restoreMessage='';
let appSuspended=false,recoveringConnection=false,recoveryQueued=false,networkRecoveryNeeded=false,connectionGeneration=0;
let pendingNavigation=null;
let pageSize=24,knownPages=1,historyMessage='',searchTimer,qrOpen=false;
let inlineVideo=false,videoStarting=false,videoAttempt=0,videoExpandRequested=false;
let viewer=[], viewerIndex=0, viewerEpoch=0, toastTimer, lastTile, retry=null;
const epoch=new RequestEpoch();
const authEpoch=new RequestEpoch();
let loginStep='phone_number',logoutPending=false;
const thumbnails=new Map(),avatars=new Map(),originalImages=new Map();
let originalImageBytes=0;
let groupsCheckedAt=null,groupsStale=true,groupsRevision=0;
let unreadRefresh=null;
let refreshingGroups=false,avatarGeneration=0,savingFavorites=false;
const status=document.createElement('div');status.className='status-strip';status.hidden=true;$('header').after(status);
const dateFormat=new Intl.DateTimeFormat('th-TH',{day:'numeric',month:'long',year:'numeric'});
const dateKey=p=>new Date(p.date).toLocaleDateString('en-CA');
function hydrate(root=document){root.querySelectorAll('[data-icon]').forEach(e=>e.innerHTML=icon(e.dataset.icon))}
function toast(text){$('toast').textContent=text;$('toast').classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('visible'),3500)}
function notice(text,error=false,action=''){status.hidden=!text;status.classList.toggle('error',error);status.classList.toggle('quiet',!!text&&!error&&!action);status.innerHTML=`<span role="status">${safe(text)}</span>${action?`<button data-status="${action}">${action==='cancel'?'ยกเลิก':action==='retry'||action==='cleanup'?'ลองใหม่':'เชื่อมต่อ'}</button>`:''}`}
function baseNotice(){if(logoutPending){notice('ล้างข้อมูลไม่สำเร็จ',true,'cleanup');return}if(preview){notice('โหมดตัวอย่าง',false,'connect');return}notice('');if(busy){status.hidden=false;status.innerHTML=`<span class="media-spinner" role="status" aria-label="กำลังโหลด"></span><button data-status="cancel" aria-label="ยกเลิกการโหลด">${icon('close')}</button>`}}

const collections=createCollections({request,safe,showSheet,closeSheet,render,toast,posts:()=>[...allPosts(),...viewer.map(x=>x.post)],isSaved:id=>state.saved.has(id),save:(ids,collectionId)=>saveChanges(ids.map(id=>({id,saved:true})),collectionId),source:id=>request('source',{post:id}),isPreview:()=>preview});
const savedContext=installSavedContextMenu({
 enabled:()=>['saved','library'].includes(state.tab)&&!state.selecting&&!$('sheet').open&&!$('viewer').open&&!savingFavorites,
 items:async id=>{if(!allPosts().some(p=>p.id===id))return [];if(!state.saved.has(id))return [{id:'open',label:'เปิดดูข้อความ'},{id:'collection',label:'เก็บเข้าคอลเลกชัน'},{id:'source',label:'ไปข้อความต้นทาง'}];await collections.load({refresh:false});if(!collections.ready())return [];return [
  {id:'open',label:'เปิดดูข้อความ'},
  {id:'collection',label:'ย้ายคอลเลกชัน'},
  {id:'note',label:'แก้ไขโน้ต'},
  {id:'viewed',label:collections.detail(id).viewed?'ทำเครื่องหมายว่ายังไม่ได้ดู':'ทำเครื่องหมายว่าดูแล้ว'},
  {id:'source',label:'ไปข้อความต้นทาง'},
  {id:'remove',label:'ลบออกจากที่เก็บไว้',destructive:true}
 ]},
 run:async(action,id)=>{try{
  if(!allPosts().some(p=>p.id===id))return;
  if(action==='open'){openViewer(id,null);return}
  if(action==='collection'||action==='note'){await collections.edit(id);if(action==='note')$('collectionNote')?.focus();return}
  if(action==='viewed'&&state.saved.has(id)){await collections.toggleViewed(id);return}
  if(action==='source'){await request('source',{post:id});return}
  if(action==='remove'&&state.saved.has(id))showSheet('ลบออกจากที่เก็บไว้?',`<p class="sheet-subtitle">นำรายการนี้พร้อมโน้ตและไฟล์ที่เก็บไว้ในแอปออก ข้อความต้นทางใน Telegram ยังอยู่</p><button class="blue-button destructive" data-confirm-remove-saved="${safe(id)}">ลบออกจากที่เก็บไว้</button><button class="plain-action" data-cancel-organization>ยกเลิก</button>`);
 }catch(e){toast(e.message)}}
});
document.addEventListener('change',e=>collections.change(e.target));
function allPosts(){return [...new Map([...favorites,...posts].map(p=>[p.id,p])).values()]}
function visible(){const list=filterPosts(state.tab==='saved'?favorites:posts,{...state,...(state.tab==='library'?{sort:'new',mediaOnly:true}:state.tab==='saved'?{query:''}:{})});return state.tab==='saved'?collections.filter(list,state.query):list}
function groupName(id){return groups.find(g=>g.id===id)?.title||'Telegram'}
function showSheet(title,body){if(inlineVideo||videoStarting)stopInlineVideo();$('sheetTitle').textContent=title;$('sheetBody').innerHTML=body;if(!$('sheet').open)$('sheet').showModal()}
function closeSheet(){if($('sheet').open)$('sheet').close()}
function cancelSheet(){authEpoch.next();if(qrOpen){qrOpen=false;request('authCancel').catch(()=>{})}closeSheet()}
dismissOnBackdrop($('sheet'),cancelSheet);
dismissOnBackdrop($('viewer'),closeViewer);
function row(text,attr,sub='',symbol='folder',selected=false){return `<button class="sheet-row ${selected?'selected-row':''}" ${attr}><span class="row-icon">${icon(symbol)}</span><span class="text">${safe(text)}${sub?`<small>${safe(sub)}</small>`:''}</span>${selected?icon('check','class="check"'):icon('chevronRight','class="check"')}</button>`}
function reset(){groupsRevision++;unreadRefresh=null;savedContext.close();groupsCheckedAt=null;groupsStale=true;collections.reset();originalImages.clear();originalImageBytes=0;pendingNavigation=null;clearThumbnailWork();starting=false;restorePending=false;clearTimeout(searchTimer);authEpoch.next();epoch.next();viewerEpoch++;closeViewer();groups=[];posts=[];favorites=[];topics=[];account=null;preview=false;busy=false;hasNext=false;page=1;thumbnails.clear();avatars.clear();avatarGeneration++;Object.assign(state,{tab:'library',group:'all',topic:'all',query:'',type:'all',selecting:false,selected:new Set(),saved:new Set()});closeSheet()}
function header(){
 $('app').classList.toggle('boot-loading',starting&&!account&&!preview);
 const g=state.tab==='groups';$('pageTitle').textContent=!account&&!preview?'TeleBlog':g?'กลุ่ม':state.tab==='saved'?'เก็บไว้':'ข้อความ';
 $('profile').hidden=!account&&!preview;$('profile').textContent=preview?'L':(account?.name||'L').slice(0,1);
 $('selectButton').hidden=g||!account&&!preview;$('options').hidden=g||!account&&!preview;
 $('selectButton').textContent=state.selecting?'ยกเลิก':'เลือก';
 for(const id of ['groupPicker','topicPicker','scopeDot'])$(id).hidden=state.tab!=='library'||!account&&!preview;
 $('pageSubtitle').hidden=true;$('pageSubtitle').textContent='';$('scopeRow').hidden=state.tab!=='library'||!account&&!preview;$('refreshGroupsButton').hidden=!g||!account||preview;

 $('groupPicker').innerHTML=safe(state.group==='all'?'ทุกกลุ่ม':groupName(state.group))+icon('chevronDown');
 $('groupPicker').classList.toggle('group-selected',state.group!=='all');
 $('topicPicker').innerHTML=safe(state.topic==='all'?'ทุกหัวข้อ':topics.find(t=>t.id===state.topic)?.title||state.topic)+icon('chevronDown');
 $('search').placeholder=g?'ค้นหาชื่อกลุ่ม':preview?'ค้นหารูป กลุ่ม หรือหัวข้อ':'ค้นหาในรายการที่โหลดแล้ว';
 $('search').disabled=busy&&!preview&&state.tab!=='saved';$('search').value=state.query;$('clearSearch').hidden=!state.query;$('search').parentElement.hidden=state.tab==='library'||!account&&!preview;if(state.tab==='saved')$('search').placeholder='ค้นหาในรายการที่เก็บไว้';
 document.querySelectorAll('[data-tab]').forEach(el=>{el.classList.toggle('active',el.dataset.tab===state.tab);if(el.dataset.tab===state.tab)el.setAttribute('aria-current','page');else el.removeAttribute('aria-current')});
 $('selectionBar').hidden=!state.selecting;document.querySelector('.tabbar').hidden=state.selecting||!account&&!preview;
 $('selectionCount').textContent=`เลือก ${state.selected.size} รายการ`;$('saveSelection').disabled=savingFavorites||!state.selected.size;
}
function imageMarkup(p,index=0,extra=''){
 const m=p.media[index];if(!m)return `<span class="tile-placeholder">${icon('location')}</span>`;
 const src=m.thumb||thumbnails.get(m.id);
 return `<img class="${src?'':'thumb-loading'}" ${src?`src="${safe(src)}"`:'src="assets/placeholder.svg"'} data-thumb-post="${safe(p.id)}" data-thumb-index="${index}" alt="${safe(p.title)}" loading="lazy" ${extra}>`;
}
function tile(p){return `<button class="tile ${state.selected.has(p.id)?'selected':''}" data-post="${safe(p.id)}" aria-label="${safe(p.title)}" ${state.selecting?`aria-pressed="${state.selected.has(p.id)}"`:''}>${imageMarkup(p)}${p.media.length>1?`<span class="tile-badge">${icon('stack')}${p.media.length}</span>`:p.media[0]?.kind==='video'?`<span class="tile-badge video">${icon('play')}</span>`:''}${state.saved.has(p.id)?`<span class="tiny-heart">${icon('heart')}</span>`:''}${state.selecting?`<span class="select-check">${state.selected.has(p.id)?icon('check'):''}</span>`:''}</button>`}
function mediaCard(p){
 const photos=p.media.filter(m=>(m.kind==='image'||m.kind==='photo')).length,videos=p.media.filter(m=>m.kind==='video').length;
 const cover=Math.max(0,p.media.findIndex(m=>(m.kind==='image'||m.kind==='photo')));
 return `<button class="media-card ${state.selected.has(p.id)?'selected':''}" data-post="${safe(p.id)}" aria-label="${safe(p.title)} · ${photos} รูป · ${videos} วิดีโอ" ${state.selecting?`aria-pressed="${state.selected.has(p.id)}"`:''}><span class="media-card-cover">${p.media.length?imageMarkup(p,cover):`<span class="media-link">${icon('location')}<span>${safe(p.text||p.title)}</span></span>`}${!photos&&videos?`<span class="media-play">${icon('play')}</span>`:''}${state.saved.has(p.id)?`<span class="tiny-heart">${icon('heart')}</span>`:''}${state.selecting?`<span class="select-check">${state.selected.has(p.id)?icon('check'):''}</span>`:''}</span><span class="media-counts"><span>${icon('photo')}${photos} รูป</span><span>${icon('play')}${videos} วิดีโอ</span></span></button>`;
}
function singleRowCard(p){
 const shown=Math.min(5,p.media.length),extra=p.media.length-shown;
 const previews=p.media.length>1?`<div class="post-thumbnails" aria-label="ไฟล์แนบของข้อความนี้">${p.media.slice(0,shown).map((m,index)=>`<button class="post-thumb" data-open-post="${safe(p.id)}" data-open-index="${index}" aria-label="${safe(p.title)} ไฟล์ที่ ${index+1}${index===shown-1&&extra>0?` และอีก ${extra} รายการ`:''}">${imageMarkup(p,index)}${m.kind==='video'?`<span class="post-thumb-video">${icon('play')}</span>`:''}${index===shown-1&&extra>0?`<span class="post-overflow">+${extra}</span>`:''}</button>`).join('')}</div>`:'';
 return `<article class="media-post" data-post-card="${safe(p.id)}">${mediaCard(p)}${previews}<button class="plain-action card-organize" data-organize="${safe(p.id)}">เก็บเข้าคอลเลกชัน</button></article>`;
}
function pagination(){
 const start=Math.max(1,page-2),end=Math.min(knownPages,page+2);
 return `<nav class="message-pagination" aria-label="หน้าโพสต์"><button data-page="${page-1}" ${page<=1||busy?'disabled':''} aria-label="หน้าก่อนหน้า">${icon('chevronLeft')}</button>${Array.from({length:Math.max(0,end-start+1)},(_,i)=>start+i).map(n=>`<button data-page="${n}" ${busy?'disabled':''} ${n===page?'aria-current="page"':''}>${n}</button>`).join('')}<button data-page="${page+1}" ${!hasNext||busy?'disabled':''} aria-label="หน้าถัดไป">${icon('chevronRight')}</button></nav>`;
}
function welcome(){
 if(starting)return '<div class="startup-loader" role="status" aria-label="กำลังเชื่อมต่อบัญชีเดิม"><div class="startup-mark"><span class="startup-track" aria-hidden="true"></span><span class="startup-orbit" aria-hidden="true"></span><img class="startup-logo" src="assets/teleblog-logo.svg" alt="TeleBlog" width="88" height="88"></div></div>';
 if(restorePending)return `<div class="welcome"><span class="brand-icon">${icon('luma')}</span><h2>กลับเข้าบัญชีเดิม</h2><p role="status">เชื่อมต่อไม่สำเร็จ</p><button class="blue-button" data-resume>ลองเชื่อมต่ออีกครั้ง</button><button class="plain-action" data-reconnect>เชื่อมต่อใหม่ด้วย QR หรือเบอร์โทร</button></div>`;
 return `<div class="welcome login-welcome"><span class="brand-icon">${icon('luma')}</span><h2>เข้าสู่ระบบ Telegram</h2><div class="login-methods"><button class="blue-button" data-qr>เข้าสู่ระบบด้วย QR Code</button><button class="secondary-button" data-connect>เข้าสู่ระบบด้วยเบอร์โทรศัพท์</button></div></div>`}
function renderContent(){
 const area=$('content');observer.disconnect();avatarObserver.disconnect();
 if(!account&&!preview){area.innerHTML=welcome();return}
 if(state.tab==='groups'){
  const list=groups.filter(g=>g.title.toLocaleLowerCase().includes(state.query.trim().toLocaleLowerCase()));
  area.innerHTML=list.map(g=>`<button class="group-card" data-group="${safe(g.id)}"><div class="group-card-info"><span class="group-avatar group-initial" data-group-avatar="${safe(g.id)}">${avatars.has(g.id)?`<img src="${safe(avatars.get(g.id))}" alt="">`:safe(g.title.slice(0,1))}</span><div class="group-description"><h3>${safe(g.title)}</h3></div><span class="group-activity">${unreadMarkup(g)}</span></div></button>`).join('');
  if(!list.length)area.innerHTML+=busy?'<div class="groups-loading" role="status" aria-label="กำลังโหลดกลุ่ม"><span class="media-spinner"></span></div>':empty('ไม่พบกลุ่ม','');if(!preview)area.querySelectorAll('[data-group-avatar]').forEach(el=>avatarObserver.observe(el));return;
 }
 const list=visible();
 if(!list.length){area.innerHTML=busy?`<div class="skeleton-grid" aria-label="กำลังโหลด">${Array(12).fill('<i class="skeleton"></i>').join('')}</div>`:empty(state.tab==='saved'?(state.saved.size?'ไม่พบรายการที่ตรงกับตัวกรอง':'ยังไม่มีรายการที่เก็บไว้'):'ยังไม่มีข้อความ',state.tab==='saved'?(state.saved.size?'ลองเปลี่ยนหมวด สถานะ หรือคำค้นหา':'เปิดรูปแล้วแตะหัวใจ กลับมาดูได้จากหน้านี้'):!posts.length?'เลือกกลุ่มและหัวข้อเพื่ออ่านข้อความ':'ลองโหลดหน้าถัดไป หรือเลือกหัวข้ออื่น');}
 else {
 let html='';
 if(state.tab==='library')html+=`<div class="large-media-grid ${state.libraryColumns===1?'single-column':'two-columns'}">${list.map(state.libraryColumns===1?singleRowCard:mediaCard).join('')}</div>`;
 else {
 html+=`<div class="saved-cards photo-grid" style="--columns:${state.columns}">${list.map(p=>`<article class="saved-post-card" data-saved-card="${safe(p.id)}">${p.media.length?tile(p):`<button class="saved-text-card" data-post="${safe(p.id)}">${safe(p.text||p.title)}</button>`}${collections.caption(p,state.query)}</article>`).join('')}</div>`;

 }
 area.innerHTML=html;
 }
 if(state.tab==='saved'){area.insertAdjacentHTML('afterbegin',collections.toolbar());}
 if(!preview&&state.tab==='library'&&state.group!=='all'&&state.topic!=='all')area.innerHTML+=pagination()+`<button class="plain-action load-more" data-refresh ${busy?'disabled':''}>รีเฟรชโพสต์</button>`;
 if(!busy||preview)area.querySelectorAll('[data-thumb-post]').forEach(img=>observer.observe(img));
}
function empty(title,description){const action=state.tab==='library'||!!state.query||state.type!=='all'||state.tab==='saved'&&state.saved.size>0;return `<div class="empty-state"><div class="empty-icon">${icon(state.tab==='saved'?'heart':'search')}</div><h3>${title}</h3>${action?`<button class="blue-button" data-reset>${state.tab==='saved'||state.query||state.type!=='all'?'ล้างตัวกรอง':'เลือกกลุ่ม'}</button>`:''}</div>`}

function render(resetScroll=false){savedContext.close();header();renderContent();if(resetScroll)$('content').scrollTop=0}
let thumbQueue=[],thumbActive=0,thumbnailGeneration=0;
const thumbnailJobs=new Map();
const avatarObserver=new IntersectionObserver(entries=>{for(const e of entries)if(e.isIntersecting){avatarObserver.unobserve(e.target);const el=e.target,id=el.dataset.groupAvatar; if(avatars.has(id))continue;const currentAccount=account,generation=avatarGeneration;request('avatar',{group:id}).then(src=>{if(src&&account===currentAccount&&generation===avatarGeneration){avatars.set(id,src);if(el.isConnected)el.innerHTML=`<img src="${safe(src)}" alt="">`}}).catch(()=>{})}},{root:$('content')});
function unreadMarkup(g){
 if(groupsStale||!Number.isInteger(g.unreadCount)||g.unreadCount<=0)return '';
 return `<span class="unread-badge" aria-label="${g.unreadCount} ข้อความที่ยังไม่อ่าน">${g.unreadCount.toLocaleString('th-TH')}</span>`;
}
function refreshUnread(){
 if(preview||!account||!groups.some(g=>g.isForum&&g.unreadCount==null))return;
 const currentAccount=account,revision=groupsRevision,generation=connectionGeneration;
 const task=request('groupUnread').then(counts=>{
  if(account!==currentAccount||revision!==groupsRevision||generation!==connectionGeneration||!Array.isArray(counts))return;
  const lookup=new Map(counts.map(c=>[c.id,c.unreadCount]));
  groups=groups.map(g=>lookup.has(g.id)?{...g,unreadCount:lookup.get(g.id)}:g);
  groupsCheckedAt=new Date();if(state.tab==='groups')renderContent();
 }).catch(()=>{/* Counts are optional: unknown badges must never block browsing. */}).finally(()=>{if(unreadRefresh===task)unreadRefresh=null});
 unreadRefresh=task;
}
async function refreshGroups(){
 if(preview||!account||refreshingGroups||state.tab!=='groups')return;
 const currentAccount=account,generation=connectionGeneration;refreshingGroups=true;groupsStale=true;renderContent();
 try{const updated=await request('groups',{refresh:true});if(account!==currentAccount||generation!==connectionGeneration)return;groups=updated;groupsRevision++;refreshUnread();groupsCheckedAt=new Date();groupsStale=false;avatars.clear();avatarGeneration++;if(state.tab==='groups'){baseNotice();renderContent()}return true}
 catch(e){if(account===currentAccount&&state.tab==='groups'){retry=recoverForeground;networkRecoveryNeeded=true;notice('โหลดกลุ่มไม่สำเร็จ',true,'retry')}return false}
 finally{refreshingGroups=false;if(account===currentAccount&&state.tab==='groups')renderContent()}
}
setInterval(()=>{if(!document.hidden&&!$('sheet').open)refreshGroups()},45000);

function clearThumbnailWork(){
 thumbnailGeneration++;
 const pending=thumbQueue;thumbQueue=[];thumbnailJobs.clear();
 for(const job of pending)job.reject(new Error('ยกเลิกการโหลดแล้ว'));
}
function loadThumbElement(img,priority=false){
 const generation=thumbnailGeneration;
 const p=allPosts().find(p=>p.id===img.dataset.thumbPost),i=Number(img.dataset.thumbIndex);
 if(!p)return;
 getThumbnail(p,i,priority).then(src=>{if(!img.isConnected||generation!==thumbnailGeneration)return;img.classList.remove('thumb-loading');if(src){if(img.getAttribute('src')!==src)img.src=src}else{img.classList.add('thumb-unavailable');img.alt='ไม่มีภาพตัวอย่าง'}}).catch(()=>{if(img.isConnected&&generation===thumbnailGeneration){img.classList.remove('thumb-loading');img.classList.add('thumb-unavailable');img.alt='โหลดภาพไม่ได้ แตะเพื่อเปิดและลองใหม่'}});
}
const observer=new IntersectionObserver(entries=>{for(const e of entries)if(e.isIntersecting){observer.unobserve(e.target);loadThumbElement(e.target)}},{root:$('content'),rootMargin:'100px'});
const filmstripObserver=new IntersectionObserver(entries=>{for(const e of entries)if(e.isIntersecting){filmstripObserver.unobserve(e.target);loadThumbElement(e.target,true)}},{root:$('filmstrip'),rootMargin:'0px 100px'});
function getThumbnail(post,index,priority=false){
 const m=post.media[index];if(!m)return Promise.resolve(null);
 if(m.thumb||thumbnails.has(m.id))return Promise.resolve(m.thumb||thumbnails.get(m.id));
 const previous=thumbnailJobs.get(m.id);
 if(previous&&previous.account===account&&previous.generation===thumbnailGeneration){if(priority){previous.priority=true;drainThumbs()}return previous.promise}
 let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b});
 const job={post,index,id:m.id,account,generation:thumbnailGeneration,priority,promise,resolve,reject};thumbnailJobs.set(m.id,job);
 if(priority)thumbQueue.unshift(job);else thumbQueue.push(job);drainThumbs();return promise;
}
function drainThumbs(){
 if(appSuspended)return;
 while(thumbActive<3&&thumbQueue.length){
  const index=$('viewer').open?thumbQueue.findIndex(job=>job.priority):0;if(index<0)return;
  const job=thumbQueue.splice(index,1)[0];
  if(job.account!==account||job.generation!==thumbnailGeneration){if(thumbnailJobs.get(job.id)===job)thumbnailJobs.delete(job.id);job.reject(new Error('ยกเลิกการโหลดแล้ว'));continue}
  thumbActive++;
  request('thumbnail',{post:job.post.id,index:job.index}).then(src=>{
   if(src&&job.account===account&&job.generation===thumbnailGeneration){if(thumbnails.size>=150)thumbnails.delete(thumbnails.keys().next().value);thumbnails.set(job.id,src)}
   job.resolve(src);
  },job.reject).finally(()=>{thumbActive--;if(thumbnailJobs.get(job.id)===job)thumbnailJobs.delete(job.id);drainThumbs()});
 }
}
async function getOriginal(post,index){
 const id=post.media[index].id;
 if(originalImages.has(id)){const src=originalImages.get(id);originalImages.delete(id);originalImages.set(id,src);return src}
 return await request('original',{post:post.id,index});
}
function rememberOriginal(id,src){
 if(src&&src.length<=24*1024*1024){
  if(originalImages.has(id))originalImageBytes-=originalImages.get(id).length;
  originalImages.delete(id);originalImages.set(id,src);originalImageBytes+=src.length;
  while(originalImages.size>8||originalImageBytes>24*1024*1024){const first=originalImages.keys().next().value;originalImageBytes-=originalImages.get(first).length;originalImages.delete(first)}
 }
}

function loadingPanel(text){const panel=document.createElement('div');panel.className='viewer-loading';panel.setAttribute('role','status');panel.setAttribute('aria-label',text);panel.innerHTML='<span class="media-spinner" aria-hidden="true"></span>';return panel}
function imageFailure(panel,text){panel.hidden=false;panel.classList.add('failed');panel.innerHTML=`<span>${safe(text)}</span><button data-retry-media>ลองใหม่</button>`}

async function enterPreview(){if(qrOpen){qrOpen=false;request('authCancel').catch(()=>{})}reset();preview=true;const demo=await import('./preview.mjs');({groups,posts}=demo.previewData());try{state.saved=new Set(JSON.parse(localStorage.getItem('luma-preview-favorites')||'[]'))}catch{}favorites=posts.filter(p=>state.saved.has(p.id));baseNotice();render(true)}
async function connected(result){
 account={id:result.account,name:result.name};preview=false;favorites=result.favorites||[];state.saved=new Set(favorites.map(p=>p.id));state.tab='groups';busy=true;baseNotice();render();
 let error;
 try{groups=await request('groups');groupsRevision++;refreshUnread();groupsCheckedAt=new Date();groupsStale=false}catch(e){error=e}
 finally{busy=false;baseNotice();render(true)}
 if(error){networkRecoveryNeeded=true;retry=recoverForeground;notice('โหลดกลุ่มไม่สำเร็จ',true,'retry')}
}
async function recoverForeground(){
 if(preview)return;
 appSuspended=false;
 if(recoveringConnection){recoveryQueued=true;return}
 if(!account){if(!starting&&!qrOpen&&!$('sheet').open)await restoreConnection();return}
 const currentAccount=account,token=epoch.next();recoveringConnection=true;busy=false;
 notice('');
 try{
  const result=await request('resume');if(account!==currentAccount||!epoch.current(token))return;
  if(!result?.connected)throw new Error('เชื่อมต่อบัญชีเดิมยังไม่สำเร็จ กรุณาลองอีกครั้ง');
  if(state.tab==='groups'&&await refreshGroups()===false)throw new Error('อัปเดตกลุ่มยังไม่สำเร็จ');
  if(account!==currentAccount||!epoch.current(token))return;
  networkRecoveryNeeded=false;baseNotice();
  const interrupted=pendingNavigation;
  if(state.tab==='library'&&interrupted?.group===state.group){
   pendingNavigation=null;
   if(interrupted.kind==='group')await chooseGroup(interrupted.group);
   else await loadPage(interrupted.page,interrupted.refresh);
   return;
  }
  if($('viewer').open)renderViewer();else render();drainThumbs();
 }catch(e){
  if(account===currentAccount&&epoch.current(token)){networkRecoveryNeeded=true;retry=recoverForeground;notice(e.message||'เชื่อมต่อ Telegram ไม่สำเร็จ กรุณาลองอีกครั้ง',true,'retry')}
 }finally{
  recoveringConnection=false;
  if(recoveryQueued&&!appSuspended){recoveryQueued=false;recoverForeground()}
 }
}

async function chooseGroup(id){
 clearTimeout(searchTimer); closeSheet();state.group=id;state.topic='all';state.tab='library';state.query='';state.type='all';state.selecting=false;state.selected.clear();page=1;historyMessage='';
 if(preview){topics=id==='all'?[...new Set(groups.flatMap(g=>g.topics.map(t=>t.title)))].map(t=>({id:t,title:t})):groups.find(g=>g.id===id).topics;render(true);return}
 clearThumbnailWork();const token=epoch.next(),intent={kind:'group',group:id};pendingNavigation=intent;
 posts=[];topics=[];thumbnails.clear();busy=true;baseNotice();render(true);
 try{await request('cancel');if(!epoch.current(token))return;const list=await request('topics',{group:id});if(!epoch.current(token))return;topics=list;if(groups.find(g=>g.id===id)?.isForum){busy=false;baseNotice();render(true);topicPicker()}else{state.topic=topics[0]?.id||'0';await loadPage(0,false,token)}}catch(e){if(epoch.current(token)){busy=false;retry=()=>chooseGroup(id);notice(e.message,true,'retry');render()}}
 finally{if(epoch.current(token)&&pendingNavigation===intent)pendingNavigation=null}

}
function applyHistory(result, token, resetScroll=false){
 if(!epoch.current(token))return;
 posts=result.posts;page=result.page;knownPages=result.knownPages||1;pageSize=result.pageSize||24;hasNext=result.hasNext;busy=result.busy||false;state.query=result.query||'';
 historyMessage=result.cached?'แสดงข้อมูลที่บันทึกไว้':result.fetchedAt?'อัปเดตล่าสุด '+new Date(result.fetchedAt).toLocaleString('th-TH'):'';
 if(result.error){retry=()=>loadPage(0,true);notice(result.error,true,'retry')}else baseNotice();
 render(resetScroll);
}
window.addEventListener('luma:history',e=>{if(e.detail)applyHistory(e.detail,e.detail.version)});
async function loadPage(number,refresh=false,existingToken){
 clearTimeout(searchTimer);clearThumbnailWork(); const token=existingToken??epoch.next(),intent={kind:'history',group:state.group,page:number,refresh};pendingNavigation=intent;busy=true;baseNotice();render();
 try{await request('cancel');if(!epoch.current(token))return;const result=await request('history',{group:state.group,topic:state.topic,page:number,refresh,pageSize,version:token});if(!epoch.current(token))return;state.selected.clear();thumbnails.clear();applyHistory(result,token,true)}catch(e){if(epoch.current(token)){busy=false;retry=()=>loadPage(number,refresh);notice(e.message,true,'retry');render()}}
 finally{if(epoch.current(token)&&pendingNavigation===intent)pendingNavigation=null}
}
async function searchHistory(){
 if(busy&&!preview)return; if(preview||state.tab!=='library'||state.topic==='all'){renderContent();return}
 const token=epoch.next();try{const result=await request('search',{query:state.query,version:token});applyHistory(result,token)}catch(e){if(epoch.current(token))notice(e.message,true)}
}
function groupPicker(){showSheet('เลือกกลุ่ม',(preview?row('ทุกกลุ่ม','data-group="all"',`${posts.length} รายการ`,'grid',state.group==='all'):'')+groups.map(g=>row(g.title,`data-group="${safe(g.id)}"`,'','albums',state.group===g.id)).join(''))}
function topicPicker(){if(!preview&&state.group==='all'){groupPicker();return}showSheet('เลือกหัวข้อ','<label class="searchbox"><input id="topicSearch" type="search" placeholder="ค้นหาหัวข้อ" aria-label="ค้นหาหัวข้อ"></label>'+(preview?row('ทุกหัวข้อ','data-topic="all"','','grid',state.topic==='all'):'')+topics.map(t=>row(t.title,`data-topic="${safe(t.id)}"`,'','folder',state.topic===t.id)).join(''));$('topicSearch').oninput=e=>{$('sheetBody').querySelectorAll('[data-topic]').forEach(row=>row.hidden=!row.textContent.toLocaleLowerCase().includes(e.target.value.trim().toLocaleLowerCase()))}}
function options(){if(state.tab==='library'){showSheet('มุมมองโพสต์',`<div class="sheet-section">การแสดงผล</div><div class="layout-options">${[1,2].map(n=>`<button class="layout-choice ${state.libraryColumns===n?'active':''}" data-library-layout="${n}" aria-pressed="${state.libraryColumns===n}"><span class="layout-preview" style="--layout-columns:${n}">${Array(n*2).fill('<i></i>').join('')}</span>${n} โพสต์ต่อแถว</button>`).join('')}</div><div class="sheet-section">จำนวนโพสต์ต่อหน้า</div>${[24,48].map(n=>row(n+' รายการ',`data-page-size="${n}"`,'','albums',pageSize===n)).join('')}`);return}showSheet('มุมมองรายการที่เก็บไว้',`<div class="sheet-section">ขนาดกริด</div><div class="density-options">${[2,3,4].map(n=>`<button class="density-option ${state.columns===n?'active':''}" data-density="${n}" aria-pressed="${state.columns===n}"><span class="density-preview" style="grid-template-columns:repeat(${n},1fr)">${Array(n*n).fill('<i></i>').join('')}</span>${n===2?'ภาพใหญ่':n===3?'พอดี':'ดูได้เยอะ'} · ${n}</button>`).join('')}</div><div class="sheet-section">เรียงลำดับ</div>${row('ใหม่ล่าสุด','data-sort="new"','','sort',state.sort==='new')}${row('เก่าสุดก่อน','data-sort="old"','','sort',state.sort==='old')}`)}
function profile(){showSheet(account?`สวัสดี ${account.name}`:'TeleBlog',`<div class="profile-brand"><span class="brand-icon">${icon('luma')}</span><div><h3>TeleBlog</h3></div></div>${account?row('ออกจากระบบ','data-logout','ยกเลิกเซสชันและล้างข้อมูลบนเครื่อง','arrowLeft'):row('เชื่อมต่อ Telegram','data-connect','','albums')}${row('เกี่ยวกับแอป','data-about','','info')}`)}
function login(step=loginStep,error=''){
 if(restorePending){restoreConnection();return}
 if(qrOpen){qrOpen=false;authEpoch.next();request('authCancel').catch(()=>{});loginStep='phone_number';step=loginStep}
 if(logoutPending){showSheet('ล้างข้อมูลบัญชีเดิม',`<p class="sheet-subtitle">ต้องล้างเซสชันเดิมให้เสร็จก่อนเชื่อมต่อบัญชีใหม่</p><button class="blue-button" data-status="cleanup">ลองล้างข้อมูลอีกครั้ง</button>`);return}
 loginStep=step;
 const labels={phone_number:['เบอร์โทรศัพท์','+66…','tel'],verification_code:['รหัสยืนยันจาก Telegram','รหัสยืนยัน','text'],password:['รหัสผ่านยืนยันสองขั้นตอน','รหัสผ่าน','password'],email:['อีเมล','name@example.com','email'],email_verification_code:['รหัสยืนยันอีเมล','รหัสยืนยัน','text']};
 const [label,placeholder,type]=labels[step]||labels.verification_code;
 showSheet('เข้าสู่ระบบด้วยเบอร์โทรศัพท์',`<p class="sheet-subtitle">${step==='phone_number'?'ใช้เบอร์โทรพร้อมรหัสประเทศของบัญชี Telegram':step==='password'?'บัญชีนี้เปิดการยืนยันสองขั้นตอนไว้':'กรอกรหัสที่ Telegram ส่งให้คุณ'}</p><form id="loginForm" class="auth-fields"><label>${label}<input id="loginInput" type="${type}" placeholder="${placeholder}" autocomplete="${step==='password'?'current-password':step==='phone_number'?'tel':'one-time-code'}" required></label><p class="error-message" role="alert">${safe(error)}</p><button class="blue-button" type="submit">${step==='phone_number'?'รับรหัสยืนยัน':'ยืนยัน'}</button></form>${step==='phone_number'?'<button class="plain-action" data-qr>เข้าสู่ระบบด้วย QR code</button>':''}`);
 $('loginForm').onsubmit=async e=>{e.preventDefault();const authToken=authEpoch.next();const button=e.target.querySelector('button'),input=$('loginInput').value.trim();if(!input)return;button.disabled=true;button.textContent='กำลังเชื่อมต่อ…';$('loginInput').value='';try{const result=await request('login',{input});if(!authEpoch.current(authToken)){if(!result.connected&&result.step)loginStep=result.step;return}if(result.connected){reset();closeSheet();await connected(result)}else login(result.step)}catch(e){if(authEpoch.current(authToken)){await request('authCancel').catch(()=>{});loginStep='phone_number';login(loginStep,e.message)}}};
}
async function saveChanges(changes,collectionId){
 if(savingFavorites)return;
 savingFavorites=true;$('favorite').disabled=true;$('saveSelection').disabled=true;
 const currentAccount=account;
 try{
  if(preview){for(const c of changes)c.saved?state.saved.add(c.id):state.saved.delete(c.id);favorites=posts.filter(p=>state.saved.has(p.id));try{localStorage.setItem('luma-preview-favorites',JSON.stringify([...state.saved]))}catch{toast('พื้นที่จัดเก็บไม่พร้อม รูปโปรดจะจำไว้เฉพาะครั้งนี้')}}
  else{
   if(changes.some(c=>c.saved)){$('favoriteLabel').textContent='กำลังเก็บ…'}
   const saved=await request('favorite',{changes,...(collectionId?{collectionId}:{})});if(account!==currentAccount)return;
   favorites=saved;state.saved=new Set(favorites.map(p=>p.id));
   toast(changes.some(c=>c.saved)?'เก็บแล้ว':'นำออกแล้ว');
  }
  collections.retain(state.saved);if($('viewer').open)updateFavorite();header();renderContent();
 }finally{savingFavorites=false;$('favorite').disabled=false;$('saveSelection').disabled=savingFavorites||!state.selected.size;if($('viewer').open)updateFavorite()}
}

function openViewer(id,element,startIndex=0){const post=visible().find(p=>p.id===id);if(!post)return;lastTile=element;viewer=post.media.length?post.media.map((media,index)=>({post,media,index})):[{post,media:null,index:0}];viewerIndex=Number.isInteger(startIndex)?Math.max(0,Math.min(startIndex,viewer.length-1)):0;$('viewer').showModal();renderViewer()}
function stopInlineVideo(){videoExpandRequested=false;videoAttempt++;inlineVideo=false;videoStarting=false;if(!preview)request('closeMedia').catch(()=>{})}
function videoFrame(){const r=$('viewerStage').getBoundingClientRect();return {x:r.x,y:r.y+48,width:r.width,height:Math.max(1,r.height-48),viewportWidth:innerWidth}}
new ResizeObserver(()=>{if(inlineVideo&&$('viewer').open)request('videoLayout',{frame:videoFrame()}).catch(()=>{})}).observe($('viewerStage'));
window.addEventListener('resize',()=>{if(inlineVideo)request('videoLayout',{frame:videoFrame()}).catch(()=>{})});
function releaseVideo(){const v=$('viewerStage').querySelector('video');if(v){v.pause();v.removeAttribute('src');v.load()}}
function closeViewer(){setImageFullscreen(false);viewerEpoch++;filmstripObserver.disconnect();releaseVideo();if($('viewer').open){$('viewer').close();$('viewerStage').querySelectorAll('.main-media,.play-native,.viewer-loading').forEach(e=>e.remove());$('filmstrip').replaceChildren();$('filmstrip').removeAttribute('data-post-key');stopInlineVideo();drainThumbs();lastTile?.isConnected&&lastTile.focus({preventScroll:true})}}
async function renderViewer(){
 setImageFullscreen(false);
 const token=++viewerEpoch;filmstripObserver.disconnect();releaseVideo();stopInlineVideo();
 const item=viewer[viewerIndex];if(!item)return;const {post:p,media:m,index}=item;
 $('viewerGroup').textContent=p.groupTitle||groupName(p.group);$('viewerTopic').textContent=p.topicTitle||p.topic;
 $('viewerTitle').textContent=p.text||p.title;$('viewerDate').hidden=true;$('viewerDate').textContent='';
 const stage=$('viewerStage');stage.classList.toggle('video-mode',m?.kind==='video');stage.classList.remove('zoomed');stage.querySelectorAll('.play-native,.viewer-loading').forEach(e=>e.remove());
 $('viewerCount').textContent=`${viewerIndex+1} / ${viewer.length}`;$('prevMedia').disabled=viewerIndex===0;$('nextMedia').disabled=viewerIndex===viewer.length-1;
 $('zoomButton').hidden=!m;$('zoomButton').setAttribute('aria-label',m?.kind==='video'?'ขยายวิดีโอเต็มหน้าจอ':'ขยายภาพ');$('zoomButton').innerHTML=icon('expand');$('download').disabled=!m;
 const strip=$('filmstrip'),stripKey=JSON.stringify([p.id,...viewer.map(x=>x.media?.id)]);strip.hidden=viewer.length<=1;
 if(strip.dataset.postKey!==stripKey){strip.dataset.postKey=stripKey;strip.innerHTML=viewer.map((x,i)=>`<button class="thumb ${i===viewerIndex?'active':''}" data-media-index="${i}" aria-label="${safe(x.post.title)} ${x.index+1}" aria-current="${i===viewerIndex}">${x.media?imageMarkup(x.post,x.index):icon('location')}${x.media?.kind==='video'?icon('play'):''}</button>`).join('')}
 else strip.querySelectorAll('[data-media-index]').forEach(button=>{const active=Number(button.dataset.mediaIndex)===viewerIndex;button.classList.toggle('active',active);button.setAttribute('aria-current',String(active))});

 updateFavorite();
 if(!m){stage.querySelectorAll('.main-media').forEach(e=>e.remove());stage.insertAdjacentHTML('afterbegin',`<div class="main-media empty-state text-message">${icon('location')}<p>${safe(p.text||p.title)}</p><button class="blue-button" data-source>เปิดลิงก์ต้นฉบับ</button></div>`);return}
 let originalReady=false;
 async function showImage(src,full=false){
  if(!src)return false;
  const current=stage.querySelector('.main-media');
  if(current?.dataset.mediaId===m.id&&current.src===src&&current.complete){if(full)originalReady=true;return true}
  const image=new Image();image.className='main-media';image.alt=p.title;image.dataset.mediaId=m.id;image.src=src;
  await image.decode();
  if(token!==viewerEpoch||!$('viewer').open||(!full&&originalReady))return false;
  if(full)originalReady=true;
  const old=stage.querySelector('.main-media');
  if(old)old.replaceWith(image);else stage.prepend(image);
  return true;
 }
 const active=strip.querySelector('.active');if(active){const left=active.offsetLeft,right=left+active.offsetWidth;if(left<strip.scrollLeft)strip.scrollLeft=Math.max(0,left-8);else if(right>strip.scrollLeft+strip.clientWidth)strip.scrollLeft=right-strip.clientWidth+8}
 const cachedOriginal=originalImages.get(m.id);
 const loading=!preview?loadingPanel(m.kind==='video'?'กำลังโหลดภาพตัวอย่าง…':'กำลังโหลดภาพ…'):null;
 if(loading){loading.hidden=!!cachedOriginal;stage.append(loading)}
 if(preview&&m.kind==='video'){
  const video=document.createElement('video');video.className='main-media';video.src=m.url;video.poster=m.thumb;video.controls=true;video.playsInline=true;video.preload='metadata';
  const old=stage.querySelector('.main-media');if(old)old.replaceWith(video);else stage.prepend(video);
 }else if(preview){try{await showImage(m.url,true)}catch{}}
 else{
  const poster=m.thumb||thumbnails.get(m.id);
  if(!cachedOriginal&&poster)showImage(poster).catch(()=>{});
  if(!cachedOriginal)getThumbnail(p,index,true).then(async src=>{
   if(token!==viewerEpoch)return;
   if(src&&!originalReady)await showImage(src);
   if(token!==viewerEpoch)return;
   if(m.kind==='video'){if(src)loading.remove();else imageFailure(loading,'ไม่มีภาพตัวอย่าง กด Play เพื่อเล่นได้เลย')}
  }).catch(()=>{if(token===viewerEpoch&&m.kind==='video')imageFailure(loading,'โหลดภาพตัวอย่างไม่สำเร็จ')});
 }
 strip.querySelectorAll('[data-thumb-post]').forEach(img=>filmstripObserver.observe(img));
 if(m.kind==='video'&&!preview){const play=document.createElement('button');play.className='play-native';play.innerHTML=icon('play');play.setAttribute('aria-label','เล่นวิดีโอ');play.onclick=()=>nativeAction('play');stage.append(play)}
 else if((m.kind==='image'||m.kind==='photo')&&!preview){
  try{const src=await getOriginal(p,index);if(token!==viewerEpoch)return;if(!src)throw new Error('ภาพต้นฉบับไม่พร้อมใช้งาน');if(await showImage(src,true)){rememberOriginal(m.id,src);loading.remove()}}
  catch(e){if(token===viewerEpoch){if(originalImages.has(m.id)){originalImageBytes-=originalImages.get(m.id).length;originalImages.delete(m.id)}imageFailure(loading,'โหลดภาพไม่สำเร็จ กรุณาลองใหม่')}}
 }

}
function updateFavorite(){const p=viewer[viewerIndex]?.post;if(!p)return;const saved=state.saved.has(p.id);$('favorite').classList.toggle('saved',saved);$('favorite').setAttribute('aria-pressed',saved);$('favoriteLabel').textContent=saved?'เก็บไว้แล้ว':'เก็บไว้'}
function step(delta){if(viewerIndex+delta<0||viewerIndex+delta>=viewer.length)return;viewerIndex+=delta;renderViewer()}
function mediaInfo(){const x=viewer[viewerIndex];if(!x)return;showSheet('รายละเอียดสื่อ',`${row('เก็บเข้าคอลเลกชัน',`data-organize="${safe(x.post.id)}"`)}<dl class="detail-list">${[['กลุ่ม',x.post.groupTitle||groupName(x.post.group)],['หัวข้อ',x.post.topicTitle||x.post.topic],['วันที่',dateFormat.format(new Date(x.post.date))],['ประเภท',x.media?.kind==='video'?'วิดีโอ':x.media?'รูปภาพ':'ลิงก์']].map(([k,v])=>`<div><dt>${k}</dt><dd>${safe(v)}</dd></div>`).join('')}</dl>${preview?'<p class="demo-note">ข้อมูลตัวอย่าง · ภาพจาก Unsplash และวิดีโอจาก MDN</p>':row('เปิดใน Telegram','data-source','','location')}${!preview?(x.post.links||[]).map((url,i)=>row(url,`data-link-index="${i}"`,'','location')).join(''):''}`)}
async function nativeAction(action){
 const item=viewer[viewerIndex];if(!item)return;
 if(action==='play'||action==='expandVideo'){
  if(recoveringConnection){toast('กำลังเชื่อมต่อ Telegram กรุณารอสักครู่');return}
  if(preview){const video=$('viewerStage').querySelector('video');if(action==='expandVideo'){if(video?.webkitEnterFullscreen)video.webkitEnterFullscreen();else video?.requestFullscreen?.()}else await video?.play();return}
  if(videoStarting){if(action==='expandVideo')videoExpandRequested=true;return;}
  if(inlineVideo){if(action==='expandVideo')await request('expandVideo');return}
  const token=viewerEpoch,attempt=++videoAttempt;videoStarting=true;videoExpandRequested=action==='expandVideo';
  const button=$('viewerStage').querySelector('.play-native');if(button){button.disabled=true;button.innerHTML='<span class="media-spinner" aria-hidden="true"></span>';button.setAttribute('aria-label','กำลังโหลดวิดีโอ')}

  try{
   await request('play',{post:item.post.id,index:item.index,frame:videoFrame()});
   if(token!==viewerEpoch||attempt!==videoAttempt)return;
   inlineVideo=true;
   await request('videoLayout',{frame:videoFrame()});
   if(token===viewerEpoch&&attempt===videoAttempt&&videoExpandRequested){videoExpandRequested=false;await request('expandVideo');}
  }catch(e){if(token===viewerEpoch&&attempt===videoAttempt)toast(e.message)}
  finally{if(attempt===videoAttempt){videoStarting=false;if(button){button.disabled=false;button.innerHTML=icon('play');button.setAttribute('aria-label','เล่นวิดีโอ')}}}
  return;
 }
 if(preview){if(action==='source'){mediaInfo();return}if(action==='share'){try{const response=await fetch(item.media.url);const blob=await response.blob();const file=new File([blob],`luma-preview.${item.media.kind==='video'?'mp4':'jpg'}`,{type:blob.type});if(navigator.canShare?.({files:[file]}))await navigator.share({files:[file],title:item.post.title});else toast('การแชร์ไฟล์ตัวอย่างไม่รองรับในมุมมองนี้')}catch(e){if(e.name!=='AbortError')toast('แชร์ไม่สำเร็จ')}return}return}
 const token=viewerEpoch;
 try{await request(action,{post:item.post.id,index:item.index})}catch(e){if(token===viewerEpoch)toast(e.message)}
}
document.addEventListener('click',async e=>{
 const b=e.target.closest('button');if(!b||b.disabled)return;
 try{
 if(b.dataset.confirmRemoveSaved){b.disabled=true;try{await saveChanges([{id:b.dataset.confirmRemoveSaved,saved:false}]);if(b.isConnected)closeSheet()}finally{b.disabled=false}return}
 if(await collections.click(b))return;
 if(b.hasAttribute('data-retry-media'))return await renderViewer();
 if(b.hasAttribute('data-refresh-groups'))return await refreshGroups();
 if(b.hasAttribute('data-resume'))return await restoreConnection();
 if(b.hasAttribute('data-reconnect')){showSheet('เริ่มเชื่อมต่อใหม่?',`<p class="sheet-subtitle">จะออกจากระบบและล้างเซสชัน แคช และรายการโปรดบนเครื่อง แล้วให้คุณสแกน QR หรือใช้เบอร์โทรใหม่</p><button class="blue-button" data-confirm-reconnect>ล้างเซสชันเดิมและเชื่อมต่อใหม่</button>`);return}
 if(b.hasAttribute('data-confirm-reconnect')){b.disabled=true;const result=await request('logout');logoutPending=!result.localCleared;if(logoutPending){baseNotice();return}reset();closeSheet();render();loginStep='phone_number';login();return}
 if(b.hasAttribute('data-qr'))return await startQr();
 if(b.hasAttribute('data-preview'))return await enterPreview();
 if(b.hasAttribute('data-connect')||b.dataset.status==='connect'){login();return}
 if(b.dataset.status==='cancel'){clearThumbnailWork();epoch.next();await request('cancel');busy=false;baseNotice();render();return}
 if(b.dataset.status==='retry'){retry?.();return}
 if(b.dataset.status==='cleanup'){const result=await request('logout');logoutPending=!result.localCleared;if(result.localCleared){loginStep='phone_number';closeSheet();baseNotice()}else notice('ยังล้างข้อมูลไม่สำเร็จ กรุณาลองอีกครั้ง',true,'cleanup');return}
 if(b.dataset.tab){clearTimeout(searchTimer);if(!preview&&b.dataset.tab==='groups'){clearThumbnailWork();epoch.next();request('cancel').catch(()=>{});request('backGroups').catch(()=>{});busy=false;baseNotice()}state.tab=b.dataset.tab;state.query='';state.type='all';state.selecting=false;state.selected.clear();render(true);if(state.tab==='groups')await refreshGroups();if(state.tab==='saved')await collections.load();return}
 if(b.dataset.filter){state.type=b.dataset.filter;render(true);return}
 if(b.dataset.group)return await chooseGroup(b.dataset.group);
 if(b.dataset.topic){state.topic=b.dataset.topic;closeSheet();if(preview)render(true);else await loadPage(0);return}
 if(b.dataset.libraryLayout){state.libraryColumns=Number(b.dataset.libraryLayout);try{localStorage.setItem('luma-library-columns',String(state.libraryColumns))}catch{}closeSheet();render(true);return}
 if(b.dataset.openPost){const id=b.dataset.openPost;if(state.selecting){state.selected.has(id)?state.selected.delete(id):state.selected.add(id);render()}else openViewer(id,b,Number(b.dataset.openIndex));return}
 if(b.dataset.pageSize){pageSize=Number(b.dataset.pageSize);closeSheet();await loadPage(0);return}
 if(b.dataset.density){state.columns=Number(b.dataset.density);renderContent();options();return}
 if(b.dataset.sort){state.sort=b.dataset.sort;renderContent();options();return}
 if(b.dataset.post){if(state.selecting){state.selected.has(b.dataset.post)?state.selected.delete(b.dataset.post):state.selected.add(b.dataset.post);render()}else openViewer(b.dataset.post,b);return}
 if(b.dataset.mediaIndex!==undefined){const next=Number(b.dataset.mediaIndex);if(next===viewerIndex)return;viewerIndex=next;await renderViewer();return}
 if(b.hasAttribute('data-reset')&&state.tab==='saved'){state.query='';state.type='all';collections.clearFilters();render(true);return}
 if(b.hasAttribute('data-reset')){if(state.query||state.type!=='all'){state.query='';state.type='all';if(!preview&&state.tab==='library')searchHistory();else render(true)}else if(!preview&&state.group!=='all'&&state.topic==='all')topicPicker();else groupPicker();return}
 if(b.dataset.page)return await loadPage(Number(b.dataset.page));
 if(b.hasAttribute('data-refresh'))return await loadPage(1,true);
 if(b.dataset.linkIndex!==undefined){const item=viewer[viewerIndex];await request('source',{post:item.post.id,linkIndex:Number(b.dataset.linkIndex)});return}
 if(b.hasAttribute('data-source'))return await nativeAction('source');
 if(b.hasAttribute('data-about')){showSheet('เกี่ยวกับ TeleBlog','<p class="sheet-tip">TeleBlog · คลังสื่อ Telegram ส่วนตัว<br><br>ภาพในโหมดตัวอย่าง: Unsplash<br>วิดีโอตัวอย่าง: MDN (CC0)<br><br>รูปโปรดเก็บบนอุปกรณ์นี้ เปิดข้อความต้นทางได้จากแต่ละรายการ</p>');return}
 if(b.hasAttribute('data-logout')){showSheet('ออกจากระบบ?',`<p class="sheet-subtitle">ล้างเซสชัน รูปโปรด และแคชบัญชีนี้ออกจากเครื่อง</p><button class="blue-button" data-confirm-logout>ออกจากระบบ</button>`);return}
 if(b.hasAttribute('data-confirm-logout')){b.disabled=true;const result=await request('logout');logoutPending=!result.localCleared;loginStep='phone_number';reset();baseNotice();render(true);if(!result.remoteRevoked)toast('ล้างข้อมูลแล้ว กรุณาตรวจสอบเซสชันใน Telegram → Devices');if(!result.localCleared)notice('ล้างข้อมูลบางส่วนไม่สำเร็จ จะไม่เชื่อมต่อบัญชีนี้อัตโนมัติ',true,'cleanup');return}
 }catch(e){toast(e.message)}
});
$('search').oninput=e=>{state.query=e.target.value;$('clearSearch').hidden=!state.query;clearTimeout(searchTimer);if(!preview&&state.tab==='library')searchTimer=setTimeout(searchHistory,220);else renderContent()};$('clearSearch').onclick=()=>{clearTimeout(searchTimer);state.query='';if(state.tab==='saved')renderContent();else searchHistory();header();$('search').focus()};
$('profile').onclick=profile;$('groupPicker').onclick=groupPicker;$('topicPicker').onclick=topicPicker;$('options').onclick=options;$('closeSheet').onclick=cancelSheet;$('sheet').addEventListener('cancel',e=>{e.preventDefault();cancelSheet()});
$('selectButton').onclick=()=>{state.selecting=!state.selecting;state.selected.clear();render()};
$('saveSelection').onclick=async()=>{try{await collections.saveNew([...state.selected]);state.selecting=false;state.selected.clear();render()}catch(e){toast(e.message)}};
$('closeViewer').onclick=closeViewer;$('viewer').addEventListener('cancel',e=>{e.preventDefault();closeViewer()});$('prevMedia').onclick=()=>step(-1);$('nextMedia').onclick=()=>step(1);$('viewerInfo').onclick=mediaInfo;$('sourceInfo').onclick=()=>nativeAction('source');$('download').onclick=()=>nativeAction('share');
$('favorite').onclick=async()=>{const id=viewer[viewerIndex]?.post.id;if(id)try{state.saved.has(id)?await saveChanges([{id,saved:false}]):await collections.saveNew([id])}catch(e){toast(e.message)}};
function setImageFullscreen(full){$('viewer').classList.toggle('media-fullscreen',full);$('exitMediaFullscreen').hidden=!full;}
function expandCurrentMedia(){if(!$('viewer').open||$('sheet').open||!viewer[viewerIndex]?.media)return;if(viewer[viewerIndex].media.kind==='video'){nativeAction('expandVideo').catch(e=>toast(e.message));return}setImageFullscreen(!$('viewer').classList.contains('media-fullscreen'))}
$('zoomButton').onclick=expandCurrentMedia;
$('exitMediaFullscreen').onclick=()=>setImageFullscreen(false);
$('viewerStage').addEventListener('dblclick',e=>{if(e.target.closest('button')&&!e.target.closest('.play-native'))return;e.preventDefault();expandCurrentMedia()});

let swipe=null;$('viewerStage').addEventListener('touchstart',e=>{swipe=e.touches.length===1&&!$('viewerStage').classList.contains('zoomed')&&e.target.tagName!=='VIDEO'?{x:e.touches[0].clientX,y:e.touches[0].clientY}:null},{passive:true});$('viewerStage').addEventListener('touchend',e=>{if(!swipe)return;const dx=e.changedTouches[0].clientX-swipe.x,dy=e.changedTouches[0].clientY-swipe.y;if(Math.abs(dx)>50&&Math.abs(dx)>Math.abs(dy)*1.3)step(dx<0?1:-1);swipe=null},{passive:true});
document.addEventListener('keydown',e=>{if(!$('viewer').open||$('sheet').open||['INPUT','TEXTAREA','SELECT','VIDEO'].includes(e.target.tagName))return;if(e.key==='ArrowRight')step(1);if(e.key==='ArrowLeft')step(-1)});
window.addEventListener('luma:suspend',()=>{
 appSuspended=true;groupsStale=true;connectionGeneration++;clearThumbnailWork();epoch.next();viewerEpoch++;releaseVideo();stopInlineVideo();busy=false;baseNotice();render();
});
window.addEventListener('luma:resume',()=>recoverForeground());
window.addEventListener('luma:network',()=>{if(account&&!appSuspended)recoverForeground()});
document.addEventListener('visibilitychange',()=>{if(document.hidden)releaseVideo();else if(appSuspended||networkRecoveryNeeded)recoverForeground();else refreshGroups()});

hydrate();render();
if(new URLSearchParams(location.search).has('preview'))await enterPreview();
else if(window.HybridWebView)await restoreConnection();
async function restoreConnection(){
 const token=authEpoch.next();starting=true;render();
 try{
  const result=await request('start');if(!authEpoch.current(token))return;
  starting=false;restorePending=!!result.restorePending;restoreMessage=result.message||'เชื่อมต่อบัญชีเดิมไม่สำเร็จ กรุณาลองอีกครั้ง';
  if(result.logoutPending){logoutPending=true;baseNotice()}
  else if(result.preview)await enterPreview();
  else if(result.connected){restorePending=false;closeSheet();await connected(result)}
  else if(result.step)login(result.step);
 }catch(e){if(authEpoch.current(token)){restorePending=true;restoreMessage='เชื่อมต่อบัญชีเดิมไม่สำเร็จ กรุณาตรวจสอบอินเทอร์เน็ตแล้วลองอีกครั้ง'}}
 finally{if(authEpoch.current(token)){starting=false;render()}}
}

async function startQr(){
 authEpoch.next();await request('authCancel');loginStep='phone_number';qrOpen=true;
 const token=authEpoch.next();
 showSheet('เข้าสู่ระบบด้วย QR Code','<ol class="qr-instructions"><li>เปิด Telegram บนอุปกรณ์ที่ล็อกอินไว้</li><li>ไปที่ การตั้งค่า → อุปกรณ์ → เชื่อมต่ออุปกรณ์</li><li>สแกน QR Code ด้านล่าง</li></ol><div id="qrContent" class="empty-state">กำลังสร้าง QR code…</div><button class="plain-action" data-connect>ใช้เบอร์โทรศัพท์แทน</button>');
 try{const result=await request('qrStart');if(!authEpoch.current(token))return;qrOpen=false;reset();await connected(result)}catch(e){if(authEpoch.current(token)){await request('authCancel').catch(()=>{});qrOpen=false;login('phone_number',e.message)}}
}
window.addEventListener('luma:qr',e=>{
 if(!qrOpen||!$('qrContent'))return;const info=e.detail;
 if(info.password){$('qrContent').innerHTML='<form id="qrPasswordForm" class="auth-fields"><label>รหัสผ่านยืนยันสองขั้นตอน<input id="qrPassword" type="password" autocomplete="current-password" required></label><button class="blue-button">ยืนยันรหัสผ่าน</button></form>';$('qrPasswordForm').onsubmit=async event=>{event.preventDefault();const input=$('qrPassword').value;$('qrPassword').value='';try{await request('qrPassword',{input})}catch(error){toast(error.message)}}}
 else if(info.image)$('qrContent').innerHTML=`<img src="${safe(info.image)}" alt="QR สำหรับเชื่อมบัญชี Telegram" style="width:220px;height:220px"><p>QR จะต่ออายุอัตโนมัติ</p>`;
});

document.addEventListener('keydown',e=>{if(e.key==='Escape'&&$('viewer').open&&!$('sheet').open&&$('viewer').classList.contains('media-fullscreen')){e.preventDefault();e.stopImmediatePropagation();setImageFullscreen(false)}},true);
