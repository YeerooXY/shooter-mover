function saveProject(){download(new Blob([pretty(state)],{type:"application/json"}),`${cleanSlug(state.level.targetFolder)}.smlvl.json`);setStatus("Editor project saved.","good")}
async function openProjectFile(file){
 try{const parsed=JSON.parse(await file.text());if(parsed.format!=="shooter-mover-web-level-project")throw new Error("Not a Shooter Mover web level project.");pushHistory(snapshot());state=parsed;normalize();fitRoom();renderAll();setStatus(`Opened ${file.name}.`,"good")}catch(e){setStatus(e.message,"bad");alert(e.message)}
}
async function scanFiles(files){
 const found=new Map(state.catalog.map(a=>[a.id,a]));
 let parsed=0;
 for(const file of files){
  const path=(file.webkitRelativePath||file.name).replaceAll("\\","/");
  if(file.name.endsWith(".json")&&file.size<4_000_000){
   try{
    const data=JSON.parse(await file.text());parsed++;
    collectCatalog(data,path,found);
   }catch{}
  }
  if(file.name.endsWith(".prefab")){
   const stem=file.name.replace(/\.prefab$/i,""),lower=path.toLowerCase();let type=lower.includes("enemy")?"enemy":lower.includes("door")?"door":lower.includes("floor")||lower.includes("tile")?"floor":"prop";
   const id=`${type}.${stem.toLowerCase().replace(/[^a-z0-9]+/g,"-")}`;if(!found.has(id))found.set(id,{id,label:stem,type,path,source:"prefab filename (verify ID)"});
  }
 }
 state.catalog=[...found.values()].sort((a,b)=>a.type.localeCompare(b.type)||a.id.localeCompare(b.id));renderAll();setStatus(`Scanned ${files.length} files / ${parsed} JSON documents; catalogue now has ${state.catalog.length} IDs.`,"good");
}
function collectCatalog(data,path,found){
 const add=(id,obj={})=>{
  if(typeof id!=="string"||!id.includes("."))return;let type=id.startsWith("enemy.")?"enemy":id.startsWith("prop.")?"prop":id.startsWith("tile.")?"floor":id.startsWith("door.")?"door":id.startsWith("decor.")||id.startsWith("presentation.")?"decor":null;if(!type)return;
  const old=found.get(id)||{};found.set(id,{...old,id,label:obj.display_name||obj.name||obj.label||id.split(".").pop().replaceAll("-"," "),type,path,source:"scanned JSON"});
 };
 const walk=x=>{
  if(Array.isArray(x)){x.forEach(walk);return}
  if(!x||typeof x!=="object")return;
  add(x.id,x);add(x.object,x);add(x.definition_id,x);if(typeof x.runtime_object==="string")add(x.runtime_object,x);
  Object.values(x).forEach(walk);
 };walk(data);
}
async function chooseProject(){
 if(window.showDirectoryPicker){
  try{
   const handle=await showDirectoryPicker({mode:"read"}),files=[];
   async function walk(h,p=""){for await(const [name,item] of h.entries()){if(item.kind==="directory"){if(!["Library","Temp","Logs","obj",".git"].includes(name))await walk(item,p+name+"/")}else{const f=await item.getFile();Object.defineProperty(f,"webkitRelativePath",{value:p+name});files.push(f)}}}
   setStatus("Scanning selected Unity project…");await walk(handle);await scanFiles(files);return;
  }catch(e){if(e.name==="AbortError")return}
 }
 $("#folderInput").click();
}
function addRoom(){
 const wasMap=state.editor.viewMode==="map";
 mutate(()=>{const i=state.rooms.length,r=newRoom(i);r.id=`room.${safeId(state.level.id,"level")}.room-${i+1}`;r.grid=[i,0];state.rooms.push(r);state.activeRoomId=r.id;state.editor.selectedId=null});
 if(wasMap)fitMap();else fitRoom();renderAll()
}
function duplicateRoom(){
 const src=currentRoom();mutate(()=>{const r=clone(src);r.id=uid("room");r.displayName=src.displayName+" COPY";r.grid=[src.grid[0]+1,src.grid[1]];r.entities.forEach(e=>e.id=uid(e.kind));r.doors.forEach(d=>d.id=uid("door"));state.rooms.push(r);state.activeRoomId=r.id;state.editor.selectedId=null})
}

$("#newBtn").onclick=()=>{if(confirm("Start a new level project?")){pushHistory(snapshot());state=initialState();fitRoom();renderAll()}};
$("#openBtn").onclick=()=>$("#openInput").click();$("#openInput").onchange=e=>e.target.files[0]&&openProjectFile(e.target.files[0]);
$("#saveBtn").onclick=saveProject;$("#scanBtn").onclick=chooseProject;$("#folderInput").onchange=e=>scanFiles([...e.target.files]);
$("#undoBtn").onclick=undo;$("#redoBtn").onclick=redo;$("#validateBtn").onclick=showValidation;$("#exportBtn").onclick=exportZip;
$("#assetSearch").oninput=renderAssets;$("#assetFilter").onchange=renderAssets;
$("#addRoom").onclick=addRoom;$("#duplicateRoom").onclick=duplicateRoom;
$("#addConnection").onclick=()=>mutate(()=>state.connections.push({id:uid("connection"),fromDoorId:"",toDoorId:"",travelPolicy:"Bidirectional"}));
$("#addLogic").onclick=()=>mutate(()=>state.logic.push({id:uid("logic"),name:"New rule",when:"switch-activated",targetId:"",action:"open-door"}));
$$(".tabs button").forEach(b=>b.onclick=()=>{$$(".tabs button").forEach(x=>x.classList.toggle("active",x===b));$$(".tab-page").forEach(x=>x.classList.toggle("active",x.id===`tab-${b.dataset.tab}`))});
$$("[data-tool]").forEach(b=>b.onclick=()=>setTool(b.dataset.tool));
$$("[data-view]").forEach(b=>b.onclick=()=>{setViewMode(b.dataset.view,{focus:b.dataset.view==="room"});if(b.dataset.view==="map")fitMap();else fitRoom();renderAll()});
$$("[data-map-mode]").forEach(b=>b.onclick=()=>setMapMode(b.dataset.mapMode));
$$("[data-placement-mode]").forEach(b=>b.onclick=()=>setPlacementMode(b.dataset.placementMode));
$("#backToGraph").onclick=()=>{setViewMode("map",{focus:false});fitMap();renderAll()};
$("#toggleAssetsDrawer").onclick=()=>toggleDrawer("left");
$("#toggleInspectorDrawer").onclick=()=>toggleDrawer("right");
$("#snapSelect").onchange=e=>{state.editor.snapSize=+e.target.value;state.editor.snap=true;renderAll()};
$("#addCustomAsset").onclick=()=>$("#customAssetDialog").showModal();$("#cancelCustomAsset").onclick=()=>$("#customAssetDialog").close();
$("#confirmCustomAsset").onclick=()=>{
 const id=$("#customAssetId").value.trim();if(!id)return;mutate(()=>state.catalog.push({id,label:$("#customAssetLabel").value.trim()||id,type:$("#customAssetType").value,source:"manual"}));$("#customAssetDialog").close();$("#customAssetId").value="";$("#customAssetLabel").value=""
};
bindChange("#levelId",v=>state.level.id=v);bindChange("#levelName",v=>state.level.name=v);bindChange("#targetFolder",v=>state.level.targetFolder=cleanSlug(v));
bindChange("#startRoom",v=>state.level.startRoomId=v);bindChange("#finalRoom",v=>{state.level.finalRoomId=v;state.level.finalExitDoorId=""});
bindChange("#finalDoor",v=>state.level.finalExitDoorId=v);$("#includeBridge").onchange=e=>mutate(()=>state.level.includeBridge=e.target.checked);
document.addEventListener("keydown",e=>{
 if(["INPUT","TEXTAREA","SELECT"].includes(document.activeElement.tagName))return;
 if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==="z"){e.preventDefault();e.shiftKey?redo():undo();return}
 if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==="y"){e.preventDefault();redo();return}
 if(e.key==="Delete"||e.key==="Backspace"){if(state.editor.selectedId)mutate(()=>deleteSelected());return}
 const key=e.key.toLowerCase(),keys={v:"select",h:"pan",f:"tile",x:"tile-erase",w:"wall",d:"door",p:"prop",t:"teleporter"};
 if(key==="1"){setPlacementMode("single");return}
 if(key==="2"){setPlacementMode("paint");return}
 if(key==="g"){setViewMode("map",{focus:false});fitMap();renderAll();return}
 if(key==="escape"){if(document.body.classList.contains("drawer-open"))closeDrawers();else if(state.editor.viewMode==="room"){setViewMode("map",{focus:false});fitMap();renderAll()}return}
 if(keys[key])setTool(keys[key]);
 if(["q","e"].includes(key)&&state.editor.selectedId){mutate(()=>{const s=selected(),o=s?.entity||s?.door;if(o)o.rotation=round((o.rotation||0)+(key==="e"?15:-15))})}
});
window.addEventListener("resize",resizeCanvas);
window.addEventListener("orientationchange",()=>setTimeout(resizeCanvas,80));
if(window.ResizeObserver)new ResizeObserver(()=>resizeCanvas()).observe($("#stage-wrap"));
normalize();requestAnimationFrame(()=>{resizeCanvas();fitRoom();renderAll();setStatus("Ready. Grid cells are square; props snap to tile centers.")});
