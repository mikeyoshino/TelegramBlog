import {matchesSaved,highlightSaved,searchExcerpt,firstMatchingField} from './saved-search.mjs';
export function createCollections({request,safe,showSheet,closeSheet,render,toast,posts,isSaved,save,source,isPreview}) {
 let data={collections:[],items:[]},collection='all',status='all',generation=0,loaded=false,loading=false,error=false,editing=null,pendingIds=[],recentId=null,recentAt=0;
 const detail=id=>data.items.find(x=>x.postId===id)||{postId:id,collectionId:null,note:'',viewed:false};
 const name=id=>data.collections.find(x=>x.id===id)?.name||'ยังไม่จัดหมวด';
 async function mutate(payload){
  const version=generation;
  if(isPreview()){throw new Error('จัดคอลเลกชันได้หลังเชื่อมต่อ Telegram')}
  const next=await request('organization',payload);
  if(version!==generation)return false;
  if(!next||!Array.isArray(next.collections)||!Array.isArray(next.items))throw new Error('โหลดคอลเลกชันไม่สำเร็จ');data=next;loaded=true;error=false;return true;
 }
 async function load({refresh=true}={}){
  if(loading||loaded||isPreview())return;
  const version=generation;loading=true;error=false;
  try{await mutate({operation:'load'})}catch(e){if(version===generation){error=true;toast(e.message)}}
  finally{if(version===generation){loading=false;if(refresh)render()}}
 }
 function choiceOptions(options,value){return options.map(c=>`<button type="button" data-choice-value="${safe(c.id)}" aria-pressed="${c.id===value}"><span>${safe(c.name)}</span><span aria-hidden="true">${c.id===value?'✓':''}</span></button>`).join('')}
 function picker(id,value,options,attr=''){return `<div class="collection-choice"><button type="button" id="${id}" ${attr} data-choice-toggle value="${safe(value)}" aria-label="คอลเลกชัน" aria-expanded="false" aria-controls="${id}Options"><span>${safe(options.find(c=>c.id===value)?.name||'เลือกคอลเลกชัน')}</span><span aria-hidden="true">⌄</span></button><div id="${id}Options" class="collection-choices" role="group" aria-label="เลือกคอลเลกชัน" hidden>${choiceOptions(options,value)}</div></div>`}
 function closeChoices(){document.querySelectorAll('[data-choice-toggle][aria-expanded="true"]').forEach(b=>{b.setAttribute('aria-expanded','false');document.getElementById(b.getAttribute('aria-controls')).hidden=true})}
 document.addEventListener('pointerdown',e=>{if(!e.target.closest('.collection-choice'))closeChoices()},true);
 document.addEventListener('keydown',e=>{const b=document.querySelector('[data-choice-toggle][aria-expanded="true"]');if(e.key==='Escape'&&b){e.preventDefault();e.stopImmediatePropagation();closeChoices();b.focus()}},true);
 function toolbar(){return `<section class="collection-toolbar" aria-label="จัดรายการที่เก็บไว้"><div class="collection-filter"><div class="collection-field"><span>คอลเลกชัน</span>${picker('filterCollection',collection,[{id:'all',name:'ทั้งหมด'},{id:'none',name:'ยังไม่จัดหมวด'},...data.collections],'data-collection-filter')}</div><button class="plain-action" data-manage-collections>จัดการหมวด</button></div><div class="collection-status">${[['all','ทั้งหมด'],['unseen','ยังไม่ได้ดู'],['seen','ดูแล้ว']].map(([id,title])=>`<button data-view-filter="${id}" aria-pressed="${status===id}">${title}</button>`).join('')}</div>${loading?'<span class="media-spinner" role="status" aria-label="กำลังโหลดคอลเลกชัน"></span>':error?'<button class="plain-action" data-retry-collections>โหลดคอลเลกชันอีกครั้ง</button>':''}</section>`}
 function filter(list,query){return list.filter(p=>{const d=detail(p.id);return(collection==='all'||(collection==='none'?!d.collectionId:d.collectionId===collection))&&(status==='all'||(status==='seen'?d.viewed:!d.viewed))&&matchesSaved([p.title,p.text,...(p.links||[]),p.groupTitle,p.topicTitle,d.note,d.collectionId?name(d.collectionId):''],query)})}
 function caption(p,query=''){const d=detail(p.id),searching=!!query.trim(),highlight=value=>highlightSaved(value,query);
 const excerpt=searchExcerpt(firstMatchingField([p.text,d.note,...(p.links||[])],query),query);
 const summary=searching?`<button class="saved-search-summary" data-post="${safe(p.id)}"><strong>${highlight(p.title)}</strong><span>${highlight([p.groupTitle,p.topicTitle].filter(Boolean).join(' · '))}</span>${excerpt?`<p>${highlight(excerpt)}</p>`:''}</button>`:'';
 return `<div class="saved-card-info">${summary}${d.collectionId?`<span class="saved-category">${highlight(name(d.collectionId))}</span>`:''}<span class="saved-viewed">${d.viewed?'ดูแล้ว':'ยังไม่ได้ดู'}</span>${d.note&&!searching?`<p>${safe(d.note)}</p>`:''}<button class="plain-action" data-organize="${safe(p.id)}" aria-label="จัดการ ${safe(p.title)}">จัดหมวด / โน้ต</button><button class="saved-more" data-saved-menu aria-haspopup="menu" aria-label="เมนูข้อความที่เก็บไว้">•••</button></div>`}
 function remember(id){recentId=id;recentAt=Date.now()}
 async function saveNew(ids){
  ids=ids.filter(id=>!isSaved(id));if(!ids.length)return;
  if(isPreview()){await save(ids);return}
  if(!loaded){await load();if(!loaded)return}
  if(recentId&&Date.now()-recentAt<30*60*1000&&data.collections.some(c=>c.id===recentId)){
   const version=generation,target=recentId;
   await save(ids,target);if(version!==generation)return;
   await mutate({operation:'load'});if(version!==generation)return;
   remember(target);render();toast(`เก็บใน “${name(target)}” แล้ว`);
  }else await editor(ids[0],ids);
 }
 async function editor(id,ids=[]){
  if(!loaded&&!isPreview()){await load();if(!loaded)return}
  editing=id;pendingIds=ids;const d=detail(id),p=posts().find(x=>x.id===id);if(!p)return;
  showSheet('เก็บเข้าคอลเลกชัน',`<div class="collection-editor"><p class="sheet-subtitle">${safe(p.title)}</p><div class="collection-field"><span>คอลเลกชัน</span>${picker('editCollection',d.collectionId||'',[{id:'',name:ids.length?'เลือกคอลเลกชัน':'ยังไม่จัดหมวด'},...data.collections])}</div><label>หรือสร้างหมวดใหม่<input id="newCollection" maxlength="80" placeholder="เช่น คลิปเรียนเขียนโปรแกรม"></label><label>โน้ตส่วนตัว<textarea id="collectionNote" maxlength="4000" rows="4" placeholder="สิ่งที่อยากกลับมาดู">${safe(d.note)}</textarea></label><label class="viewed-check"><input id="collectionViewed" type="checkbox" ${d.viewed?'checked':''}> ดูแล้ว</label><button class="blue-button" data-save-organization>บันทึก</button><button class="plain-action" data-post-source="${safe(id)}">ไปข้อความต้นทาง</button><button class="plain-action" data-cancel-organization>ยกเลิก</button></div>`);
 }
 function manage(){showSheet('จัดการคอลเลกชัน',`<div class="collection-editor"><label>สร้างหมวดใหม่<input id="collectionName" maxlength="80" placeholder="ชื่อคอลเลกชัน"></label><button class="blue-button" data-create-collection>สร้างหมวด</button>${data.collections.map(c=>`<div class="collection-manage-row"><span>${safe(c.name)}</span><button class="plain-action" data-rename-collection="${safe(c.id)}">แก้ไข</button><button class="plain-action" data-delete-collection="${safe(c.id)}">ลบ</button></div>`).join('')}</div>`)}
 async function click(b){
  if(b.hasAttribute('data-choice-toggle')){const open=b.getAttribute('aria-expanded')!=='true';closeChoices();b.setAttribute('aria-expanded',String(open));document.getElementById(b.getAttribute('aria-controls')).hidden=!open;return true}
  if(b.hasAttribute('data-choice-value')){const root=b.closest('.collection-choice'),trigger=root.querySelector('[data-choice-toggle]');trigger.value=b.dataset.choiceValue;trigger.firstElementChild.textContent=b.firstElementChild.textContent;root.querySelectorAll('[data-choice-value]').forEach(option=>{const selected=option===b;option.setAttribute('aria-pressed',String(selected));option.lastElementChild.textContent=selected?'✓':''});closeChoices();trigger.focus({preventScroll:true});if(trigger.hasAttribute('data-collection-filter')){collection=trigger.value;render()}return true}

  if(b.dataset.organize){await editor(b.dataset.organize);return true}
  if(b.hasAttribute('data-cancel-organization')){closeSheet();return true}
  if(b.dataset.postSource){await source(b.dataset.postSource);return true}
  if(b.dataset.viewFilter){status=b.dataset.viewFilter;render();return true}
  if(b.hasAttribute('data-retry-collections')){await load();return true}
  if(b.hasAttribute('data-manage-collections')){if(!loaded){await load();if(!loaded)return true}manage();return true}
  if(b.dataset.deleteCollection){showSheet('ลบคอลเลกชัน?',`<p class="sheet-subtitle">รายการจะย้ายไป “ยังไม่จัดหมวด” ภาพ โน้ต และสถานะยังอยู่</p><button class="blue-button" data-confirm-delete-collection="${safe(b.dataset.deleteCollection)}">ลบหมวด</button><button class="plain-action" data-manage-collections>ยกเลิก</button>`);return true}
  if(b.dataset.renameCollection){showSheet('เปลี่ยนชื่อคอลเลกชัน',`<div class="collection-editor"><label>ชื่อคอลเลกชัน<input id="collectionName" maxlength="80" value="${safe(name(b.dataset.renameCollection))}"></label><button class="blue-button" data-update-collection="${safe(b.dataset.renameCollection)}">บันทึก</button></div>`);return true}
  if(b.hasAttribute('data-create-collection')||b.dataset.updateCollection){b.disabled=true;try{if(await mutate({operation:'collection',id:b.dataset.updateCollection||null,name:document.getElementById('collectionName').value})){if(b.isConnected&&document.getElementById('sheet').open)manage();render()}}finally{b.disabled=false}return true}
  if(b.dataset.confirmDeleteCollection){b.disabled=true;try{if(await mutate({operation:'delete',id:b.dataset.confirmDeleteCollection})){if(collection===b.dataset.confirmDeleteCollection)collection='all';if(b.isConnected&&document.getElementById('sheet').open)manage();render()}}finally{b.disabled=false}return true}
  if(b.hasAttribute('data-save-organization')){
   b.disabled=true;const id=editing,version=generation,ids=pendingIds.length?[...pendingIds]:[id],newSave=pendingIds.length>0||!isSaved(id);
   const newName=document.getElementById('newCollection').value.trim();let collectionId=document.getElementById('editCollection').value||null;
   const note=document.getElementById('collectionNote').value,viewed=document.getElementById('collectionViewed').checked;
   try{
    if(newSave&&!collectionId&&!newName)throw new Error('กรุณาเลือกหมวดหรือสร้างหมวดใหม่');
    if(newName){if(!await mutate({operation:'collection',name:newName}))return true;collectionId=data.collections.find(c=>c.name===newName)?.id||null;
     // Keep the newly created choice in the editor if the item save fails.
     if(!b.isConnected||!document.getElementById('sheet').open)return true;const select=document.getElementById('editCollection');select.value=collectionId;select.firstElementChild.textContent=newName;document.getElementById('editCollectionOptions').innerHTML=choiceOptions([{id:'',name:'เลือกคอลเลกชัน'},...data.collections],collectionId);document.getElementById('newCollection').value='';}
    if(newSave){await save(ids,collectionId);if(version!==generation)return true;}
    for(const post of ids){if(!await mutate({operation:'item',post,collectionId,note,viewed}))return true;}
    if(collectionId)remember(collectionId);
    if(b.isConnected&&document.getElementById('sheet').open)closeSheet();render();toast(collectionId?`เก็บใน “${name(collectionId)}” แล้ว`:'บันทึกโน้ตแล้ว');
   }finally{b.disabled=false}return true;
  }
  return false;
 }
 function change(el){if(el.hasAttribute('data-collection-filter')){collection=el.value;render();return true}return false}
 async function toggleViewed(id){
  if(!loaded){await load();if(!loaded)return false}
  const d=detail(id);if(await mutate({operation:'item',post:id,collectionId:d.collectionId,note:d.note,viewed:!d.viewed})){render();toast(d.viewed?'ทำเครื่องหมายว่ายังไม่ได้ดูแล้ว':'ทำเครื่องหมายว่าดูแล้ว');return true}return false;
 }
 return {ready:()=>loaded||isPreview(),edit:editor,detail,toggleViewed,saveNew,load,toolbar,filter,caption,click,change,clearFilters(){collection='all';status='all'},retain(ids){data.items=data.items.filter(x=>ids.has(x.postId))},reset(){generation++;data={collections:[],items:[]};collection='all';status='all';loaded=false;loading=false;error=false;editing=null;pendingIds=[];recentId=null;recentAt=0}};
}
