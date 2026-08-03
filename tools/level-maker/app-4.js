function drawWall(e){
 withTransform(e,()=>{
  const len=(e.length||1)*state.editor.zoom,th=Math.max(4,(e.thickness||.5)*state.editor.zoom);
  ctx.fillStyle="#718096";ctx.strokeStyle=isSelected(e.id)?"#fff":"#9eacbe";ctx.lineWidth=isSelected(e.id)?3:1;
  ctx.fillRect(-len/2,-th/2,len,th);ctx.strokeRect(-len/2,-th/2,len,th);
 });
}
function drawDoor(d){
 withTransform(d,()=>{
  const w=1.5*state.editor.zoom,h=.35*state.editor.zoom;ctx.fillStyle="#ffd166";ctx.strokeStyle=isSelected(d.id)?"#fff":"#6b5724";ctx.lineWidth=isSelected(d.id)?3:1;ctx.fillRect(-w/2,-h/2,w,h);ctx.strokeRect(-w/2,-h/2,w,h)
 });labelEntity(d);
}
function hitTest(screen){
 const p=screenToWorld(screen),r=currentRoom(),candidates=[...r.doors,...r.entities].reverse(),z=state.editor.zoom;
 for(const e of candidates){
  const dx=p[0]-e.position[0],dy=p[1]-e.position[1],a=-deg2rad(e.rotation||0),lx=dx*Math.cos(a)-dy*Math.sin(a),ly=dx*Math.sin(a)+dy*Math.cos(a);
  if(e.kind==="wall"){if(Math.abs(lx)<=((e.length||1)/2+.2)&&Math.abs(ly)<=((e.thickness||.5)/2+.25))return e}
  else if(e.object==="prop.wall-1x1"||e.object==="prop.wall-2x2"){const half=e.object.endsWith("2x2")?1:.5;if(Math.abs(lx)<=half&&Math.abs(ly)<=half)return e}
  else if(Math.hypot(dx,dy)<=Math.max(.5,16/z))return e;
 }
 return null;
}
function eventPoint(e){const r=canvas.getBoundingClientRect();return[e.clientX-r.left,e.clientY-r.top]}
function fitRoom(){
 const r=currentRoom(),rect=canvas.getBoundingClientRect();state.editor.zoom=clamp(Math.min((rect.width-80)/r.bounds.width,(rect.height-80)/r.bounds.height),8,80);state.editor.pan=[0,0];saveCurrentView()
}
function fitMap(){
 const rect=canvas.getBoundingClientRect();if(state.level.rooms.length===1){state.editor.zoom=28;state.editor.pan=[0,0];saveCurrentView();return}
 const pts=state.level.rooms.map(mapRoomCenter),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]),w=Math.max(10,Math.max(...xs)-Math.min(...xs)+12),h=Math.max(8,Math.max(...ys)-Math.min(...ys)+9);
 state.editor.zoom=clamp(Math.min((rect.width-100)/w,(rect.height-100)/h),6,42);const center=[(Math.min(...xs)+Math.max(...xs))/2,(Math.min(...ys)+Math.max(...ys))/2];state.editor.pan=[-center[0]*state.editor.zoom,center[1]*state.editor.zoom];saveCurrentView()
}
function saveCurrentView(){const key=state.editor.viewMode==="map"?"mapView":"roomView";state.editor[key]={zoom:state.editor.zoom,pan:[...state.editor.pan]}}
function setViewMode(mode,{focus=true}={}){
 if(mode===state.editor.viewMode){
  if(mode==="room"){state.editor.focusRoom=focus;syncWorkspaceMode();requestAnimationFrame(resizeCanvas)}
  return
 }
 saveCurrentView();state.editor.viewMode=mode;
 if(mode==="room")state.editor.focusRoom=focus;
 const v=state.editor[mode==="map"?"mapView":"roomView"]||{zoom:mode==="map"?22:32,pan:[0,0]};
 state.editor.zoom=v.zoom;state.editor.pan=[...v.pan];state.editor.selectedId=null;closeDrawers();syncWorkspaceMode();
 canvas.style.cursor=mode==="map"?(state.editor.mapMode==="arrange"?"move":state.editor.mapMode==="connect"?"crosshair":"pointer"):(state.editor.tool==="pan"?"grab":state.editor.tool==="select"?"default":"crosshair");
 requestAnimationFrame(resizeCanvas);renderAll()
}
function openRoomEditor(room){
 if(!room)return;state.editor.activeRoomId=room.id;state.editor.selectedId=null;setViewMode("room",{focus:true});requestAnimationFrame(()=>{fitRoom();renderAll()})
}
function setTool(t){
 if(state.editor.viewMode!=="room")setViewMode("room",{focus:true});state.editor.tool=t;$$('[data-tool]').forEach(b=>b.classList.toggle("active",b.dataset.tool===t));canvas.style.cursor=t==="pan"?"grab":t==="select"?"default":"crosshair";renderFooter()
}
function assetForTool(tool){
 const a=state.assets.find(x=>x.id===state.editor.selectedAssetId);
 if(tool==="enemy"&&a?.type==="enemy")return a;
 if(tool==="prop"&&a?.type==="prop")return a;
 if(tool==="wall")return a?.type==="prop"&&a.id.startsWith("prop.wall-")
  ?a
  :state.assets.find(x=>x.id==="prop.wall-1x1");
 if(tool==="door"&&a?.type==="door")return a;
 if(tool==="tile"&&a?.type==="floor")return a;
 return null;
}
function placementKey(pos,tool=state.editor.tool){
 if(tool==="prop"||tool==="wall"){const p=snapToRoomCellCenter(currentRoom(),pos);return `cell:${p[0]},${p[1]}`}
 const p=snapPoint(pos),s=state.editor.snapSize||1;return `${Math.round(p[0]/s)},${Math.round(p[1]/s)}`
}
