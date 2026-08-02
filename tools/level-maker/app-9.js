"use strict";

(() => {
 const GENERATED_LEVEL_PREFIX="Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/";
 const LEGACY_ENEMY_CATALOG=/Assets\/ShooterMover\/Content\/Definitions\/Enemies\/Json\/enemy_catalog_v1\.json$/i;
 const SAFE_BUILT_IN_ENEMIES=new Set(["enemy.moving-droid"]);
 const PLACEMENT_MODE_KEY="shooter-mover.level-maker.preferred-placement-mode.v1";
 const CATEGORY_TO_TOOL={enemy:"enemy",prop:"prop",floor:"tile",door:"door"};
 const CATEGORY_LABELS={enemy:"Enemies",prop:"Props",floor:"Floors",door:"Doors"};
 let scannedCatalog=[];
 let sharedCatalog=clone(defaultCatalog);
 let sharedCatalogCaptured=false;
 let preferredPlacementMode=readPlacementPreference();

 const baseNormalize=normalize;
 const baseRenderAssets=renderAssets;
 const baseSetTool=setTool;
 const baseRenderInspector=renderInspector;

 const style=document.createElement("style");
 style.textContent=`
  #asset-palette .palette-category-tabs{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:5px;margin-bottom:8px}
  #asset-palette .palette-category-tabs button{padding:7px 5px;font-size:10px;white-space:nowrap}
  #asset-palette .palette-category-tabs button.active{border-color:#67c7ff;background:#243d52;color:#fff}
  #asset-palette .palette-empty{padding:12px;color:#9fb0c3;font-size:12px;text-align:center}
  .asset-replacement-section{margin-top:12px;padding-top:12px;border-top:1px solid #34465b}
  .asset-replacement-section .asset-replacement-warning{margin:0 0 8px;padding:8px;border:1px solid #87505a;background:#351d24;color:#ffb7c0;border-radius:6px;font-size:12px}
  .asset-replacement-section .row{align-items:end}
 `;
 document.head.appendChild(style);

 function readPlacementPreference(){
  try{
   const value=localStorage.getItem(PLACEMENT_MODE_KEY);
   return value==="single"||value==="paint"?value:"paint";
  }catch{return "paint"}
 }
 function rememberPlacementPreference(value){
  if(value!=="single"&&value!=="paint")return;
  preferredPlacementMode=value;
  try{localStorage.setItem(PLACEMENT_MODE_KEY,value)}catch{}
 }
 function normalizedSource(asset){
  return String(asset?.source||"").replace(/\\/g,"/");
 }
 function isGeneratedLevelAsset(asset){
  return normalizedSource(asset).startsWith(GENERATED_LEVEL_PREFIX);
 }
 function isLegacyEnemyAsset(asset){
  if(asset?.type!=="enemy")return false;
  if(SAFE_BUILT_IN_ENEMIES.has(asset.id))return false;
  const source=normalizedSource(asset);
  return source==="EnemyCatalog"||LEGACY_ENEMY_CATALOG.test(source);
 }
 function isPlacementAsset(asset){
  if(!asset?.id)return false;
  if(asset.source==="manual")return true;
  if(isGeneratedLevelAsset(asset))return false;
  if(isLegacyEnemyAsset(asset))return false;
  if(asset.type==="enemy"&&!SAFE_BUILT_IN_ENEMIES.has(asset.id))return false;
  return true;
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
   if(!item?.id||!isPlacementAsset(item))continue;
   const previous=byId.get(item.id)||{};
   byId.set(item.id,{...previous,...item,type:item.type||previous.type||typeForAssetId(item.id),label:item.label||previous.label||fallbackLabel(item.id)});
  }
  return [...byId.values()].sort((left,right)=>left.type.localeCompare(right.type)||String(left.label||left.id).localeCompare(String(right.label||right.id)));
 }
 function cleanCurrentCatalog(){
  state.catalog=mergeCatalog(state.catalog||[]);
 }
 function captureScannedCatalog(){
  cleanCurrentCatalog();
  scannedCatalog=clone(state.catalog||[]);
  sharedCatalog=mergeCatalog([...defaultCatalog,...scannedCatalog]);
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
  for(const item of [...scannedCatalog,...current])if(item?.id&&isPlacementAsset(item))candidates.set(item.id,item);
  const kept=[...sharedCatalog];
  for(const id of usedAssetIds(project)){
   const candidate=candidates.get(id);
   if(candidate)kept.push(candidate);
  }
  for(const item of current){
   if(item?.source==="manual")kept.push(item);
  }
  return mergeCatalog(kept);
 }
 function repairSelectedAsset(){
  if(state.catalog.some(asset=>asset.id===state.editor.selectedAssetId))return;
  const wantedType=state.editor.tool==="enemy"?"enemy":state.editor.tool==="door"?"door":state.editor.tool==="tile"?"floor":"prop";
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
  cleanCurrentCatalog();
  state.editor.placementMode=preferredPlacementMode;
  repairSelectedAsset();
 };

 function categoryForTool(tool=state.editor.tool){
  if(tool==="enemy")return "enemy";
  if(tool==="prop"||tool==="wall")return "prop";
  if(tool==="tile"||tool==="tile-erase")return "floor";
  if(tool==="door")return "door";
  return state.editor.assetCategory||"";
 }
 function assetsForCategory(category){
  const assets=state.catalog.filter(asset=>asset.type===category);
  return state.editor.tool==="wall"&&category==="prop"
   ?assets.filter(asset=>asset.id.startsWith("prop.wall-"))
   :assets;
 }
 function renderImprovedPalette(){
  const palette=document.querySelector("#asset-palette");
  if(!palette)return;
  const category=state.editor.assetCategory||categoryForTool();
  if(state.editor.viewMode!=="room"||!category){
   palette.style.display="none";
   palette.innerHTML="";
   return;
  }
  const list=assetsForCategory(category);
  palette.style.display="block";
  palette.innerHTML=`<div class="palette-category-tabs">${Object.entries(CATEGORY_LABELS).map(([key,label])=>`<button type="button" data-palette-category="${key}" class="${key===category?"active":""}">${label}</button>`).join("")}</div><div class="palette-title">${state.editor.tool==="wall"&&category==="prop"?"Wall assets":CATEGORY_LABELS[category]||"Assets"}</div><div class="palette-grid">${list.map(asset=>`<button class="palette-asset ${asset.id===state.editor.selectedAssetId?"selected":""}" data-palette-asset="${esc(asset.id)}" title="${esc(asset.id)}"><span>${iconFor(asset.type)}</span><b>${esc(asset.label||fallbackLabel(asset.id))}</b></button>`).join("")||`<div class="palette-empty">No compiler-safe ${String(CATEGORY_LABELS[category]||"assets").toLowerCase()} are available.</div>`}</div>`;
  palette.querySelectorAll("[data-palette-category]").forEach(button=>button.onclick=()=>{
   const next=button.dataset.paletteCategory;
   state.editor.assetCategory=next;
   baseSetTool(CATEGORY_TO_TOOL[next]);
   state.editor.placementMode=preferredPlacementMode;
   renderAssets();renderHeaderFields();renderCanvas();renderFooter();scheduleRecoverySave();
  });
  palette.querySelectorAll("[data-palette-asset]").forEach(button=>button.onclick=()=>{
   state.editor.selectedAssetId=button.dataset.paletteAsset;
   baseSetTool(CATEGORY_TO_TOOL[category]);
   state.editor.placementMode=preferredPlacementMode;
   renderAssets();renderHeaderFields();renderCanvas();renderFooter();scheduleRecoverySave();
  });
 }

 setTool=function(tool){
  baseSetTool(tool);
  state.editor.placementMode=preferredPlacementMode;
  renderImprovedPalette();
 };
 renderAssets=function(){
  baseRenderAssets();
  renderImprovedPalette();
 };

 function replacementAssets(entity){
  if(entity.kind==="enemy")return state.catalog.filter(asset=>asset.type==="enemy");
  if(entity.kind==="wall")return state.catalog.filter(asset=>asset.type==="prop"&&asset.id.startsWith("prop.wall-"));
  if(entity.kind==="prop")return state.catalog.filter(asset=>asset.type==="prop");
  return [];
 }
 function addReplacementControl(){
  const entity=selected()?.entity;
  if(!entity||!["enemy","prop","wall"].includes(entity.kind))return;
  const inspector=document.querySelector("#inspector .panel");
  if(!inspector||inspector.querySelector(".asset-replacement-section"))return;
  const assets=replacementAssets(entity);
  if(!assets.length)return;
  const currentKnown=state.catalog.some(asset=>asset.id===entity.object);
  const section=document.createElement("div");
  section.className="section asset-replacement-section";
  section.innerHTML=`<div class="section-title">Replace asset</div>${currentKnown?"":`<div class="asset-replacement-warning"><b>${esc(entity.object||"Missing object")}</b> is not registered in the room compiler. Replace it before Playtest.</div>`}<div class="row"><div class="grow"><label>Compiler-safe asset</label><select data-replacement-asset>${assets.map(asset=>`<option value="${esc(asset.id)}" ${asset.id===entity.object?"selected":""}>${esc(asset.label||fallbackLabel(asset.id))}</option>`).join("")}</select></div><button type="button" class="primary" data-replace-asset>Replace</button></div>`;
  inspector.appendChild(section);
  section.querySelector("[data-replace-asset]").onclick=()=>{
   const value=section.querySelector("[data-replacement-asset]").value;
   mutate(()=>{entity.object=value});
   setStatus(`Replaced the object with ${fallbackLabel(value)}.`,"good");
  };
 }
 renderInspector=function(){
  baseRenderInspector();
  addReplacementControl();
 };

 document.addEventListener("click",event=>{
  const target=event.target instanceof Element?event.target:null;
  if(!target)return;
  const placement=target.closest("[data-placement-mode]");
  if(placement)rememberPlacementPreference(placement.dataset.placementMode);
  if(target.closest("#createLevelBtn")){
   if(!sharedCatalogCaptured)captureScannedCatalog();
   state.catalog=clone(sharedCatalog);
   state.editor.placementMode=preferredPlacementMode;
   return;
  }
  if(target.closest("[data-continue], [data-level-target]")){
   if(!sharedCatalogCaptured)captureScannedCatalog();
  }
 },true);

 const startDialog=document.querySelector("#startDialog");
 if(startDialog)startDialog.addEventListener("close",()=>queueMicrotask(sanitizeCurrentProject));

 const baseOpenProjectFile=openProjectFile;
 openProjectFile=async function(file){
  await baseOpenProjectFile(file);
  sanitizeCurrentProject();
 };

 state.editor.placementMode=preferredPlacementMode;
 cleanCurrentCatalog();
 repairSelectedAsset();
})();
