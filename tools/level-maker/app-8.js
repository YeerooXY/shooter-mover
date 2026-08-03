"use strict";

(() => {
 const recoveryEnvelope=(()=>{
  try{return JSON.parse(localStorage.getItem(RECOVERY_STORAGE_KEY)||"null")}catch{return null}
 })();
 const recoveryProject=recoveryEnvelope?.project?.format==="shooter-mover-web-level-project"?clone(recoveryEnvelope.project):null;
 let repositoryLevels=[];
 let startupResolved=false;
 let latestPointerWorld=null;

 const baseScheduleRecoverySave=scheduleRecoverySave;
 const baseWriteRecoveryDraft=writeRecoveryDraft;
 const baseSetTool=setTool;
 const baseRenderAssets=renderAssets;
 const baseRenderRooms=renderRooms;
 const baseRenderLogic=renderLogic;
 const baseRenderInspector=renderInspector;
 const baseRenderFooter=renderFooter;
 const basePlaceAt=placeAt;

 document.removeEventListener("pointerup",baseScheduleRecoverySave);
 document.removeEventListener("change",baseScheduleRecoverySave);
 document.removeEventListener("keyup",baseScheduleRecoverySave);
 canvas.removeEventListener("wheel",baseScheduleRecoverySave);
 window.removeEventListener("pagehide",baseWriteRecoveryDraft);
 window.removeEventListener("beforeunload",baseWriteRecoveryDraft);
 scheduleRecoverySave=function(){if(startupResolved)baseScheduleRecoverySave()};
 writeRecoveryDraft=function(){if(startupResolved)baseWriteRecoveryDraft()};
 document.addEventListener("pointerup",scheduleRecoverySave);
 document.addEventListener("change",scheduleRecoverySave);
 document.addEventListener("keyup",scheduleRecoverySave);
 canvas.addEventListener("wheel",scheduleRecoverySave,{passive:true});
 window.addEventListener("pagehide",writeRecoveryDraft);
 window.addEventListener("beforeunload",writeRecoveryDraft);

 const stylesheet=document.createElement("link");
 stylesheet.rel="stylesheet";stylesheet.href="style-ux.css";document.head.appendChild(stylesheet);

 function catalogLabel(id){
  const asset=state.assets?.find(item=>item.id===id);
  if(asset?.label)return asset.label;
  return String(id||"Object").split(".").pop().replace(/-/g," ").replace(/\b\w/g,c=>c.toUpperCase());
 }
 function entityLabel(entity){
  if(!entity)return "Nothing selected";
  if(entity.kind==="door")return "Door";
  if(entity.kind==="teleporter")return "Teleporter";
  if(entity.kind==="enemy"||entity.kind==="prop"||entity.kind==="wall")return catalogLabel(entity.object);
  return entity.id||entity.kind||"Object";
 }
 function doorFriendlyLabel(id){
  const found=findDoor(id);if(!found)return "Missing door";
  const sameSide=found.room.doors.filter(door=>(door.side||"Door")===(found.door.side||"Door"));
  const index=Math.max(0,sameSide.indexOf(found.door))+1;
  return `${found.room.displayName} · ${found.door.side||"Door"} door${sameSide.length>1?` ${index}`:""}`;
 }
 function targetSlug(name){
  return String(name||"New Level").trim().toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-+|-+$/g,"")||"new-level";
 }
 function freshProject(name){
  const project=initialState(),title=String(name||"New Level").trim()||"New Level",target=targetSlug(title);
  const room=project.level.rooms[0],door=room.doors[0];
  room.id=`room.${target}-start`;room.displayName="START ROOM";
  door.id=`door.${target}-final-exit`;
  project.level={id:`level.${target}`,name:title,targetFolder:target,startRoomId:room.id,finalRoomId:room.id,finalExitDoorId:door.id};
  project.editor.activeRoomId=room.id;
  project.assets=clone(state.assets?.length?state.assets:defaultCatalog);
  return project;
 }
 function removeField(control){
  if(!control)return;
  const label=control.previousElementSibling?.tagName==="LABEL"?control.previousElementSibling:null;
  label?.remove();control.remove();
 }

 function installToolbar(){
  const tools=$("#tools");
  tools.innerHTML=`
   <button data-tool="select" class="active"><span class="tool-icon">↖</span><span>Select</span></button>
   <button data-tool="pan"><span class="tool-icon">✋</span><span>Pan</span></button>
   <button data-tool="enemy"><span class="tool-icon">👾</span><span>Enemies</span></button>
   <button data-tool="prop"><span class="tool-icon">▣</span><span>Props</span></button>
   <button data-tool="tile"><span class="tool-icon">▦</span><span>Floors</span></button>
   <button data-tool="tile-erase"><span class="tool-icon">⌫</span><span>Erase</span></button>
   <button data-tool="wall"><span class="tool-icon">▬</span><span>Walls</span></button>
   <button data-tool="door"><span class="tool-icon">▥</span><span>Doors</span></button>
   <button data-tool="player"><span class="tool-icon">●</span><span>Player</span></button>
   <button data-tool="teleporter"><span class="tool-icon">◎</span><span>Teleport</span></button>`;
  const palette=document.createElement("div");palette.id="asset-palette";palette.className="floating";tools.after(palette);
  tools.querySelectorAll("[data-tool]").forEach(button=>button.onclick=()=>setTool(button.dataset.tool));
 }
 function installSimplifiedLevelPanel(){
  const panel=$("#right > .panel");
  panel.innerHTML=`<h2>Level</h2><label>Display name</label><input id="levelName" value="${esc(state.level.name)}">`;
  $("#levelName").onchange=e=>mutate(()=>state.level.name=e.target.value.trim()||state.level.name);
 }
 function installStartDialog(){
  const dialog=document.createElement("dialog");dialog.id="startDialog";
  dialog.innerHTML=`<h2>Open Level Maker</h2><div id="startChoices"></div>`;
  dialog.addEventListener("cancel",event=>event.preventDefault());document.body.appendChild(dialog);
 }

 function renderAssetPalette(){
  const palette=$("#asset-palette");if(!palette)return;
  const category=state.editor.assetCategory||"";
  if(state.editor.viewMode!=="room"||!category){palette.style.display="none";palette.innerHTML="";return}
  const list=state.assets.filter(asset=>asset.type===category);
  const title={enemy:"Enemies",prop:state.editor.tool==="wall"?"Wall assets":"Props",floor:"Floor tiles",door:"Doors"}[category]||"Assets";
  palette.style.display="block";
  palette.innerHTML=`<div class="palette-title">${title}</div><div class="palette-grid">${list.map(asset=>`<button class="palette-asset ${asset.id===state.editor.selectedAssetId?"selected":""}" data-palette-asset="${esc(asset.id)}" title="${esc(asset.id)}"><span>${iconFor(asset.type)}</span><b>${esc(asset.label||catalogLabel(asset.id))}</b></button>`).join("")||`<div class="help">No ${title.toLowerCase()} are available yet.</div>`}</div>`;
  palette.querySelectorAll("[data-palette-asset]").forEach(button=>button.onclick=()=>{
   state.editor.selectedAssetId=button.dataset.paletteAsset;
   if(state.editor.tool!=="wall")baseSetTool(({enemy:"enemy",prop:"prop",floor:"tile",door:"door"})[category]||state.editor.tool);
   renderAssets();renderHeaderFields();renderCanvas();renderFooter();scheduleRecoverySave();
  });
 }

 setTool=function(tool){
  baseSetTool(tool);
  const category=tool==="enemy"?"enemy":tool==="prop"||tool==="wall"?"prop":tool==="tile"?"floor":tool==="door"?"door":"";
  state.editor.assetCategory=category;renderAssetPalette();scheduleRecoverySave();
 };
 renderAssets=function(){
  baseRenderAssets();
  $$("#assetList .asset-path").forEach(path=>path.textContent="");
  renderAssetPalette();
 };
 renderRooms=function(){
  baseRenderRooms();
  $$("#roomList .room-item").forEach(item=>{
   const room=state.level.rooms.find(value=>value.id===item.dataset.room),meta=item.querySelector(".room-meta");
   if(room&&meta)meta.textContent=`${room.bounds.width}×${room.bounds.height} · ${room.entities.length} objects · ${room.doors.length} doors`;
  });
 };
 renderHeaderFields=function(){
  syncWorkspaceMode();
  const levelName=$("#levelName");if(levelName&&document.activeElement!==levelName)levelName.value=state.level.name;
  const room=currentRoom();
  $("#room-hud").innerHTML=state.editor.viewMode==="map"
   ? `<b>LEVEL GRAPH</b> · ${state.editor.mapMode==="connect"?(state.editor.connectSourceDoorId?"choose destination door":"choose first door"):esc(({open:"open room",arrange:"arrange rooms"})[state.editor.mapMode]||state.editor.mapMode)}`
   : `<b>${esc(room.displayName)}</b> · ${room.bounds.width} × ${room.bounds.height} · ${room.tileGridEnabled?room.tiles.length+" painted cells":"full-room floor fill"}`;
  $$('[data-view]').forEach(button=>button.classList.toggle('active',button.dataset.view===state.editor.viewMode));
  $$('[data-map-mode]').forEach(button=>button.classList.toggle('active',button.dataset.mapMode===state.editor.mapMode));
  $$('[data-placement-mode]').forEach(button=>button.classList.toggle('active',button.dataset.placementMode===state.editor.placementMode));
  $("#tools").style.display=state.editor.viewMode==="room"?"flex":"none";
  $("#map-tools").style.display=state.editor.viewMode==="map"?"flex":"none";
  $("#room-focus-tools").style.display=state.editor.viewMode==="room"&&state.editor.focusRoom!==false?"flex":"none";
  $("#snapSelect").value=String(state.editor.snapSize||1);
  const asset=state.assets.find(value=>value.id===state.editor.selectedAssetId);
  $("#selected-asset-chip").textContent=asset?`${iconFor(asset.type)} ${asset.label||catalogLabel(asset.id)}`:"No asset selected";
  const help=state.editor.mapMode==="connect"?(state.editor.connectSourceDoorId?"Click the destination door":"Click the first door"):{open:"Click a room to edit it",arrange:"Drag rooms on the graph grid"}[state.editor.mapMode];
  $("#map-mode-help").textContent=help||"";renderAssetPalette();
 };
 labelEntity=function(entity){
  const position=worldToScreen(entity.position),label=entityLabel(entity);
  ctx.fillStyle="#dbe7f6";ctx.font="10px system-ui";ctx.textAlign="center";ctx.fillText(label.length>28?label.slice(0,27)+"…":label,position[0],position[1]+25);
 };
 renderFooter=function(){
  baseRenderFooter();const value=selected(),object=value?.entity||value?.door;
  $("#selection-hud").textContent=object?entityLabel(object):"Nothing selected";
 };

 function beginDoorConnection(doorId=""){
  const found=doorId?findDoor(doorId):null;
  state.editor.connectSourceDoorId=found?.door.id||null;
  if(found){state.editor.activeRoomId=found.room.id;state.editor.selectedId=found.door.id}else state.editor.selectedId=null;
  setViewMode("map",{focus:false});setMapMode("connect");fitMap();renderAll();
  setStatus(found?`Connecting from ${doorFriendlyLabel(found.door.id)}. Click the destination door.`:"Click the first yellow door, then the destination door.","good");
 }
 function chooseConnectionDoor(doorId){
  const sourceId=state.editor.connectSourceDoorId;
  if(!sourceId){state.editor.connectSourceDoorId=doorId;state.editor.selectedId=doorId;renderAll();setStatus(`Connecting from ${doorFriendlyLabel(doorId)}. Click the destination door.`,"good");return}
  if(sourceId===doorId){state.editor.connectSourceDoorId=null;state.editor.selectedId=null;renderAll();setStatus("Door connection cancelled.","warn");return}
  const source=findDoor(sourceId),target=findDoor(doorId);
  if(!source||!target){state.editor.connectSourceDoorId=null;renderAll();return}
  if(source.room.id===target.room.id){setStatus("Choose a door in another room.","warn");return}
  const duplicate=state.level.connections.some(connection=>(connection.fromDoorId===sourceId&&connection.toDoorId===doorId)||(connection.toDoorId===sourceId&&connection.fromDoorId===doorId));
  if(duplicate){setStatus("Those doors are already connected.","warn");return}
  mutate(()=>{
   state.level.connections.push({id:uid("connection"),fromDoorId:sourceId,toDoorId:doorId,travelPolicy:"Bidirectional"});
   state.editor.connectSourceDoorId=null;state.editor.selectedId=doorId;
  });
  setStatus(`${doorFriendlyLabel(sourceId)} connected to ${doorFriendlyLabel(doorId)}.`,"good");
 }

 renderLogic=function(){
  baseRenderLogic();
  const list=$("#connectionList");
  list.innerHTML=state.level.connections.map(connection=>`<div class="logic-item connection-card" data-connection="${esc(connection.id)}"><div class="connection-route"><b>${esc(doorFriendlyLabel(connection.fromDoorId))}</b><span>→</span><b>${esc(doorFriendlyLabel(connection.toDoorId))}</b></div><div class="row"><select data-policy class="grow"><option ${connection.travelPolicy==="Bidirectional"?"selected":""}>Bidirectional</option><option ${connection.travelPolicy==="OneWay"?"selected":""}>OneWay</option></select><button data-show>Show</button><button data-delete>Delete</button></div></div>`).join("")||`<div class="help">No room connections yet. Use the visual connector instead of choosing IDs.</div>`;
  list.querySelectorAll(".connection-card").forEach(card=>{
   const connection=state.level.connections.find(value=>value.id===card.dataset.connection);
   card.querySelector("[data-policy]").onchange=event=>mutate(()=>connection.travelPolicy=event.target.value);
   card.querySelector("[data-show]").onclick=()=>{const source=findDoor(connection.fromDoorId);if(source){state.editor.activeRoomId=source.room.id;state.editor.selectedId=source.door.id;setViewMode("map",{focus:false});setMapMode("open");fitMap();renderAll()}};
   card.querySelector("[data-delete]").onclick=()=>mutate(()=>state.level.connections=state.level.connections.filter(value=>value!==connection));
  });
  const add=$("#addConnection");add.textContent="Connect doors visually";add.onclick=()=>beginDoorConnection();
 };
 renderInspector=function(){
  baseRenderInspector();
  const wrap=$("#inspector"),selection=selected();
  if(!selection){
   removeField(wrap.querySelector('[data-r="id"]'));
   wrap.querySelector('[data-r="gridX"]')?.closest(".grid2")?.remove();
   removeField(wrap.querySelector('[data-r="floorObject"]'));
   const heading=wrap.querySelector("h2");if(heading)heading.textContent="Room settings";
   const notice=wrap.querySelector(".notice");if(notice)notice.textContent="Select an object to edit it, or choose a large placement button above the room.";
   return
  }
  if(selection.entity){
   const entity=selection.entity;removeField(wrap.querySelector('[data-i="id"]'));removeField(wrap.querySelector('[data-i="object"]'));
   const heading=wrap.querySelector("h2");if(heading)heading.textContent=entityLabel(entity);
   return
  }
  const door=selection.door;
  removeField(wrap.querySelector('[data-d="id"]'));removeField(wrap.querySelector('[data-d="runtimeObject"]'));
  const side=wrap.querySelector('[data-d="side"]');if(side){const grid=side.closest(".grid2"),cell=side.parentElement;cell?.remove();if(grid)grid.style.gridTemplateColumns="1fr"}
  const heading=wrap.querySelector("h2");if(heading)heading.textContent="Door";
  const deleteButton=wrap.querySelector('[data-action="delete"]');
  const actions=document.createElement("div");actions.className="row door-action-row";
  const isFinal=state.level.finalExitDoorId===door.id;
  actions.innerHTML=`<button class="primary grow" data-connect-door>Connect this door</button><button class="grow" data-final-door ${isFinal?"disabled":""}>${isFinal?"Final exit ✓":"Set as final exit"}</button>`;
  deleteButton?.before(actions);
  actions.querySelector("[data-connect-door]").onclick=()=>beginDoorConnection(door.id);
  actions.querySelector("[data-final-door]").onclick=()=>mutate(()=>{state.level.finalRoomId=currentRoom().id;state.level.finalExitDoorId=door.id});
 };

 placeAt=function(tool,position){
  if(tool!=="prop")return basePlaceAt(tool,position);
  const room=currentRoom(),raw=latestPointerWorld||position,cellPosition=snapToRoomCellCenter(room,raw),asset=assetForTool("prop");
  let prop=room.entities.find(entity=>entity.kind==="prop"&&Math.abs(entity.position[0]-cellPosition[0])<.001&&Math.abs(entity.position[1]-cellPosition[1])<.001);
  if(!prop){prop={id:uid("prop"),kind:"prop",object:"",position:cellPosition,rotation:0,blocksMovement:true,layer:"default"};room.entities.push(prop)}
  prop.object=asset?.id||prop.object;prop.position=cellPosition;prop.rotation=0;prop.blocksMovement=true;prop.layer=prop.layer||"default";state.editor.selectedId=prop.id;
 };
 canvas.addEventListener("pointerdown",event=>{if(state.editor.viewMode==="room")latestPointerWorld=screenToWorld(eventPoint(event))},true);
 canvas.addEventListener("pointermove",event=>{if(state.editor.viewMode==="room")latestPointerWorld=screenToWorld(eventPoint(event))},true);
 canvas.addEventListener("pointerup",()=>{latestPointerWorld=null});
 canvas.addEventListener("pointerdown",event=>{
  if(state.editor.viewMode!=="map"||state.editor.mapMode!=="connect"||event.button!==0)return;
  const hit=mapHitTest(eventPoint(event));if(hit?.type!=="door")return;
  event.preventDefault();event.stopImmediatePropagation();chooseConnectionDoor(hit.door.id);
 },true);
 canvas.addEventListener("contextmenu",event=>{
  const hit=state.editor.viewMode==="map"?mapHitTest(eventPoint(event)):hitTest(eventPoint(event));
  const door=hit?.type==="door"?hit.door:hit?.kind==="door"?hit:null;
  if(!door)return;event.preventDefault();event.stopImmediatePropagation();beginDoorConnection(door.id);
 },true);

 function finishStartup(project,message){
  state=clone(project);startupResolved=true;state.editor.connectSourceDoorId=null;normalize();fitRoom();renderAll();baseWriteRecoveryDraft();$("#startDialog").close();setStatus(message,"good");
 }
 function showStartDialog(mode="startup"){
  const dialog=$("#startDialog"),choices=$("#startChoices"),canCancel=mode!=="startup";
  const recovery=mode==="startup"&&recoveryProject?`<button class="start-choice primary" data-continue><b>Continue ${esc(recoveryProject.level?.name||"recovered level")}</b><span>${esc(recoveryProject.rooms?.find(room=>room.id===recoveryProject.activeRoomId)?.displayName||"Last active room")} · saved ${esc(new Date(recoveryEnvelope.savedAt).toLocaleString())}</span></button>`:"";
  const existing=mode!=="new"&&repositoryLevels.length?`<div class="start-section-title">Existing project levels</div><div class="start-level-list">${repositoryLevels.map(level=>`<button class="start-choice" data-level-target="${esc(level.target)}"><b>${esc(level.name)}</b><span>Continue project level</span></button>`).join("")}</div>`:"";
  const create=mode!=="open"?`<div class="start-section-title">Create a new level</div><div class="new-level-row"><input id="newLevelName" value="New Level" aria-label="New level name"><button class="good" id="createLevelBtn">Create</button></div>`:"";
  choices.innerHTML=`${recovery}${existing}${create}${canCancel?`<div class="start-cancel"><button id="cancelStartDialog">Cancel</button></div>`:""}`;
  choices.querySelector("[data-continue]")?.addEventListener("click",()=>finishStartup(recoveryProject,"Recovered work continued."));
  choices.querySelectorAll("[data-level-target]").forEach(button=>button.onclick=async()=>{
   try{const result=await helper(`/api/level?target=${encodeURIComponent(button.dataset.levelTarget)}`);finishStartup(result.project,`Opened ${result.project.level.name}.`)}catch(error){setStatus(error.message,"bad")}
  });
  choices.querySelector("#createLevelBtn")?.addEventListener("click",()=>finishStartup(freshProject(choices.querySelector("#newLevelName").value),"New level created."));
  choices.querySelector("#cancelStartDialog")?.addEventListener("click",()=>dialog.close());
  if(!dialog.open)dialog.showModal();
 }

 connectHelper=async function(){
  try{
   const status=await helper("/api/status");helperToken=status.mutationToken;
   const [catalogue,levels]=await Promise.all([helper("/api/level-assets"),helper("/api/levels")]);
   if(catalogue.assets?.length)state.assets=catalogue.assets;repositoryLevels=levels.levels||[];
   normalize();renderAll();setStatus(`Connected to ${status.branch}; choose a level to continue.`,"good");
  }catch(error){setStatus(`Local helper unavailable: ${error.message}`,"bad")}
  showStartDialog("startup");
 };
 openProjectFile=async function(file){
  try{const project=JSON.parse(await file.text());if(project.format!=="shooter-mover-web-level-project")throw new Error("Not a Shooter Mover web level project.");finishStartup(project,`Opened ${file.name}.`)}catch(error){setStatus(error.message,"bad");alert(error.message)}
 };
 openRepositoryLevel=function(){showStartDialog("open")};

 installToolbar();installSimplifiedLevelPanel();installStartDialog();
 $("#newBtn").onclick=()=>showStartDialog("new");
 $("#openRepoBtn").onclick=()=>showStartDialog("open");
 const connectModeButton=$("#map-tools [data-map-mode=connect]");if(connectModeButton)connectModeButton.onclick=()=>{state.editor.connectSourceDoorId=null;setMapMode("connect");renderAll()};
 $("#map-tools [data-map-mode=open]")?.addEventListener("click",()=>{state.editor.connectSourceDoorId=null});
 $("#map-tools [data-map-mode=arrange]")?.addEventListener("click",()=>{state.editor.connectSourceDoorId=null});
 state.editor.assetCategory=state.editor.assetCategory||"";
})();
