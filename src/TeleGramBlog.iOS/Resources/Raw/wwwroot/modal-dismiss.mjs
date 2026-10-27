// Dismiss only a complete tap on the backdrop, never padding or a drag from the form.
export function dismissOnBackdrop(dialog,cancel,surface=()=>dialog){
 let gesture=null;
 const outside=e=>{const element=surface();if(!element)return false;const r=element.getBoundingClientRect();return e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom};
 dialog.addEventListener('pointerdown',e=>{gesture=e.isPrimary&&e.button===0?{id:e.pointerId,x:e.clientX,y:e.clientY,outside:outside(e),released:false}:null});
 dialog.addEventListener('pointermove',e=>{if(gesture&&Math.hypot(e.clientX-gesture.x,e.clientY-gesture.y)>10)gesture=null});
 dialog.addEventListener('pointerup',e=>{if(gesture&&gesture.id===e.pointerId)gesture.released=outside(e)});
 dialog.addEventListener('pointercancel',()=>{gesture=null});
 dialog.addEventListener('close',()=>{gesture=null});
 dialog.addEventListener('click',e=>{const dismiss=gesture?.outside&&gesture.released&&outside(e);gesture=null;if(dismiss&&dialog.open){e.preventDefault();e.stopImmediatePropagation();cancel()}},true);
}
