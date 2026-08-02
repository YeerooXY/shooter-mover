"use strict";

(() => {
 const GENERATED_LEVEL_PREFIX="Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/";
 let scannedCatalog=[];
 let sharedCatalog=[];
 let sharedCatalogCaptured=false;

 const baseNormalize=normalize;

 function normalizedSource(asset){
  return String(asset?.source||"").replace(/\\/g,"/");
 }
 function isGeneratedLevelAsset(asset){
  return normalizedSource(asset).startsWith(GENERATED_LEVEL_PREFIX);
 }
 function isEnemyAsset(asset){
  return asset?.type==="enemy"||String(asset?.id||"").startsWith("enemy.");
 }
 function typeForAssetId(id){
  const value=String(id||"");
  if(value.startsWith("enemy."))return "enemy";
  if(value.startsWith("prop."))return "prop";
  if(value.startsWith("tile."))return "floor";
  if(value.startsWith("door."))return "door";
  if(value.startsWith("decor.")||value.startsWith("presentation."))return "decor";
  return "prop";
 }
 function fallbackLabel(id){
  return String(id||"Object").split(".").pop().replace(/-/g," ").replace(/\b\w/g,letter=>letter.toUpperCase());
 }
 function mergeCatalog(items){
  const byId=new Map();
  for(const item of items||[]){
   if(!item?.id||isEnemyAsset(item))continue;
   const previous=byId.get(item.id)||{};
   byId.set(item.id,{...previous,...item,type:item.type||previous.type||typeForAssetId(item.id),label:item.label||previous.label||fallbackLabel(item.id)});
  }
  return [...byId.values()].sort((left,right)=>left.type.localeCompare(right.type)||String(left.label||left.id).localeCompare(String(right.label||right.id)));
 }
 function captureScannedCatalog(){
  scannedCatalog=mergeCatalog(state.catalog||[]);
  sharedCatalog=mergeCatalog([
   ...defaultCatalog,
   ...scannedCatalog.filter(asset=>!isGeneratedLevelAsset(asset)),
  ]);
  sharedCatalogCaptured=true;
 }
 function usedAssetIds(project){
  const ids=new Set();
  for(const room of project?.rooms||[]){
   if(room.floorObject)ids.add(room.floorObject);
   for(const tile of room.tiles||[])if(tile.object)ids.add(tile.object);
   for(const entity of room.entities||[])if(entity.object)ids.add(entity.object);
   for(const door of room.doors||[])if(door.runtimeObject)ids.add(door.runtimeObject);
  }
  return ids;
 }
 function catalogueForProject(project){
  if(!sharedCatalogCaptured)captureScannedCatalog();
  const current=clone(project?.catalog||[]);
  const candidates=new Map();
  for(const item of [...scannedCatalog,...current])if(item?.id&&!isEnemyAsset(item))candidates.set(item.id,item);
  const kept=[...sharedCatalog];
  for(const id of usedAssetIds(project)){
   if(String(id).startsWith("enemy."))continue;
   kept.push(candidates.get(id)||{id,type:typeForAssetId(id),label:fallbackLabel(id),source:"level-reference"});
  }
  for(const item of current){
   if(item?.source==="manual"&&!isEnemyAsset(item))kept.push(item);
  }
  return mergeCatalog(kept);
 }
 function repairSelectedAsset(){
  if(state.catalog.some(asset=>asset.id===state.editor.selectedAssetId))return;
  const wantedType=state.editor.tool==="door"?"door":state.editor.tool==="tile"?"floor":"prop";
  state.editor.selectedAssetId=state.catalog.find(asset=>asset.type===wantedType)?.id||state.catalog[0]?.id||"";
 }
 function sanitizeCurrentProject(){
  if(!state?.rooms?.length)return;
  state.catalog=catalogueForProject(state);
  repairSelectedAsset();
  normalize();
  renderAll();
  writeRecoveryDraft();
 }

 normalize=function(){
  baseNormalize();
  state.catalog=mergeCatalog(state.catalog||[]);
  repairSelectedAsset();
 };

 document.addEventListener("click",event=>{
  const target=event.target instanceof Element?event.target:null;
  if(!target)return;
  if(target.closest("#createLevelBtn")){
   if(!sharedCatalogCaptured)captureScannedCatalog();
   // app-8 creates a new project from state.catalog in the target-phase click handler.
   // Supplying only shared non-enemy assets here prevents deleted enemy catalogue
   // entries and prior level instances from being copied into a fresh project.
   state.catalog=clone(sharedCatalog);
   return;
  }
  if(target.closest("[data-continue], [data-level-target]")){
   if(!sharedCatalogCaptured)captureScannedCatalog();
  }
 },true);

 const startDialog=$("#startDialog");
 if(startDialog){
  startDialog.addEventListener("close",()=>queueMicrotask(sanitizeCurrentProject));
 }

 const baseOpenProjectFile=openProjectFile;
 openProjectFile=async function(file){
  await baseOpenProjectFile(file);
  sanitizeCurrentProject();
 };

 sharedCatalog=mergeCatalog(defaultCatalog);
 state.catalog=mergeCatalog(state.catalog||[]);
 repairSelectedAsset();
})();
