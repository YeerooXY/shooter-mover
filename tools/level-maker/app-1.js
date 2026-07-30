"use strict";

const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];
const clone = x => structuredClone(x);
const uid = p => p + "." + ((crypto.randomUUID?.() || (`${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`)).slice(0,8));
const cleanSlug = s => (s||"Level1").trim().replace(/[^A-Za-z0-9_-]+/g,"-") || "Level1";
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const deg2rad=d=>d*Math.PI/180;
const round=(n,p=3)=>Number(Number(n).toFixed(p));
const safeId=(s,fallback)=>String(s||fallback).trim().replace(/\s+/g,"-").toLowerCase();
const iconFor=t=>({enemy:"👾",prop:"▣",floor:"▦",door:"▥",decor:"✦"}[t]||"◇");

const defaultCatalog = [
 {id:"enemy.moving-droid",label:"Moving Droid",type:"enemy",source:"EnemyCatalog"},
 {id:"enemy.mobile-blaster-droid",label:"Mobile Blaster Droid",type:"enemy",source:"EnemyCatalog"},
 {id:"enemy.ram-pouncer",label:"Ram Pouncer",type:"enemy",source:"EnemyCatalog"},
 {id:"enemy.blaster-turret",label:"Blaster Turret",type:"enemy",source:"EnemyCatalog"},
 {id:"enemy.pursuer-drone",label:"Pursuer Drone",type:"enemy",source:"EnemyCatalog"},
 {id:"enemy.hybrid-sentinel",label:"Hybrid Sentinel",type:"enemy",source:"EnemyCatalog"},
 {id:"tile.floor-industrial",label:"Industrial Floor",type:"floor",source:"Level1"},
 {id:"door.room-standard",label:"Standard Room Door",type:"door",source:"Level1"},
];

function newRoom(index=0){
 const rid=`room.level-1-${index===0?"start":"room-"+(index+1)}`;
 return {
   id:rid, displayName:index===0?"START ROOM":`ROOM ${index+1}`, grid:[index,0], slot:1,
   bounds:{width:24,height:14}, playerStart:index===0?{position:[-9,0],rotation:0}:null,
   floorObject:"tile.floor-industrial", tileGridEnabled:false, tiles:[], entities:[], doors:[],
   encounter:{completion:"all-enemies"}, visibleOnMap:true
 };
}
function initialState(){
 const r=newRoom(0);
 const exit={id:"door.level-1-final-exit",kind:"door",position:[11,0],rotation:90,side:"East",placementMode:"Fixed",traversable:true,visibleOnMap:true,runtimeObject:"door.room-standard",openWhen:"room-complete"};
 r.doors.push(exit);
 return {
  format:"shooter-mover-web-level-project",editorVersion:1,schemaVersion:2,
  level:{id:"level.level-1",name:"Level 1",targetFolder:"level-1",startRoomId:r.id,finalRoomId:r.id,finalExitDoorId:exit.id},
  rooms:[r],connections:[],logic:[],catalog:clone(defaultCatalog),activeRoomId:r.id,
  editor:{tool:"select",viewMode:"room",mapMode:"open",placementMode:"single",focusRoom:true,selectedId:null,selectedAssetId:"enemy.moving-droid",zoom:32,pan:[0,0],snap:true,snapSize:1,roomView:{zoom:32,pan:[0,0]},mapView:{zoom:22,pan:[0,0]}}
 };
}
let state=initialState(), history=[], future=[], gestureSnapshot=null;
let pointer={down:false,last:[0,0],mode:null,wallStart:null,dragOffset:[0,0],lastTileKey:null,lastPlacementKey:null,sourceDoorId:null,mapRoomOffset:[0,0],moved:false};
let canvas=$("#stage"), ctx=canvas.getContext("2d"), dpr=1;

function currentRoom(){return state.rooms.find(r=>r.id===state.activeRoomId)||state.rooms[0]}
function allEntities(){return state.rooms.flatMap(r=>r.entities)}
function findEntity(id){for(const r of state.rooms){const e=r.entities.find(x=>x.id===id);if(e)return {room:r,entity:e}} return null}
function findDoor(id){for(const r of state.rooms){const d=r.doors.find(x=>x.id===id);if(d)return {room:r,door:d}} return null}
function selected(){return findEntity(state.editor.selectedId)||findDoor(state.editor.selectedId)}
function snapshot(){return JSON.stringify({...state,editor:{...state.editor,pan:state.editor.pan}})}
function pushHistory(before){
 const now=before||snapshot(); if(history.at(-1)!==now) history.push(now);
 if(history.length>100)history.shift(); future.length=0; updateUndo();
}
function mutate(fn,{history:useHistory=true}={}){
 const before=useHistory?snapshot():null; fn(); if(useHistory)pushHistory(before); normalize(); renderAll();
}
function undo(){if(!history.length)return;future.push(snapshot());state=JSON.parse(history.pop());normalize();renderAll()}
function redo(){if(!future.length)return;history.push(snapshot());state=JSON.parse(future.pop());normalize();renderAll()}
function updateUndo(){$("#undoBtn").disabled=!history.length;$("#redoBtn").disabled=!future.length}
function normalize(){
 if(!state.catalog)state.catalog=clone(defaultCatalog);
 if(!state.rooms?.length){state.rooms=[newRoom(0)]}
 if(!state.rooms.some(r=>r.id===state.activeRoomId))state.activeRoomId=state.rooms[0].id;
 state.rooms.forEach((r,i)=>{
   r.bounds ||= {width:24,height:14};
   r.bounds.width=Math.max(2,Math.round(Number(r.bounds.width)||24));
   r.bounds.height=Math.max(2,Math.round(Number(r.bounds.height)||14));
   r.entities ||= []; r.doors ||= []; r.encounter ||= {completion:"all-enemies"};
   r.grid ||= [i,0]; r.slot ||= 1; r.floorObject ||= "tile.floor-industrial"; r.tiles ||= [];
   if(typeof r.tileGridEnabled!=="boolean")r.tileGridEnabled=r.tiles.length>0;
   const cols=Math.max(1,Math.round(r.bounds.width)),rows=Math.max(1,Math.round(r.bounds.height));
   r.tiles=r.tiles.filter(t=>Number.isInteger(t.x)&&Number.isInteger(t.y)&&t.x>=0&&t.y>=0&&t.x<cols&&t.y<rows&&t.object);
 });
 state.connections ||= [];state.logic ||= [];
 state.editor ||= initialState().editor;
 state.editor.viewMode ||= "room";
 state.editor.mapMode ||= "open";
 state.editor.placementMode ||= "single";
 if(typeof state.editor.focusRoom!=="boolean")state.editor.focusRoom=true;
 state.editor.roomView ||= {zoom:state.editor.zoom||32,pan:state.editor.pan||[0,0]};
 state.editor.mapView ||= {zoom:22,pan:[0,0]};
 state.level ||= initialState().level;
}

function setStatus(text,kind=""){
 const el=$("#status");el.textContent=text;el.className=kind?`status-${kind}`:"";
}
function renderAll(){renderHeaderFields();renderAssets();renderRooms();renderLogic();renderInspector();renderCanvas();renderFooter();updateUndo()}
function syncWorkspaceMode(){
 const focused=state.editor.viewMode==="room"&&state.editor.focusRoom!==false;
 document.body.classList.toggle("room-focus",focused);
 if(!focused)closeDrawers();
}
function closeDrawers(){
 document.body.classList.remove("left-drawer-open","right-drawer-open","drawer-open");
}
function toggleDrawer(side){
 const className=side==="left"?"left-drawer-open":"right-drawer-open";
 const other=side==="left"?"right-drawer-open":"left-drawer-open";
 const opening=!document.body.classList.contains(className);
 document.body.classList.remove(other);
 document.body.classList.toggle(className,opening);
 document.body.classList.toggle("drawer-open",opening);
 requestAnimationFrame(resizeCanvas);
}
function setMapMode(mode){
 state.editor.mapMode=mode;
 $$("[data-map-mode]").forEach(b=>b.classList.toggle("active",b.dataset.mapMode===mode));
 const help={open:"Click a room to edit it",arrange:"Drag rooms on the graph grid",connect:"Drag one door socket onto another"}[mode];
 $("#map-mode-help").textContent=help;
 canvas.style.cursor=mode==="arrange"?"move":mode==="connect"?"crosshair":"pointer";
 renderCanvas();renderFooter();
}
function setPlacementMode(mode){
 state.editor.placementMode=mode;
 $$("[data-placement-mode]").forEach(b=>b.classList.toggle("active",b.dataset.placementMode===mode));
 renderFooter();
}
function renderHeaderFields(){
 syncWorkspaceMode();
 $("#levelId").value=state.level.id;$("#levelName").value=state.level.name;$("#targetFolder").value=state.level.targetFolder;
 const opts=state.rooms.map(r=>`<option value="${esc(r.id)}">${esc(r.displayName)} · ${esc(r.id)}</option>`).join("");
 $("#startRoom").innerHTML=opts;$("#finalRoom").innerHTML=opts;
 $("#startRoom").value=state.level.startRoomId;$("#finalRoom").value=state.level.finalRoomId;
 const fr=state.rooms.find(r=>r.id===state.level.finalRoomId);
 $("#finalDoor").innerHTML=`<option value="">— none —</option>`+(fr?.doors||[]).map(d=>`<option value="${esc(d.id)}">${esc(d.id)}</option>`).join("");
 $("#finalDoor").value=state.level.finalExitDoorId||"";
 const r=currentRoom();
 $("#room-hud").innerHTML=state.editor.viewMode==="map"
  ? `<b>LEVEL GRAPH</b> · ${esc(({open:"open room",arrange:"arrange rooms",connect:"connect doors"})[state.editor.mapMode])}`
  : `<b>${esc(r.displayName)}</b> · ${r.bounds.width} × ${r.bounds.height} · ${r.tileGridEnabled?r.tiles.length+" painted cells":"full-room floor fill"}`;
 $$('[data-view]').forEach(b=>b.classList.toggle('active',b.dataset.view===state.editor.viewMode));
 $$('[data-map-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mapMode===state.editor.mapMode));
 $$('[data-placement-mode]').forEach(b=>b.classList.toggle('active',b.dataset.placementMode===state.editor.placementMode));
 $("#tools").style.display=state.editor.viewMode==="room"?"flex":"none";
 $("#map-tools").style.display=state.editor.viewMode==="map"?"flex":"none";
 $("#room-focus-tools").style.display=state.editor.viewMode==="room"&&state.editor.focusRoom!==false?"flex":"none";
 $("#snapSelect").value=String(state.editor.snapSize||1);
 const asset=state.catalog.find(a=>a.id===state.editor.selectedAssetId);
 $("#selected-asset-chip").textContent=asset?`${iconFor(asset.type)} ${asset.label||asset.id}`:"No asset selected";
 const help={open:"Click a room to edit it",arrange:"Drag rooms on the graph grid",connect:"Drag one door socket onto another"}[state.editor.mapMode];
 $("#map-mode-help").textContent=help;
}
function esc(s){return String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;"}[c]))}
function renderAssets(){
 const q=$("#assetSearch").value.toLowerCase(),f=$("#assetFilter").value;
 const list=state.catalog.filter(a=>(f==="all"||a.type===f)&&(!q||`${a.id} ${a.label} ${a.path||""}`.toLowerCase().includes(q)));
 $("#assetList").innerHTML=list.map(a=>`<div class="asset ${state.editor.selectedAssetId===a.id?"selected":""}" data-asset="${esc(a.id)}">
   <div class="asset-icon">${iconFor(a.type)}</div><div><div class="asset-name">${esc(a.label||a.id)}</div><div class="asset-path">${esc(a.id)}${a.path?" · "+esc(a.path):""}</div></div><span class="badge">${esc(a.type)}</span>
 </div>`).join("")||`<div class="notice">No matching project assets. Add an ID manually if its content package is still being prepared.</div>`;
 $$(".asset").forEach(el=>el.onclick=()=>{
   state.editor.selectedAssetId=el.dataset.asset;
   const a=state.catalog.find(x=>x.id===el.dataset.asset);
   if(a?.type==="enemy")setTool("enemy");else if(a?.type==="door")setTool("door");else if(a?.type==="floor")setTool("tile");else setTool("prop");
   if(document.body.classList.contains("room-focus"))closeDrawers();
   renderAssets();renderCanvas();
 });
}
function renderRooms(){
 $("#roomList").innerHTML=state.rooms.map(r=>`<div class="room-item ${r.id===state.activeRoomId?"active":""}" data-room="${esc(r.id)}">
  <div class="room-title">${esc(r.displayName)}</div><div class="room-meta">${esc(r.id)} · grid ${r.grid[0]},${r.grid[1]} · ${r.bounds.width}×${r.bounds.height}</div>
 </div>`).join("");
 $$(".room-item").forEach(x=>{
  x.onclick=()=>{const room=state.rooms.find(r=>r.id===x.dataset.room);state.activeRoomId=x.dataset.room;state.editor.selectedId=null;if(state.editor.viewMode==="map"&&state.editor.mapMode==="open")openRoomEditor(room);else renderAll()};
  x.ondblclick=()=>{const room=state.rooms.find(r=>r.id===x.dataset.room);if(room)openRoomEditor(room)};
 });
}
function doorOptions(selectedValue=""){
 const doors=state.rooms.flatMap(r=>r.doors.map(d=>({id:d.id,label:`${r.displayName}: ${d.id}`})));
 return `<option value="">— choose door —</option>`+doors.map(d=>`<option value="${esc(d.id)}" ${d.id===selectedValue?"selected":""}>${esc(d.label)}</option>`).join("");
}
