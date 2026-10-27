import {dismissOnBackdrop} from './modal-dismiss.mjs';
// Pointer hold, secondary click and keyboard all open the same message menu.
export function installSavedContextMenu({enabled,items,run}) {
 const dialog=document.createElement('dialog');dialog.className='saved-context-layer';dialog.setAttribute('aria-label','เมนูข้อความ');document.body.append(dialog);
 let hold=null,timer=null,origin=null,swallow=false,serial=0;
 const cardFor=e=>e.target.closest?.('[data-saved-card], [data-post-card], .media-card[data-post]');
 function cancelHold(){clearTimeout(timer);timer=null;hold=null}
 function close(){serial++;cancelHold();if(dialog.open)dialog.close();if(origin?.isConnected)origin.focus({preventScroll:true})}
 async function open(card,x,y){
  if(!enabled()||!card?.isConnected)return;
  cancelHold();const token=++serial;origin=card.querySelector('[data-post]')||card;
  const id=card.dataset.savedCard||card.dataset.postCard||card.dataset.post,actions=await items(id);
  if(token!==serial||!enabled()||!card.isConnected||!actions?.length)return;
  dialog.replaceChildren();const menu=document.createElement('div');menu.className='saved-context-menu';menu.setAttribute('role','menu');menu.setAttribute('aria-label','จัดการข้อความ');
  for(const action of actions){const button=document.createElement('button');button.type='button';button.setAttribute('role','menuitem');button.dataset.contextAction=action.id;button.className=action.destructive?'destructive':'';button.textContent=action.label;button.onclick=()=>{close();Promise.resolve(run(action.id,id)).catch(()=>{})};menu.append(button)}
  dialog.append(menu);if(!dialog.open)dialog.showModal();
  const viewport=window.visualViewport, left=viewport?.offsetLeft||0,top=viewport?.offsetTop||0,width=viewport?.width||innerWidth,height=viewport?.height||innerHeight;
  const r=menu.getBoundingClientRect();menu.style.left=`${Math.max(left+12,Math.min(x,left+width-r.width-12))}px`;menu.style.top=`${Math.max(top+12,Math.min(y,top+height-r.height-12))}px`;
  menu.querySelector('button')?.focus({preventScroll:true});
 }
 document.addEventListener('pointerdown',e=>{
  // A fresh gesture must never be swallowed by the preceding long press.
  serial++;swallow=false;if(hold){cancelHold();return}if(!e.isPrimary||e.button!==0||!enabled()||dialog.open)return;
  const card=cardFor(e);if(!card)return;hold={id:e.pointerId,x:e.clientX,y:e.clientY,card};
  timer=setTimeout(()=>{const h=hold;if(!h)return;swallow=true;open(h.card,h.x,h.y).catch(()=>{});},550);
 },true);
 document.addEventListener('pointermove',e=>{if(hold&&(Math.hypot(e.clientX-hold.x,e.clientY-hold.y)>10))cancelHold()},true);
 for(const event of ['pointerup','pointercancel'])document.addEventListener(event,cancelHold,true);
 document.addEventListener('scroll',e=>{cancelHold();if(e.target!==dialog&&!dialog.contains(e.target)){serial++;if(dialog.open)close()}},true);
 document.addEventListener('click',e=>{if(swallow){swallow=false;e.preventDefault();e.stopImmediatePropagation();return}const button=e.target.closest('[data-saved-menu]');if(button){e.preventDefault();e.stopImmediatePropagation();const r=button.getBoundingClientRect();open(button.closest('[data-saved-card], [data-post-card], .media-card[data-post]'),r.left,r.bottom).catch(()=>{})}},true);
 document.addEventListener('contextmenu',e=>{const card=cardFor(e);if(!card||!enabled())return;e.preventDefault();cancelHold();if(!dialog.open)open(card,e.clientX,e.clientY).catch(()=>{})});
 document.addEventListener('keydown',e=>{
  if(dialog.open){const buttons=[...dialog.querySelectorAll('button')],i=buttons.indexOf(document.activeElement);if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();buttons[e.key==='Home'?0:e.key==='End'?buttons.length-1:(i+(e.key==='ArrowDown'?1:buttons.length-1))%buttons.length]?.focus()}return}
  if(e.key==='ContextMenu'||(e.shiftKey&&e.key==='F10')){const card=cardFor(e);if(card&&enabled()){e.preventDefault();const r=card.getBoundingClientRect();open(card,r.left+20,r.top+20).catch(()=>{})}}
 });
 dismissOnBackdrop(dialog,close,()=>dialog.querySelector('.saved-context-menu'));dialog.addEventListener('cancel',e=>{e.preventDefault();close()});
 window.addEventListener('resize',close);window.addEventListener('luma:suspend',close);
 document.addEventListener('visibilitychange',()=>{if(document.hidden)close()});
 return {close};
}
