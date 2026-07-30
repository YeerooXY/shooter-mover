function compressedFloorTiles(r){
 if(!r.tileGridEnabled)return [{object:r.floorObject,fill:{from:[-r.bounds.width/2,-r.bounds.height/2],to:[r.bounds.width/2,r.bounds.height/2]}}];
 const {cols,rows}=tileDimensions(r),byKey=new Map(r.tiles.map(t=>[`${t.x},${t.y}`,t.object])),used=new Set(),out=[];
 for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){
  const key=`${x},${y}`,object=byKey.get(key);if(!object||used.has(key))continue;
  let w=1;while(x+w<cols&&byKey.get(`${x+w},${y}`)===object&&!used.has(`${x+w},${y}`))w++;
  let h=1,ok=true;while(y+h<rows&&ok){for(let xx=x;xx<x+w;xx++)if(byKey.get(`${xx},${y+h}`)!==object||used.has(`${xx},${y+h}`)){ok=false;break}if(ok)h++}
  for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)used.add(`${xx},${yy}`);
  out.push({object,fill:{from:[-r.bounds.width/2+x,-r.bounds.height/2+y],to:[-r.bounds.width/2+x+w,-r.bounds.height/2+y+h]}})
 }
 return out
}
function runtimeRoomFiles(r){
 const folder=`Room_${r.grid[0]}_${r.grid[1]}_${String(r.slot||1).padStart(2,"0")}`;
 const enemies=r.entities.filter(e=>e.kind==="enemy").map(e=>({id:e.id,object:e.object,tier:Number(e.tier||1),position:e.position.map(round),rotation:round(e.rotation||0)}));
 const props=r.entities.filter(e=>e.kind==="prop"||e.kind==="wall").map(e=>({id:e.id,object:e.object,position:e.position.map(round),rotation:round(e.rotation||0)}));
 const doors=r.doors.map(d=>({door_id:d.id,side:d.side||"East",placement_mode:"Fixed",current_local_position:d.position.map(round),traversable:d.traversable!==false,visible_on_map:d.visibleOnMap!==false,runtime_object:d.runtimeObject||"door.room-standard"}));
 const optional=r.entities.filter(e=>e.kind==="enemy"&&e.optional).map(e=>e.id);
 const doorRules=r.doors.filter(d=>d.openWhen==="room-complete").map(d=>({match:{door_id:d.id},open_when:"room-complete"}));
 return {folder,documents:{
  "room.json":{schema_version:2,room_id:r.id,display_name:r.displayName,grid_position:r.grid,slot:r.slot||1,footprint_cells:[1,1],runtime_bounds:{center:[0,0],size:[r.bounds.width,r.bounds.height]},...(r.playerStart?{player_start:{position:r.playerStart.position.map(round),rotation:round(r.playerStart.rotation||0)}}:{})},
  "doors.json":{schema_version:2,room_id:r.id,doors},
  "floor.json":{schema_version:2,room:r.id,tiles:compressedFloorTiles(r)},
  "enemies.json":{schema_version:2,room:r.id,enemies},
  "props.json":{schema_version:2,room:r.id,props},
  "decor.json":{schema_version:2,room:r.id,background:[],foreground:[]},
  "encounter.json":{schema_version:2,room:r.id,completion:r.encounter?.completion||"all-enemies",optional_enemy_ids:optional,door_rules:doorRules}
 }};
}
function buildExportFiles(){
 const target=cleanSlug(state.level.targetFolder).toLowerCase(),base=levelSourceBase(state.level.id,target);
 const roomBuilds=state.rooms.map(runtimeRoomFiles);
 const roomIndex=state.rooms.map((r,i)=>({room_id:r.id,grid_position:r.grid,slot:r.slot||1,folder:roomBuilds[i].folder}));
 const nodes=state.rooms.map(r=>({room_id:r.id,grid_position:r.grid,slot:r.slot||1,label:r.displayName,visible_on_map:r.visibleOnMap!==false}));
 const endpoint=doorId=>{const f=findDoor(doorId);return{room_id:f?.room.id||"",door_id:doorId||""}};
 const connections=state.connections.map(c=>({connection_id:c.id,from:endpoint(c.fromDoorId),to:endpoint(c.toDoorId),travel_policy:c.travelPolicy||"Bidirectional"}));
 const level={schema_version:2,level_id:state.level.id,display_name:state.level.name,authoring_state:"validated-playable",runtime_import_status:"compiler-ready",start_room_id:state.level.startRoomId,final_exit:{room_id:state.level.finalRoomId,door_id:state.level.finalExitDoorId||""},room_ids:state.rooms.map(r=>r.id),rooms:roomIndex};
 const files={};
 files[`${base}/level.json`]=pretty(level);files[`${base}/map.json`]=pretty({schema_version:2,nodes,connections});
 roomBuilds.forEach(rb=>Object.entries(rb.documents).forEach(([name,obj])=>files[`${base}/Rooms/${rb.folder}/${name}`]=pretty(obj)));
 return files;
}
function pretty(x){return JSON.stringify(x,null,2)+"\n"}

function levelSourceBase(levelId,target){
 if(levelId==="level.level-1")return "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/Level1";
 return `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/Published/${target}`;
}

function download(blob,name){const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(a.href),3000)}
