"use strict";

(() => {
 const GENERATED_LEVEL_PREFIX="Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/";
 const CANONICAL_ENEMY_PREFIX="Content/Enemies/";
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
 function isCanonicalEnemyAsset(asset){
  return isEnemyAsset(asset)&&normalizedSource(asset).startsWith(CANONICAL_ENEMY_PREFIX);
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
   if(!item?.id)continue;
   const previous=byId.get(item.id)||{};
   byId.set(item.id,{...previous,...item,type:item.type||previous.type||typeForAssetId(item.id),label:item.label||previous.label||fallbackLabel(item.id)});
  }
  return [...byId.values()].sort((left,right)=>left.type.localeCompare(right.type)||String(left.label||left.id).localeCompare(String(right.label||right.id)));
 }
 function captureScannedCatalog(){
  scannedCatalog=mergeCatalog(state.assets||[]);
  sharedCatalog=mergeCatalog([
   ...defaultCatalog.filter(asset=>!isEnemyAsset(asset)),
   ...scannedCatalog.filter(asset=>!isGeneratedLevelAsset(asset)&&(!isEnemyAsset(asset)||isCanonicalEnemyAsset(asset))),
  ]);
  sharedCatalogCaptured=true;
 }
 function usedAssetIds(project){
  const ids=new Set();
  for(const room of project?.level?.rooms||[]){
   FloorData.prepareRoom(room);
   for(const tile of room.floor.tiles)if(tile)ids.add(tile);
   for(const entity of room.entities||[])if(entity.object)ids.add(entity.object);
   for(const door of room.doors||[])if(door.runtimeObject)ids.add(door.runtimeObject);
  }
  return ids;
 }
 function removeMissingEnemyPlacements(project,knownEnemyIds){
  let removed=0;
  for(const room of project?.level?.rooms||[]){
   const entities=Array.isArray(room.entities)?room.entities:[];
   const kept=entities.filter(entity=>{
    const object=String(entity?.object||"");
    const missing=object.startsWith("enemy.")&&!knownEnemyIds.has(object);
    if(missing)removed++;
    return !missing;
   });
   room.entities=kept;
  }
  return removed;
 }
 function catalogueForProject(project){
  if(!sharedCatalogCaptured)captureScannedCatalog();
  const current=clone(project?.assets||[]);
  const candidates=new Map();
  for(const item of scannedCatalog){
   if(!item?.id)continue;
   if(!isEnemyAsset(item)||isCanonicalEnemyAsset(item))candidates.set(item.id,item);
  }
  for(const item of current){
   if(item?.id&&!isEnemyAsset(item))candidates.set(item.id,item);
  }
  const kept=[...sharedCatalog];
  for(const id of usedAssetIds(project)){
   const candidate=candidates.get(id);
   if(String(id).startsWith("enemy.")){
    if(candidate&&isCanonicalEnemyAsset(candidate))kept.push(candidate);
    continue;
   }
   kept.push(candidate||{id,type:typeForAssetId(id),label:fallbackLabel(id),source:"level-reference"});
  }
  for(const item of current){
   if(item?.source==="manual"&&!isEnemyAsset(item))kept.push(item);
  }
  return mergeCatalog(kept);
 }
 function repairSelectedAsset(){
  if(state.assets.some(asset=>asset.id===state.editor.selectedAssetId))return;
  const wantedType=state.editor.tool==="enemy"?"enemy":state.editor.tool==="door"?"door":state.editor.tool==="tile"?"floor":"prop";
  state.editor.selectedAssetId=state.assets.find(asset=>asset.type===wantedType)?.id||state.assets[0]?.id||"";
 }
 function sanitizeCurrentProject(){
  if(!state?.level?.rooms?.length)return;
  state.assets=catalogueForProject(state);
  const knownEnemyIds=new Set(state.assets.filter(isCanonicalEnemyAsset).map(asset=>asset.id));
  const removedEnemyPlacements=removeMissingEnemyPlacements(state,knownEnemyIds);
  repairSelectedAsset();
  normalize();
  renderAll();
  writeRecoveryDraft();
  if(removedEnemyPlacements>0){
   console.warn(`Removed ${removedEnemyPlacements} enemy placement${removedEnemyPlacements===1?"":"s"} whose definition is missing or invalid under Content/Enemies.`);
  }
 }

 normalize=function(){
  baseNormalize();
  state.assets=mergeCatalog(state.assets||[]);
  repairSelectedAsset();
 };

 document.addEventListener("click",event=>{
  const target=event.target instanceof Element?event.target:null;
  if(!target)return;
  if(target.closest("#createLevelBtn")){
   if(!sharedCatalogCaptured)captureScannedCatalog();
   // app-8 creates a new project from state.assets in the target-phase click handler.
   // Supply shared project assets plus validated Content/Enemies definitions, while
   // excluding enemy IDs discovered only from generated levels or retired sources.
   state.assets=clone(sharedCatalog);
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

 sharedCatalog=mergeCatalog(defaultCatalog.filter(asset=>!isEnemyAsset(asset)));
 state.assets=mergeCatalog(state.assets||[]);
 repairSelectedAsset();
})();
