let helperToken="";
async function helper(path,options={}){
 const headers={"Content-Type":"application/json",...(options.headers||{})};
 if(options.method&&options.method!=="GET")headers["x-level-maker-token"]=helperToken;
 const response=await fetch(path,{...options,headers}),value=await response.json();
 if(!response.ok)throw new Error(value.error||`Level Maker request failed (${response.status}).`);
 return value;
}
async function connectHelper(){
 try{
  const status=await helper("/api/status");helperToken=status.mutationToken;
  const catalogue=await helper("/api/level-assets");
  if(catalogue.assets?.length){
   const merged=new Map(state.assets.map(asset=>[asset.id,asset]));
   catalogue.assets.forEach(asset=>merged.set(asset.id,{...merged.get(asset.id),...asset}));
   state.assets=[...merged.values()].sort((left,right)=>left.type.localeCompare(right.type)||left.id.localeCompare(right.id));
  }
  normalize();renderAll();setStatus(`Connected to ${status.branch}; ${catalogue.assets.length} project assets available.${recoveryNotice()}`,"good");
 }catch(error){setStatus(`Local helper unavailable: ${error.message}${recoveryNotice()}`,"bad")}
}
async function publishProject(){
 const errors=validate().filter(x=>x.severity==="error");
 if(errors.length){showValidation();setStatus("Publish blocked by validation errors.","bad");return}
 try{
  const result=await helper("/api/level",{method:"PUT",body:JSON.stringify({project:state,files:buildExportFiles()})});
  setStatus(`Saved ${result.projectPath} and published ${result.fileCount} Unity source files.`,"good");
 }catch(error){setStatus(error.message,"bad")}
}
function saveProject(){return publishProject()}
function downloadProject(){download(new Blob([pretty(state)],{type:"application/json"}),`${cleanSlug(state.level.targetFolder)}.level.json`);setStatus("Portable level project exported.","good")}
async function openProjectFile(file){
 try{const parsed=JSON.parse(await file.text());if(parsed.format!=="shooter-mover-web-level-project")throw new Error("Not a Shooter Mover web level project.");pushHistory(snapshot());state=parsed;normalize();fitRoom();renderAll();setStatus(`Opened ${file.name}.`,"good")}catch(e){setStatus(e.message,"bad");alert(e.message)}
}
async function openRepositoryLevel(){
 try{
  const list=await helper("/api/levels");
  if(!list.levels.length)throw new Error("No project levels were found.");
  const choices=list.levels.map((x,index)=>`${index+1}. ${x.name} (${x.target})`).join("\n");
  const answer=prompt(`Choose a project level:\n${choices}`,"1");
  if(answer===null)return;
  const index=Number(answer)-1;
  if(!Number.isInteger(index)||index<0||index>=list.levels.length)throw new Error("That level selection is invalid.");
  const result=await helper(`/api/level?target=${encodeURIComponent(list.levels[index].target)}`);
  pushHistory(snapshot());state=result.project;normalize();fitRoom();renderAll();
  setStatus(`Opened ${list.levels[index].name} from the repository.`,"good");
 }catch(error){setStatus(error.message,"bad")}
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
 mutate(()=>{const i=state.level.rooms.length,roomKey=safeId(state.level.targetFolder||state.level.name||"level","level").replace(/\./g,"-");const r=newRoom(i);r.id=`room.${roomKey}-${i+1}`;r.grid=[i,0];state.level.rooms.push(r);state.editor.activeRoomId=r.id;state.editor.selectedId=null});
 if(wasMap)fitMap();else fitRoom();renderAll()
}
function duplicateRoom(){
 const src=currentRoom();mutate(()=>{const r=clone(src);r.id=uid("room");r.displayName=src.displayName+" COPY";r.grid=[src.grid[0]+1,src.grid[1]];r.entities.forEach(e=>e.id=uid(e.kind));r.doors.forEach(d=>d.id=uid("door"));state.level.rooms.push(r);state.editor.activeRoomId=r.id;state.editor.selectedId=null})
}

$("#newBtn").onclick=()=>{if(confirm("Start a new level project?")){pushHistory(snapshot());state=initialState();fitRoom();renderAll()}};
$("#openBtn").onclick=()=>$("#openInput").click();$("#openInput").onchange=e=>e.target.files[0]&&openProjectFile(e.target.files[0]);
$("#openRepoBtn").onclick=openRepositoryLevel;
$("#saveBtn").onclick=saveProject;$("#downloadBtn").onclick=downloadProject;
$("#undoBtn").onclick=undo;$("#redoBtn").onclick=redo;$("#validateBtn").onclick=showValidation;$("#exportBtn").onclick=publishProject;
$("#assetSearch").oninput=renderAssets;$("#assetFilter").onchange=renderAssets;
$("#addRoom").onclick=addRoom;$("#duplicateRoom").onclick=duplicateRoom;
$("#addConnection").onclick=()=>mutate(()=>state.level.connections.push({id:uid("connection"),fromDoorId:"",toDoorId:"",travelPolicy:"Bidirectional"}));
$("#addLogic").onclick=()=>mutate(()=>state.level.logic.push({id:uid("logic"),name:"New rule",when:"switch-activated",targetId:"",action:"open-door"}));
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
 const id=$("#customAssetId").value.trim();if(!id)return;mutate(()=>state.assets.push({id,label:$("#customAssetLabel").value.trim()||id,type:$("#customAssetType").value,source:"manual"}));$("#customAssetDialog").close();$("#customAssetId").value="";$("#customAssetLabel").value=""
};
bindChange("#levelId",v=>state.level.id=v);bindChange("#levelName",v=>state.level.name=v);bindChange("#targetFolder",v=>state.level.targetFolder=cleanSlug(v));
bindChange("#startRoom",v=>state.level.startRoomId=v);bindChange("#finalRoom",v=>{state.level.finalRoomId=v;state.level.finalExitDoorId=""});
bindChange("#finalDoor",v=>state.level.finalExitDoorId=v);
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
document.addEventListener("pointerup",scheduleRecoverySave);
document.addEventListener("change",scheduleRecoverySave);
document.addEventListener("keyup",scheduleRecoverySave);
canvas.addEventListener("wheel",scheduleRecoverySave,{passive:true});
window.addEventListener("pagehide",writeRecoveryDraft);
window.addEventListener("beforeunload",writeRecoveryDraft);
window.addEventListener("resize",resizeCanvas);
window.addEventListener("orientationchange",()=>setTimeout(resizeCanvas,80));
if(window.ResizeObserver)new ResizeObserver(()=>resizeCanvas()).observe($("#stage-wrap"));
normalize();requestAnimationFrame(()=>{resizeCanvas();fitRoom();renderAll();connectHelper()});
