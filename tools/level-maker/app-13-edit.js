"use strict";
(() => {
 let clipboard=null;
 const value=()=>{const s=selected();return s?.entity||s?.door||null};
 const fp=o=>{const a=state.catalog.find(x=>x.id===(o?.object||o));const m=`${a?.id||o?.object||o||""} ${a?.label||""}`.match(/(\d+)\s*[x×]\s*(\d+)/i);return m?{w:+m[1],h:+m[2]}:{w:1,h:1}};
 const rect=(p,f)=>({l:p[0]-f.w/2,r:p[0]+f.w/2,b:p[1]-f.h/2,t:p[1]+f.h/2});
 const overlap=(a,b)=>a.l<b.r-.001&&a.r>b.l+.001&&a.b<b.t-.001&&a.t>b.b+.001;
 const blocking=o=>o?.kind==="prop"||o?.kind==="wall";
 function snap(room,p,f){const c=Math.round(room.bounds.width),r=Math.round(room.bounds.height),w=Math.min(c,f.w),h=Math.min(r,f.h),x=-c/2,y=-r/2;return[x+clamp(Math.round(p[0]-x-w/2),0,c-w)+w/2,y+clamp(Math.round(p[1]-y-h/2),0,r-h)+h/2]}
 function free(room,p,f,ignore=""){const q=rect(p,f);return !room.entities.some(e=>e.id!==ignore&&blocking(e)&&overlap(q,rect(e.position,fp(e))))}
 function doorFree(room,p,ignore=""){return !room.doors.some(d=>d.id!==ignore&&Math.hypot(d.position[0]-p.position[0],d.position[1]-p.position[1])<.25)}
 function inspect(){document.body.classList.remove("left-drawer-open","tools-popover-open");document.body.classList.add("right-drawer-open","drawer-open");requestAnimationFrame(resizeCanvas)}
 function copy(){const o=value();if(!o)return false;clipboard={door:o.kind==="door",data:clone(o)};setStatus("Copied.","good");return true}
 function offsets(room,o,f){const s=Math.max(1,+state.editor.snapSize||1),a=[[1,0],[0,1],[-1,0],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1],[2,0],[0,2]];return a.map(v=>snap(room,[o.position[0]+v[0]*s,o.position[1]+v[1]*s],f))}
 function paste(){if(!clipboard){setStatus("Copy an object first.","warn");return false}const room=currentRoom(),src=clipboard.data;
  if(clipboard.door){const place=offsets(room,src,{w:1,h:1}).map(p=>doorEdgePlacement(room,p)).find(p=>doorFree(room,p));if(!place){setStatus("No free door position nearby.","warn");return false}mutate(()=>{const d=clone(src);d.id=uid("door");Object.assign(d,place);room.doors.push(d);state.editor.selectedId=d.id});inspect();return true}
  const f=src.kind==="enemy"||src.kind==="teleporter"?{w:1,h:1}:fp(src),p=blocking(src)?offsets(room,src,f).find(x=>free(room,x,f)):offsets(room,src,f)[0];if(!p){setStatus("No free space nearby.","warn");return false}mutate(()=>{const e=clone(src);e.id=uid(e.kind||"entity");e.position=p;room.entities.push(e);state.editor.selectedId=e.id});inspect();return true}
 function duplicate(){return copy()&&paste()}
 function nudge(dx,dy,big){const s=selected(),o=s?.entity||s?.door;if(!o||state.editor.viewMode!=="room")return false;const room=s.room,step=Math.max(.25,+state.editor.snapSize||1)*(big?4:1),raw=[o.position[0]+dx*step,o.position[1]+dy*step];
  if(s.door){const p=doorEdgePlacement(room,raw);if(!doorFree(room,p,o.id)){setStatus("Another door occupies that position.","warn");return true}mutate(()=>Object.assign(o,p));inspect();return true}
  const f=o.kind==="enemy"||o.kind==="teleporter"?{w:1,h:1}:fp(o),p=snap(room,raw,f);if(blocking(o)&&!free(room,p,f,o.id)){setStatus("That space is occupied.","warn");return true}mutate(()=>o.position=p);inspect();return true}
 document.addEventListener("keydown",e=>{if(["INPUT","TEXTAREA","SELECT"].includes(document.activeElement?.tagName))return;const k=e.key.toLowerCase(),stop=()=>{e.preventDefault();e.stopImmediatePropagation()};if((e.ctrlKey||e.metaKey)&&k==="c"){if(copy())stop();return}if((e.ctrlKey||e.metaKey)&&k==="v"){stop();paste();return}if((e.ctrlKey||e.metaKey)&&k==="d"){stop();duplicate();return}if(e.ctrlKey||e.metaKey||e.altKey)return;const d={arrowleft:[-1,0],arrowright:[1,0],arrowup:[0,1],arrowdown:[0,-1]}[k];if(d&&nudge(d[0],d[1],e.shiftKey))stop()},true);
})();
