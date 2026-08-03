"use strict";

const fs = require("fs");
const path = require("path");

const root = __dirname;
const files = new Map();

for (const name of fs.readdirSync(root).sort()) {
  if (!/^app-.*\.js$/.test(name)) continue;
  files.set(name, fs.readFileSync(path.join(root, name), "utf8"));
}

for (const [name, original] of files) {
  let text = original
    .replace(/state\.activeRoomId\b/g, "state.editor.activeRoomId")
    .replace(/state\.connections\b/g, "state.level.connections")
    .replace(/state\.catalog\b/g, "state.assets")
    .replace(/state\.rooms\b/g, "state.level.rooms")
    .replace(/state\.logic\b/g, "state.level.logic");

  if (name === "app-8.js" || name === "app-9.js") {
    text = text
      .replace(/project\.activeRoomId\b/g, "project.editor.activeRoomId")
      .replace(/project\.catalog\b/g, "project.assets")
      .replace(/project\?\.catalog\b/g, "project?.assets")
      .replace(/project\.rooms\b/g, "project.level.rooms")
      .replace(/project\?\.rooms\b/g, "project?.level?.rooms");
  }

  if (name === "app-1.js") {
    text = text.replace(
      /if\(!project\|\|project\.format!=="shooter-mover-web-level-project"\|\|!Array\.isArray\(project\.rooms\)\|\|!project\.rooms\.length\)/,
      'if(!project||project.format!=="shooter-mover-web-level-project"||!Array.isArray(project.level?.rooms)||!project.level.rooms.length)'
    );

    text = text.replace(
      /function newRoom\(index=0\)\{[\s\S]*?\n\}\nfunction initialState\(\)\{[\s\S]*?\n\}\nlet state=/,
`function newRoom(index=0){
 const rid=\`room.level-1-\${index===0?"start":"room-"+(index+1)}\`;
 return FloorData.prepareRoom({
   id:rid, displayName:index===0?"START ROOM":\`ROOM \${index+1}\`, grid:[index,0], slot:1,
   bounds:{width:24,height:14}, playerStart:index===0?{position:[-9,0],rotation:0}:null,
   floor:FloorData.makeFloor(24,14,"tile.floor-industrial"), entities:[], doors:[],
   encounter:{completion:"all-enemies"}, visibleOnMap:true
 });
}
function initialState(){
 const r=newRoom(0);
 const exit={id:"door.level-1-final-exit",kind:"door",position:[12,0],rotation:90,side:"East",placementMode:"Fixed",traversable:true,visibleOnMap:true,runtimeObject:"door.room-standard",openWhen:"room-complete"};
 r.doors.push(exit);
 return {
  format:"shooter-mover-web-level-project",editorVersion:1,schemaVersion:LevelSave?.LEVEL_VERSION||4,
  level:{id:"level.level-1",name:"Level 1",targetFolder:"level-1",startRoomId:r.id,finalRoomId:r.id,finalExitDoorId:exit.id,rooms:[r],connections:[],logic:[]},
  editor:{activeRoomId:r.id,tool:"select",viewMode:"room",mapMode:"open",placementMode:"single",focusRoom:true,selectedId:null,selectedAssetId:"prop.wall-1x1",zoom:32,pan:[0,0],snap:true,snapSize:1,roomView:{zoom:32,pan:[0,0]},mapView:{zoom:22,pan:[0,0]},customAssets:[]},
  assets:clone(defaultCatalog)
 };
}
let state=`
    );

    text = text.replace(
      /function normalize\(\)\{[\s\S]*?\n\}\n\nfunction setStatus/,
`function normalize(){
 state.level ||= initialState().level;
 state.level.rooms ||= [];
 state.level.connections ||= [];
 state.level.logic ||= [];
 state.editor ||= initialState().editor;
 state.assets ||= [];

 const knownAssets=new Map(defaultCatalog.map(asset=>[asset.id,clone(asset)]));
 state.assets.forEach(asset=>knownAssets.set(asset.id,{...knownAssets.get(asset.id),...asset}));
 state.assets=[...knownAssets.values()].sort((left,right)=>left.type.localeCompare(right.type)||left.id.localeCompare(right.id));
 if(!state.level.rooms.length)state.level.rooms=[newRoom(0)];
 if(!state.level.rooms.some(room=>room.id===state.editor.activeRoomId))state.editor.activeRoomId=state.level.rooms[0].id;
 state.level.rooms.forEach((room,index)=>{
   room.bounds ||= {width:24,height:14};
   room.bounds.width=Math.max(2,Math.round(Number(room.bounds.width)||24));
   room.bounds.height=Math.max(2,Math.round(Number(room.bounds.height)||14));
   room.entities ||= [];
   room.doors ||= [];
   room.encounter ||= {completion:"all-enemies"};
   room.doors.forEach(door=>{const placement=doorEdgePlacement(room,door.position||[0,0]);door.position=placement.position;door.side=placement.side;door.rotation=placement.rotation});
   room.grid ||= [index,0];
   room.slot ||= 1;
   FloorData.prepareRoom(room);
 });
 state.editor.viewMode ||= "room";
 state.editor.mapMode ||= "open";
 state.editor.placementMode ||= "single";
 if(typeof state.editor.focusRoom!=="boolean")state.editor.focusRoom=true;
 state.editor.roomView ||= {zoom:state.editor.zoom||32,pan:state.editor.pan||[0,0]};
 state.editor.mapView ||= {zoom:22,pan:[0,0]};
 state.editor.customAssets ||= [];
}

function setStatus`
    );

    text = text.replace(
      /\$\("#room-hud"\)\.innerHTML=state\.editor\.viewMode==="map"([\s\S]*?): `(<b>\$\{esc\(r\.displayName\)\}[\s\S]*?)\$\{r\.tileGridEnabled\?r\.tiles\.length\+" painted cells":"full-room floor fill"\}`;/,
      '$(&quot;#room-hud&quot;).innerHTML=state.editor.viewMode==="map"$1: `$2${FloorData.isFullFloor(r)?"full-room floor fill":r.floor.count+" painted cells"}`;'
    ).replace(/\$\(&quot;/g, '$("').replace(/&quot;\)/g, '")');
  }

  if (name === "app-2.js") {
    text = text
      .replace('value="${esc(r.floorObject)}"', 'value="${esc(FloorData.defaultFloorTile(r))}"')
      .replace(
        'else if(k==="visible")r.visibleOnMap=el.checked;else r[k]=el.value;',
        'else if(k==="visible")r.visibleOnMap=el.checked;else if(k==="floorObject")FloorData.setDefaultFloorTile(r,el.value);else r[k]=el.value;'
      )
      .replace('fillRoomTiles(r,selectedFloorObject()||r.floorObject)', 'FloorData.fillFloor(r,selectedFloorObject()||FloorData.defaultFloorTile(r))')
      .replace('mutate(()=>{r.tileGridEnabled=true;r.tiles=[]})', 'mutate(()=>FloorData.clearFloor(r))');
  }

  if (name === "app-3.js") {
    text = text
      .replace('return a?.id||currentRoom()?.floorObject||"tile.floor-industrial"', 'return a?.id||FloorData.defaultFloorTile(currentRoom())')
      .replace(
        /function setRoomTile\(r,cell,object\)\{[\s\S]*?\n\}\nfunction fillRoomTiles\(r,object\)\{[\s\S]*?\n\}/,
`function setRoomTile(room,cell,object){
 if(!cell)return false;
 return FloorData.setFloorTile(room,cell.x,cell.y,object);
}
function fillRoomTiles(room,object){
 FloorData.fillFloor(room,object);
}`
      )
      .replace(
        /function drawRoomTiles\(r\)\{[\s\S]*?\n\}\nfunction drawRoom\(r\)\{/,
`function drawRoomTiles(room){
 FloorData.prepareRoom(room);
 const floor=room.floor,tl=worldToScreen([-room.bounds.width/2,room.bounds.height/2]),br=worldToScreen([room.bounds.width/2,-room.bounds.height/2]);
 const left=devicePixel(Math.min(tl[0],br[0])),top=devicePixel(Math.min(tl[1],br[1])),right=devicePixel(Math.max(tl[0],br[0])),bottom=devicePixel(Math.max(tl[1],br[1]));
 const full=FloorData.isFullFloor(room);
 ctx.fillStyle=tileColor(FloorData.defaultFloorTile(room));ctx.globalAlpha=full?.34:.14;ctx.fillRect(left,top,right-left,bottom-top);ctx.globalAlpha=1;
 if(!full)for(let y=0;y<floor.height;y++)for(let x=0;x<floor.width;x++){
  const object=FloorData.getFloorTile(room,x,y);if(!object)continue;
  const rect=tileCellWorldRect(room,x,y),a=worldToScreen([rect.x,rect.y+1]),b=worldToScreen([rect.x+1,rect.y]);
  const sx=devicePixel(Math.min(a[0],b[0])),sy=devicePixel(Math.min(a[1],b[1])),w=devicePixel(Math.abs(b[0]-a[0])),h=devicePixel(Math.abs(b[1]-a[1]));
  ctx.fillStyle=tileColor(object);ctx.fillRect(sx+2,sy+2,Math.max(0,w-4),Math.max(0,h-4));
  ctx.strokeStyle="rgba(239,247,255,.48)";ctx.lineWidth=Math.max(1,1/dpr);ctx.strokeRect(sx+1.5,sy+1.5,Math.max(0,w-3),Math.max(0,h-3));
 }
 const minor=Math.max(1,1/dpr),major=Math.max(2,2/dpr);
 for(let x=0;x<=floor.width;x++){
  const isMajor=x%4===0||x===floor.width,a=worldToScreen([-room.bounds.width/2+x,-room.bounds.height/2]),b=worldToScreen([-room.bounds.width/2+x,room.bounds.height/2]),sx=devicePixel(a[0]),w=isMajor?major:minor;
  ctx.fillStyle=isMajor?"rgba(213,233,251,.96)":"rgba(157,192,224,.76)";ctx.fillRect(devicePixel(sx-w/2),devicePixel(Math.min(a[1],b[1])),w,devicePixel(Math.abs(b[1]-a[1])));
 }
 for(let y=0;y<=floor.height;y++){
  const isMajor=y%4===0||y===floor.height,a=worldToScreen([-room.bounds.width/2,-room.bounds.height/2+y]),b=worldToScreen([room.bounds.width/2,-room.bounds.height/2+y]),sy=devicePixel(a[1]),w=isMajor?major:minor;
  ctx.fillStyle=isMajor?"rgba(213,233,251,.96)":"rgba(157,192,224,.76)";ctx.fillRect(devicePixel(Math.min(a[0],b[0])),devicePixel(sy-w/2),devicePixel(Math.abs(b[0]-a[0])),w);
 }
}
function drawRoom(r){`
      );
  }

  if (name === "app-5.js") {
    text = text.replace(
      'if(r.tileGridEnabled&&r.tiles.length===0)warn(`Room ${r.id} has an enabled tile grid but no painted floor cells.`);',
      'if(!FloorData.isFullFloor(r)&&r.floor.count===0)warn(`Room ${r.id} has no painted floor cells.`);'
    );
  }

  if (name === "app-6.js") {
    text = text.replace(
      /function compressedFloorTiles\(r\)\{[\s\S]*?\n\}\nfunction runtimeRoomFiles/,
      'function compressedFloorTiles(room){return FloorData.buildUnityTiles(room)}\nfunction runtimeRoomFiles'
    );
  }

  if (name === "app-8.js") {
    text = text
      .replace('const room=project.level.rooms[0],door=room.doors[0];', 'const room=project.level.rooms[0],door=room.doors[0];')
      .replace('project.editor.activeRoomId=room.id;', 'project.editor.activeRoomId=room.id;')
      .replace('project.assets=clone(state.assets?.length?state.assets:defaultCatalog);', 'project.assets=clone(state.assets?.length?state.assets:defaultCatalog);')
      .replace(
        '${room.tileGridEnabled?room.tiles.length+" painted cells":"full-room floor fill"}',
        '${FloorData.isFullFloor(room)?"full-room floor fill":room.floor.count+" painted cells"}'
      );
  }

  if (name === "app-9.js") {
    text = text.replace(
      /for\(const room of project\?\.level\?\.rooms\|\|\[\]\)\{\n   if\(room\.floorObject\)ids\.add\(room\.floorObject\);\n   for\(const tile of room\.tiles\|\|\[\]\)if\(tile\.object\)ids\.add\(tile\.object\);/,
      'for(const room of project?.level?.rooms||[]){\n   FloorData.prepareRoom(room);\n   for(const tile of room.floor.tiles)if(tile)ids.add(tile);'
    );
  }

  if (name === "app-13-asset-previews.js") {
    text = text.replace(
      /function drawFloorImages\(room\) \{[\s\S]*?\n  \}\n\n  drawRoomTiles/,
`function drawFloorImages(room) {
    FloorData.prepareRoom(room);
    for (let y = 0; y < room.floor.height; y++) {
      for (let x = 0; x < room.floor.width; x++) {
        const object = FloorData.getFloorTile(room, x, y);
        if (!object) continue;
        const image = imageFor(object);
        if (!image) continue;
        const screen = visibleCell(room, x, y);
        if (!screen) continue;
        const inset = Math.min(3, Math.max(1, screen.width * 0.06));
        drawImageInScreenRect(image, screen.left + inset, screen.top + inset, Math.max(0, screen.width - inset * 2), Math.max(0, screen.height - inset * 2), 0.72);
      }
    }
  }

  drawRoomTiles`
    );
  }

  fs.writeFileSync(path.join(root, name), text);
}
