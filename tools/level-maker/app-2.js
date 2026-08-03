function renderLogic(){
 $("#connectionList").innerHTML=state.level.connections.map((c,i)=>`<div class="logic-item" data-connection="${esc(c.id)}">
   <div class="section-title">${esc(c.id)}</div>
   <label>From</label><select data-field="from">${doorOptions(c.fromDoorId)}</select>
   <label>To</label><select data-field="to">${doorOptions(c.toDoorId)}</select>
   <div class="row"><select data-field="policy" class="grow"><option ${c.travelPolicy==="Bidirectional"?"selected":""}>Bidirectional</option><option ${c.travelPolicy==="OneWay"?"selected":""}>OneWay</option></select><button data-delete>Delete</button></div>
 </div>`).join("")||`<div class="help">No room connections yet.</div>`;
 $$("#connectionList .logic-item").forEach(el=>{
  const c=state.level.connections.find(x=>x.id===el.dataset.connection);
  el.querySelector('[data-field=from]').onchange=e=>mutate(()=>c.fromDoorId=e.target.value);
  el.querySelector('[data-field=to]').onchange=e=>mutate(()=>c.toDoorId=e.target.value);
  el.querySelector('[data-field=policy]').onchange=e=>mutate(()=>c.travelPolicy=e.target.value);
  el.querySelector('[data-delete]').onclick=()=>mutate(()=>state.level.connections=state.level.connections.filter(x=>x!==c));
 });
 $("#logicList").innerHTML=state.level.logic.map(l=>`<div class="logic-item" data-logic="${esc(l.id)}">
  <div class="row"><input data-field="name" value="${esc(l.name)}"><button data-delete>Delete</button></div>
  <label>When</label><select data-field="when"><option ${l.when==="switch-activated"?"selected":""}>switch-activated</option><option ${l.when==="room-complete"?"selected":""}>room-complete</option><option ${l.when==="enemy-count-zero"?"selected":""}>enemy-count-zero</option><option ${l.when==="player-enters-trigger"?"selected":""}>player-enters-trigger</option></select>
  <label>Target entity / door ID</label><input data-field="target" value="${esc(l.targetId||"")}">
  <label>Action</label><select data-field="action"><option ${l.action==="open-door"?"selected":""}>open-door</option><option ${l.action==="close-door"?"selected":""}>close-door</option><option ${l.action==="enable-entity"?"selected":""}>enable-entity</option><option ${l.action==="disable-entity"?"selected":""}>disable-entity</option><option ${l.action==="teleport"?"selected":""}>teleport</option></select>
 </div>`).join("")||`<div class="help">No custom rules.</div>`;
 $$("#logicList .logic-item").forEach(el=>{
  const l=state.level.logic.find(x=>x.id===el.dataset.logic);
  ["name","when","target","action"].forEach(k=>el.querySelector(`[data-field=${k}]`).onchange=e=>mutate(()=>l[k==="target"?"targetId":k]=e.target.value));
  el.querySelector("[data-delete]").onclick=()=>mutate(()=>state.level.logic=state.level.logic.filter(x=>x!==l));
 });
}
function renderFooter(){
 const r=currentRoom(),en=r.entities.filter(e=>e.kind==="enemy").length,pr=r.entities.filter(e=>e.kind!=="enemy").length;
 $("#counts").textContent=`${state.level.rooms.length} room(s) · active: ${en} enemies, ${pr} objects, ${r.doors.length} doors`;
 $("#view-hud").textContent=`${state.editor.viewMode==="map"?"MAP":"ROOM"} · ${Math.round(state.editor.zoom/32*100)}% · snap ${state.editor.snap?state.editor.snapSize:"off"}`;
 const sel=selected();$("#selection-hud").textContent=sel?(sel.entity?.id||sel.door?.id):"Nothing selected";
}
function bindChange(selector,fn){const el=$(selector);el.onchange=()=>mutate(()=>fn(el.value,el))}
function commonTransformInspector(e){
 return `<div class="section"><div class="section-title">Transform</div>
 <div class="grid2"><div><label>X</label><input data-i="x" type="number" step=".25" value="${e.position[0]}"></div><div><label>Y</label><input data-i="y" type="number" step=".25" value="${e.position[1]}"></div></div>
 <label>Rotation (degrees)</label><input data-i="rotation" type="number" step="15" value="${e.rotation||0}">
 </div>`;
}
function renderInspector(){
 const wrap=$("#inspector"),sel=selected(),r=currentRoom();
 if(!sel){
  wrap.innerHTML=`<div class="panel"><h2>Active room</h2>
   <label>Room ID</label><input data-r="id" value="${esc(r.id)}">
   <label>Display name</label><input data-r="displayName" value="${esc(r.displayName)}">
   <div class="grid2"><div><label>Map grid X</label><input data-r="gridX" type="number" value="${r.grid[0]}"></div><div><label>Map grid Y</label><input data-r="gridY" type="number" value="${r.grid[1]}"></div></div>
   <div class="grid2"><div><label>Width</label><input data-r="width" type="number" min="2" step="1" value="${r.bounds.width}"></div><div><label>Height</label><input data-r="height" type="number" min="2" step="1" value="${r.bounds.height}"></div></div>
   <label>Default floor object</label><input data-r="floorObject" value="${esc(FloorData.defaultFloorTile(r))}">
   <div class="section"><div class="section-title">Tile grid · 1 × 1 world units</div>
    <div class="tile-chip"><span class="tile-swatch" style="background:${tileColor(selectedFloorObject())}"></span>${esc(selectedFloorObject()||"Select a floor asset")}</div>
    <div class="row" style="margin-top:8px"><button data-action="fill-tiles">Fill all cells</button><button data-action="clear-tiles">Clear all cells</button></div>
    <div class="help" style="margin-top:6px">Choose a floor asset, then paint with <kbd>F</kbd>. Use <kbd>X</kbd> or right-click to erase.</div>
   </div>
   <label><input data-r="visible" type="checkbox" ${r.visibleOnMap!==false?"checked":""}> Visible on map</label>
   <hr><div class="row"><button data-action="center">Center view</button><button data-action="map">Show level map</button><button class="danger" data-action="delete-room" ${state.level.rooms.length===1?"disabled":""}>Delete room</button></div>
   <div class="notice">Select an object to edit its gameplay properties. Place content from the catalogue on the left.</div>
  </div>`;
  wrap.querySelectorAll("[data-r]").forEach(el=>el.onchange=()=>mutate(()=>{
   const k=el.dataset.r;
   if(k==="id"){const old=r.id;r.id=el.value;state.editor.activeRoomId=r.id;if(state.level.startRoomId===old)state.level.startRoomId=r.id;if(state.level.finalRoomId===old)state.level.finalRoomId=r.id}
   else if(k==="gridX")r.grid[0]=+el.value;else if(k==="gridY")r.grid[1]=+el.value;
   else if(k==="width")r.bounds.width=Math.max(2,Math.round(+el.value));else if(k==="height")r.bounds.height=Math.max(2,Math.round(+el.value));
   else if(k==="visible")r.visibleOnMap=el.checked;else if(k==="floorObject")FloorData.setDefaultFloorTile(r,el.value);else r[k]=el.value;
  }));
  wrap.querySelector("[data-action=center]").onclick=()=>{state.editor.pan=[0,0];fitRoom();renderCanvas()};
  wrap.querySelector("[data-action=map]").onclick=()=>{setViewMode("map",{focus:false});fitMap();renderAll()};
  wrap.querySelector("[data-action=fill-tiles]").onclick=()=>mutate(()=>FloorData.fillFloor(r,selectedFloorObject()||FloorData.defaultFloorTile(r)));
  wrap.querySelector("[data-action=clear-tiles]").onclick=()=>mutate(()=>FloorData.clearFloor(r));
  const del=wrap.querySelector("[data-action=delete-room]");if(del)del.onclick=()=>mutate(()=>deleteActiveRoom());
  return;
 }
 if(sel.entity){
  const e=sel.entity;
  let specific="";
  if(e.kind==="enemy")specific=`<div class="section"><div class="section-title">Enemy</div>
    <label>Enemy object</label><input data-i="object" value="${esc(e.object)}">
    <div><label>Enemy tier</label><select data-i="tier">${[1,2,3,4].map(t=>`<option value="${t}" ${Number(e.tier||1)===t?"selected":""}>Tier ${t}</option>`).join("")}</select></div>
    <label>Drop profile override (optional)</label><input data-i="dropProfile" value="${esc(e.dropProfile||"")}">
    <label><input data-i="optional" type="checkbox" ${e.optional?"checked":""}> Optional for room completion</label>
   </div>`;
  else if(e.kind==="wall")specific=`<div class="section"><div class="section-title">Wall</div>
    <label>Runtime prop object</label><input data-i="object" value="${esc(e.object||"")}">
    <div class="grid2"><div><label>Length</label><input data-i="length" type="number" min=".1" step=".25" value="${e.length||1}"></div><div><label>Thickness</label><input data-i="thickness" type="number" min=".05" step=".05" value="${e.thickness||.5}"></div></div>
    <label>Height / visual scale</label><input data-i="height" type="number" min=".1" step=".1" value="${e.height||1}">
   </div>`;
  else if(e.kind==="teleporter")specific=`<div class="section"><div class="section-title">Teleporter (future runtime)</div>
    <label>Pair ID</label><input data-i="pairId" value="${esc(e.pairId||"A")}">
    <label><input data-i="enabled" type="checkbox" ${e.enabled!==false?"checked":""}> Enabled</label>
    <div class="notice warn">Preserved in editor metadata. The current Shooter Mover room compiler has no teleporter sidecar yet.</div>
   </div>`;
  else specific=`<div class="section"><div class="section-title">Prop</div>
    <label>Prop object</label><input data-i="object" value="${esc(e.object||"")}">
    <div><label>Layer</label><input data-i="layer" value="${esc(e.layer||"default")}"></div>
    <label><input data-i="blocksMovement" type="checkbox" ${e.blocksMovement!==false?"checked":""}> Blocks movement</label>
   </div>`;
  wrap.innerHTML=`<div class="panel"><h2>${esc(e.kind)} entity</h2><label>Instance ID</label><input data-i="id" value="${esc(e.id)}">${commonTransformInspector(e)}${specific}<button class="danger" data-action="delete">Delete entity</button></div>`;
  wireInspectorEntity(e,wrap);return;
 }
 const d=sel.door;
 wrap.innerHTML=`<div class="panel"><h2>Door</h2><label>Door ID</label><input data-d="id" value="${esc(d.id)}">${commonTransformInspector(d)}
 <div class="section"><div class="section-title">Runtime</div>
  <label>Runtime object</label><input data-d="runtimeObject" value="${esc(d.runtimeObject||"door.room-standard")}">
  <div class="grid2"><div><label>Side</label><select data-d="side">${["North","East","South","West"].map(x=>`<option ${d.side===x?"selected":""}>${x}</option>`).join("")}</select></div>
  <div><label>Open when</label><select data-d="openWhen">${["always","room-complete","switch-activated","key-collected"].map(x=>`<option ${d.openWhen===x?"selected":""}>${x}</option>`).join("")}</select></div></div>
  <label><input data-d="traversable" type="checkbox" ${d.traversable!==false?"checked":""}> Traversable</label>
  <label><input data-d="visibleOnMap" type="checkbox" ${d.visibleOnMap!==false?"checked":""}> Visible on map</label>
  <label>Required key / switch ID</label><input data-d="requirementId" value="${esc(d.requirementId||"")}">
  <label>Auto-close delay (seconds)</label><input data-d="autoCloseDelay" type="number" min="0" step=".1" value="${d.autoCloseDelay||0}">
 </div><button class="danger" data-action="delete">Delete door</button></div>`;
 wrap.querySelectorAll("[data-d], [data-i]").forEach(el=>el.onchange=()=>mutate(()=>{
   const k=el.dataset.d||el.dataset.i,v=el.type==="checkbox"?el.checked:(el.type==="number"?+el.value:el.value);
   if(k==="x")d.position[0]=v;else if(k==="y")d.position[1]=v;else d[k]=v;
 }));
 wrap.querySelector("[data-action=delete]").onclick=()=>mutate(()=>deleteSelected());
}
function wireInspectorEntity(e,wrap){
 wrap.querySelectorAll("[data-i]").forEach(el=>el.onchange=()=>mutate(()=>{
   const k=el.dataset.i,v=el.type==="checkbox"?el.checked:(el.type==="number"?+el.value:el.value);
   if(k==="x")e.position[0]=v;else if(k==="y")e.position[1]=v;else e[k]=v;
 }));
 wrap.querySelector("[data-action=delete]").onclick=()=>mutate(()=>deleteSelected());
}
function deleteSelected(){
 const id=state.editor.selectedId;if(!id)return;
 for(const r of state.level.rooms){r.entities=r.entities.filter(e=>e.id!==id);r.doors=r.doors.filter(d=>d.id!==id)}
 state.level.connections=state.level.connections.filter(c=>c.fromDoorId!==id&&c.toDoorId!==id);
 state.editor.selectedId=null;
}
