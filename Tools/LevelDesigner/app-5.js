function placeAt(tool,pos){
 const r=currentRoom(),p=tool==="prop"?snapToRoomCellCenter(r,pos):snapPoint(pos),a=assetForTool(tool);
 if(tool==="player"){r.playerStart={position:p,rotation:0};state.editor.selectedId=null;return}
 if(tool==="door"){
  const hw=r.bounds.width/2,hh=r.bounds.height/2;let side,pp=[...p],rot=0;
  const distances=[["East",Math.abs(hw-p[0])],["West",Math.abs(-hw-p[0])],["North",Math.abs(hh-p[1])],["South",Math.abs(-hh-p[1])]].sort((x,y)=>x[1]-y[1]);side=distances[0][0];
  if(side==="East"){pp[0]=hw-1;rot=90}else if(side==="West"){pp[0]=-hw+1;rot=90}else if(side==="North"){pp[1]=hh-1;rot=0}else{pp[1]=-hh+1;rot=0}
  const d={id:uid("door"),kind:"door",position:pp,rotation:rot,side,placementMode:"Fixed",traversable:true,visibleOnMap:true,runtimeObject:a?.id||"door.room-standard",openWhen:"room-complete"};
  r.doors.push(d);state.editor.selectedId=d.id;return;
 }
 if(tool==="teleporter"){
  const e={id:uid("teleporter"),kind:"teleporter",position:p,rotation:0,pairId:"A",enabled:true};r.entities.push(e);state.editor.selectedId=e.id;return;
 }
 const kind=tool==="enemy"?"enemy":"prop";
 const e={id:uid(kind),kind,object:a?.id||"",position:p,rotation:0};
 if(kind==="enemy"){e.level=1;e.dropChance=100;e.optional=false}else{e.dropChance=0;e.blocksMovement=true;e.layer="default"}
 r.entities.push(e);state.editor.selectedId=e.id;
}
canvas.addEventListener("contextmenu",e=>e.preventDefault());
canvas.addEventListener("dblclick",e=>{
 if(state.editor.viewMode!=="map")return;const hit=mapHitTest(eventPoint(e));if(hit?.room)openRoomEditor(hit.room)
});
canvas.addEventListener("pointerdown",e=>{
 canvas.setPointerCapture(e.pointerId);pointer.down=true;pointer.last=eventPoint(e);gestureSnapshot=snapshot();pointer.lastTileKey=null;pointer.lastPlacementKey=null;pointer.moved=false;
 if(state.editor.viewMode==="map"){
  if(e.button===1||e.altKey){pointer.mode="pan";canvas.style.cursor="grabbing";return}
  const hit=mapHitTest(pointer.last),wp=screenToWorld(pointer.last);
  if(state.editor.mapMode==="open"){
   if(hit?.room){openRoomEditor(hit.room);pointer.down=false;gestureSnapshot=null}
   else{pointer.mode="pan";canvas.style.cursor="grabbing"}
   return
  }
  if(state.editor.mapMode==="connect"){
   if(hit?.type==="door"){state.activeRoomId=hit.room.id;state.editor.selectedId=hit.door.id;pointer.mode="connect-door";pointer.sourceDoorId=hit.door.id;renderAll()}
   else if(hit?.room){state.activeRoomId=hit.room.id;state.editor.selectedId=null;renderAll()}
   else{pointer.mode="pan";canvas.style.cursor="grabbing"}
   return
  }
  if(state.editor.mapMode==="arrange"){
   if(hit?.type==="room"){state.activeRoomId=hit.room.id;state.editor.selectedId=null;pointer.mode="drag-map-room";const c=mapRoomCenter(hit.room);pointer.mapRoomOffset=[wp[0]-c[0],wp[1]-c[1]];renderAll()}
   else{pointer.mode="pan";canvas.style.cursor="grabbing"}
   return
  }
 }
 if(e.button===1||state.editor.tool==="pan"||e.altKey){pointer.mode="pan";canvas.style.cursor="grabbing";return}
 const rawWorld=screenToWorld(pointer.last),wp=snapPoint(rawWorld);
 if(state.editor.tool==="select"){
   const hit=hitTest(pointer.last);state.editor.selectedId=hit?.id||null;pointer.mode=hit?"drag":"select";if(hit)pointer.dragOffset=[wp[0]-hit.position[0],wp[1]-hit.position[1]];renderAll();return;
 }
 if(state.editor.tool==="wall"){pointer.mode="wall";pointer.wallStart=wp;return}
 if(state.editor.tool==="tile"||state.editor.tool==="tile-erase"||e.button===2){
   const cell=tileCellFromWorld(currentRoom(),rawWorld),erase=state.editor.tool==="tile-erase"||e.button===2;
   setRoomTile(currentRoom(),cell,erase?null:selectedFloorObject());pointer.lastTileKey=cell?.key||null;
   pointer.mode=state.editor.placementMode==="paint"?"tile-paint":"placed";renderCanvas();renderHeaderFields();renderFooter();return
 }
 const paintable=["enemy","prop"].includes(state.editor.tool);
 if(paintable&&state.editor.placementMode==="paint"){
   pointer.mode="entity-paint";placeAt(state.editor.tool,rawWorld);pointer.lastPlacementKey=placementKey(rawWorld);renderAll();return
 }
 placeAt(state.editor.tool,wp);pointer.mode="placed";renderAll();
});
canvas.addEventListener("pointermove",e=>{
 pointer.last=eventPoint(e);if(!pointer.down){if(pointer.wallStart)renderCanvas();return}
 if(Math.abs(e.movementX)+Math.abs(e.movementY)>2)pointer.moved=true;
 const dx=e.movementX,dy=e.movementY;
 if(pointer.mode==="pan"){state.editor.pan[0]+=dx;state.editor.pan[1]+=dy;saveCurrentView();renderCanvas();renderFooter();return}
 if(pointer.mode==="connect-door"){renderCanvas();return}
 if(pointer.mode==="drag-map-room"){
  const r=currentRoom(),wp=screenToWorld(pointer.last),gx=Math.round((wp[0]-pointer.mapRoomOffset[0])/MAP_SPACING[0]),gy=Math.round((wp[1]-pointer.mapRoomOffset[1])/MAP_SPACING[1]);r.grid=[gx,gy];renderCanvas();renderRooms();return
 }
 if(pointer.mode==="tile-paint"){
  const cell=tileCellFromWorld(currentRoom(),screenToWorld(pointer.last));if(cell&&cell.key!==pointer.lastTileKey){setRoomTile(currentRoom(),cell,state.editor.tool==="tile-erase"?null:selectedFloorObject());pointer.lastTileKey=cell.key;renderCanvas();renderHeaderFields();renderFooter()}return
 }
 if(pointer.mode==="entity-paint"){
  const raw=screenToWorld(pointer.last),key=placementKey(raw);
  if(key!==pointer.lastPlacementKey){placeAt(state.editor.tool,raw);pointer.lastPlacementKey=key;renderCanvas();renderInspector();renderFooter()}
  return
 }
 if(pointer.mode==="drag"){
  const sel=selected(),raw=screenToWorld(pointer.last);
  if(sel){
   const o=sel.entity||sel.door;
   if(sel.entity?.kind==="prop")o.position=snapToRoomCellCenter(currentRoom(),raw);
   else{const p=snapPoint(raw);o.position=[p[0]-pointer.dragOffset[0],p[1]-pointer.dragOffset[1]]}
   renderCanvas();renderInspector();renderFooter()
  }
 }else if(pointer.mode==="wall")renderCanvas();
});
canvas.addEventListener("pointerup",e=>{
 if(pointer.mode==="connect-door"&&pointer.sourceDoorId){
  const hit=mapHitTest(pointer.last),source=findDoor(pointer.sourceDoorId);
  if(hit?.type==="door"&&hit.door.id!==pointer.sourceDoorId&&hit.room.id!==source?.room.id){
   const duplicate=state.connections.some(c=>(c.fromDoorId===pointer.sourceDoorId&&c.toDoorId===hit.door.id)||(c.toDoorId===pointer.sourceDoorId&&c.fromDoorId===hit.door.id));
   if(!duplicate)state.connections.push({id:uid("connection"),fromDoorId:pointer.sourceDoorId,toDoorId:hit.door.id,travelPolicy:"Bidirectional"});else setStatus("Those doors are already connected.","warn")
  }
 }
 if(pointer.mode==="wall"&&pointer.wallStart){
   const a=pointer.wallStart,b=snapPoint(screenToWorld(pointer.last)),dx=b[0]-a[0],dy=b[1]-a[1],len=Math.hypot(dx,dy);
   if(len>.1){const asset=assetForTool("wall"),wall={id:uid("wall"),kind:"wall",object:asset?.id||"",position:[(a[0]+b[0])/2,(a[1]+b[1])/2],rotation:round(Math.atan2(dy,dx)*180/Math.PI),length:round(len),thickness:.5,height:1};currentRoom().entities.push(wall);state.editor.selectedId=wall.id}
 }
 if(["drag","wall","pan","tile-paint","entity-paint","drag-map-room","connect-door","placed"].includes(pointer.mode)&&gestureSnapshot&&gestureSnapshot!==snapshot())pushHistory(gestureSnapshot);
 pointer.down=false;pointer.mode=null;pointer.wallStart=null;pointer.sourceDoorId=null;pointer.lastTileKey=null;pointer.lastPlacementKey=null;pointer.moved=false;gestureSnapshot=null;saveCurrentView();canvas.style.cursor=state.editor.viewMode==="map"?"default":state.editor.tool==="pan"?"grab":state.editor.tool==="select"?"default":"crosshair";renderAll();
});
canvas.addEventListener("wheel",e=>{
 e.preventDefault();const before=screenToWorld(eventPoint(e)),factor=e.deltaY<0?1.12:.89;state.editor.zoom=clamp(state.editor.zoom*factor,6,120);const after=worldToScreen(before),pt=eventPoint(e);state.editor.pan[0]+=pt[0]-after[0];state.editor.pan[1]+=pt[1]-after[1];saveCurrentView();renderCanvas();renderFooter()
},{passive:false});

function validate(){
 const issues=[],err=(m,p="")=>issues.push({severity:"error",message:m,path:p}),warn=(m,p="")=>issues.push({severity:"warning",message:m,path:p});
 if(!state.level.id)err("Level ID is required.","level.id");
 if(!state.rooms.some(r=>r.id===state.level.startRoomId))err("Start room does not exist.","level.startRoomId");
 if(!state.rooms.some(r=>r.id===state.level.finalRoomId))err("Final room does not exist.","level.finalRoomId");
 const ids=new Set(),catalogIds=new Set(state.catalog.map(a=>a.id));
 state.rooms.forEach((r,ri)=>{
  if(ids.has(r.id))err(`Duplicate room ID ${r.id}.`,`rooms[${ri}]`);ids.add(r.id);
  if(r.bounds.width<=0||r.bounds.height<=0)err(`Room ${r.id} has invalid size.`);
  if(!r.playerStart&&r.id===state.level.startRoomId)err(`Start room ${r.id} needs a player start.`);
  if(r.tileGridEnabled&&r.tiles.length===0)warn(`Room ${r.id} has an enabled tile grid but no painted floor cells.`);
  const instanceIds=new Set();
  [...r.entities,...r.doors].forEach(o=>{if(instanceIds.has(o.id))err(`Duplicate instance ID ${o.id} in ${r.id}.`);instanceIds.add(o.id)});
  r.entities.forEach(e=>{
   if(["enemy","prop","wall"].includes(e.kind)&&!e.object)err(`${e.id} has no runtime object ID.`);
   if(e.object&&!catalogIds.has(e.object))warn(`${e.id} uses ${e.object}, which was not found in the scanned catalogue.`);
   if(e.kind==="enemy"&&(e.level<1||e.level>100))err(`${e.id} enemy level must be 1–100.`);
   if(e.dropChance<0||e.dropChance>100)err(`${e.id} drop chance must be 0–100%.`);
   if(e.kind==="teleporter")warn(`${e.id} is preserved but not emitted into the current runtime package.`);
  });
  r.doors.forEach(d=>{if(!d.runtimeObject)err(`${d.id} has no runtime door object.`);if(d.runtimeObject&&!catalogIds.has(d.runtimeObject))warn(`${d.id} uses unscanned door object ${d.runtimeObject}.`);if(!["always","room-complete"].includes(d.openWhen||"always"))warn(`${d.id} uses ${d.openWhen}; this rule is preserved in editor metadata but not emitted to the current runtime encounter schema.`)});
 });
 const allDoorIds=new Set(state.rooms.flatMap(r=>r.doors.map(d=>d.id)));
 state.connections.forEach(c=>{if(!allDoorIds.has(c.fromDoorId))err(`${c.id} has an invalid source door.`);if(!allDoorIds.has(c.toDoorId))err(`${c.id} has an invalid destination door.`);if(c.fromDoorId===c.toDoorId)err(`${c.id} connects a door to itself.`)});
 if(!state.level.finalExitDoorId)err("A final exit door is required.","level.finalExitDoorId");
 if(state.level.finalExitDoorId&&!allDoorIds.has(state.level.finalExitDoorId))err("Final exit door does not exist.");
 const tele=state.rooms.flatMap(r=>r.entities.filter(e=>e.kind==="teleporter"));const pairs={};tele.forEach(t=>(pairs[t.pairId]??=[]).push(t));Object.entries(pairs).forEach(([id,v])=>{if(v.length!==2)warn(`Teleporter pair ${id} has ${v.length} endpoints; expected 2.`)});
 return issues;
}
function showValidation(){
 const issues=validate(),errors=issues.filter(x=>x.severity==="error").length,warnings=issues.length-errors;
 $("#validationSummary").innerHTML=errors===0?`<div class="notice">${warnings?`No blocking errors; ${warnings} warning(s).`:"Everything looks compiler-ready."}</div>`:`<div class="notice error">${errors} blocking error(s), ${warnings} warning(s).</div>`;
 $("#validationList").innerHTML=issues.map(i=>`<div class="validation-item ${i.severity}"><b>${i.severity.toUpperCase()}</b> ${esc(i.message)} ${i.path?`<div class="help">${esc(i.path)}</div>`:""}</div>`).join("")||`<div class="validation-item">No issues found.</div>`;
 $("#validationDialog").showModal();return {issues,errors,warnings};
}
