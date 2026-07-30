function deleteActiveRoom(){
 const id=state.activeRoomId,stateIds=new Set(currentRoom().doors.map(d=>d.id));
 state.rooms=state.rooms.filter(r=>r.id!==id);state.connections=state.connections.filter(c=>!stateIds.has(c.fromDoorId)&&!stateIds.has(c.toDoorId));
 state.activeRoomId=state.rooms[0].id;if(state.level.startRoomId===id)state.level.startRoomId=state.activeRoomId;if(state.level.finalRoomId===id)state.level.finalRoomId=state.activeRoomId;
}

function viewScale(){
 const raw=Math.max(1,state.editor.zoom||32);
 return Math.max(1,Math.round(raw*dpr)/dpr)
}
function devicePixel(v){return Math.round(v*dpr)/dpr}
function resizeCanvas(){
 const width=Math.max(1,Math.floor(canvas.clientWidth));
 const height=Math.max(1,Math.floor(canvas.clientHeight));
 dpr=Math.max(1,Math.min(3,devicePixelRatio||1));
 const backingWidth=Math.round(width*dpr),backingHeight=Math.round(height*dpr);
 if(canvas.width!==backingWidth||canvas.height!==backingHeight){
  canvas.width=backingWidth;canvas.height=backingHeight
 }
 ctx.imageSmoothingEnabled=false;
 renderCanvas();
}
function worldToScreen(p){
 const z=viewScale(),rect=canvas.getBoundingClientRect();
 return [rect.width/2+state.editor.pan[0]+p[0]*z,rect.height/2+state.editor.pan[1]-p[1]*z]
}
function screenToWorld(p){
 const z=viewScale(),rect=canvas.getBoundingClientRect();
 return [(p[0]-rect.width/2-state.editor.pan[0])/z,-(p[1]-rect.height/2-state.editor.pan[1])/z]
}
function snapPoint(p){if(!state.editor.snap)return p;const s=state.editor.snapSize||1;return p.map(v=>Math.round(v/s)*s)}
function snapToRoomCellCenter(r,p){
 const cols=Math.max(1,Math.round(r.bounds.width)),rows=Math.max(1,Math.round(r.bounds.height));
 const minX=-cols/2,minY=-rows/2;
 const x=clamp(Math.floor(p[0]-minX),0,cols-1);
 const y=clamp(Math.floor(p[1]-minY),0,rows-1);
 return [minX+x+.5,minY+y+.5]
}
function doorEdgePlacement(r,p){
 const hw=r.bounds.width/2,hh=r.bounds.height/2,x=clamp(p[0],-hw,hw),y=clamp(p[1],-hh,hh);
 const side=[["East",Math.abs(hw-x)],["West",Math.abs(-hw-x)],["North",Math.abs(hh-y)],["South",Math.abs(-hh-y)]].sort((a,b)=>a[1]-b[1])[0][0];
 if(side==="East")return {side,position:[hw,y],rotation:90};
 if(side==="West")return {side,position:[-hw,y],rotation:90};
 if(side==="North")return {side,position:[x,hh],rotation:0};
 return {side,position:[x,-hh],rotation:0}
}
function doorIsOnRoomEdge(r,d){
 const hw=r.bounds.width/2,hh=r.bounds.height/2,x=d.position?.[0],y=d.position?.[1],epsilon=.001;
 if(!Number.isFinite(x)||!Number.isFinite(y))return false;
 return Math.abs(Math.abs(x)-hw)<=epsilon||Math.abs(Math.abs(y)-hh)<=epsilon
}
function renderCanvas(){
 if(!ctx)return;const rect=canvas.getBoundingClientRect();ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,rect.width,rect.height);
 drawGrid(rect);
 if(state.editor.viewMode==="map")drawLevelMap();
 else drawRoom(currentRoom())
}
function drawGrid(rect){
 const z=viewScale(),s=Math.max(.25,state.editor.snapSize||1),step=z*s;
 ctx.fillStyle="#0d1015";ctx.fillRect(0,0,rect.width,rect.height);
 if(step<4)return;
 const origin=worldToScreen([0,0]);
 const minorWidth=Math.max(1/dpr,1),axisWidth=Math.max(2/dpr,2);
 for(let x=((origin[0]%step)+step)%step;x<rect.width;x+=step){
  const axis=Math.abs(x-origin[0])<step*.08,w=axis?axisWidth:minorWidth;
  ctx.fillStyle=axis?"rgba(126,164,207,.78)":"rgba(67,89,116,.62)";
  ctx.fillRect(devicePixel(x-w/2),0,w,rect.height)
 }
 for(let y=((origin[1]%step)+step)%step;y<rect.height;y+=step){
  const axis=Math.abs(y-origin[1])<step*.08,w=axis?axisWidth:minorWidth;
  ctx.fillStyle=axis?"rgba(126,164,207,.78)":"rgba(67,89,116,.62)";
  ctx.fillRect(0,devicePixel(y-w/2),rect.width,w)
 }
}
function selectedFloorObject(){
 const a=state.catalog.find(x=>x.id===state.editor.selectedAssetId&&x.type==="floor");return a?.id||currentRoom()?.floorObject||"tile.floor-industrial"
}
function tileColor(id){
 let h=0;for(const c of String(id||"tile"))h=(h*31+c.charCodeAt(0))>>>0;
 const hue=h%360;return `hsl(${hue} 38% 38%)`
}
function tileDimensions(r){return {cols:Math.max(1,Math.round(r.bounds.width)),rows:Math.max(1,Math.round(r.bounds.height)),size:1}}
function tileCellFromWorld(r,p){
 const {cols,rows}=tileDimensions(r),x=Math.floor(p[0]+r.bounds.width/2),y=Math.floor(p[1]+r.bounds.height/2);
 return x>=0&&y>=0&&x<cols&&y<rows?{x,y,key:`${x},${y}`}:null
}
function tileCellWorldRect(r,x,y){return {x:-r.bounds.width/2+x,y:-r.bounds.height/2+y,w:1,h:1}}
function setRoomTile(r,cell,object){
 if(!cell)return;r.tileGridEnabled=true;const i=r.tiles.findIndex(t=>t.x===cell.x&&t.y===cell.y);
 if(object){const value={x:cell.x,y:cell.y,object};if(i>=0)r.tiles[i]=value;else r.tiles.push(value)}else if(i>=0)r.tiles.splice(i,1)
}
function fillRoomTiles(r,object){
 r.tileGridEnabled=true;r.tiles=[];const {cols,rows}=tileDimensions(r);for(let y=0;y<rows;y++)for(let x=0;x<cols;x++)r.tiles.push({x,y,object})
}
function drawRoomTiles(r){
 const tl=worldToScreen([-r.bounds.width/2,r.bounds.height/2]),br=worldToScreen([r.bounds.width/2,-r.bounds.height/2]);
 const left=devicePixel(Math.min(tl[0],br[0])),top=devicePixel(Math.min(tl[1],br[1]));
 const right=devicePixel(Math.max(tl[0],br[0])),bottom=devicePixel(Math.max(tl[1],br[1]));
 ctx.fillStyle=tileColor(r.floorObject);ctx.globalAlpha=r.tileGridEnabled?.14:.34;ctx.fillRect(left,top,right-left,bottom-top);ctx.globalAlpha=1;
 if(r.tileGridEnabled){
  for(const t of r.tiles){
   const rect=tileCellWorldRect(r,t.x,t.y),a=worldToScreen([rect.x,rect.y+1]),b=worldToScreen([rect.x+1,rect.y]);
   const x=devicePixel(Math.min(a[0],b[0])),y=devicePixel(Math.min(a[1],b[1]));
   const w=devicePixel(Math.abs(b[0]-a[0])),h=devicePixel(Math.abs(b[1]-a[1]));
   ctx.fillStyle=tileColor(t.object);ctx.fillRect(x+2,y+2,Math.max(0,w-4),Math.max(0,h-4));
   ctx.strokeStyle="rgba(239,247,255,.48)";ctx.lineWidth=Math.max(1,1/dpr);
   ctx.strokeRect(x+1.5,y+1.5,Math.max(0,w-3),Math.max(0,h-3));
   if(viewScale()>=34){ctx.fillStyle="rgba(255,255,255,.86)";ctx.font="8px system-ui";ctx.textAlign="center";ctx.fillText(String(t.object).split(".").pop().slice(0,8),x+w/2,y+h/2+3)}
  }
 }
 const {cols,rows}=tileDimensions(r),minor=Math.max(1,1/dpr),major=Math.max(2,2/dpr);
 for(let x=0;x<=cols;x++){
  const isMajor=x%4===0||x===cols,a=worldToScreen([-r.bounds.width/2+x,-r.bounds.height/2]),b=worldToScreen([-r.bounds.width/2+x,r.bounds.height/2]);
  const sx=devicePixel(a[0]),w=isMajor?major:minor;
  ctx.fillStyle=isMajor?"rgba(213,233,251,.96)":"rgba(157,192,224,.76)";
  ctx.fillRect(devicePixel(sx-w/2),devicePixel(Math.min(a[1],b[1])),w,devicePixel(Math.abs(b[1]-a[1])));
  if(isMajor&&viewScale()>=20&&x<cols){ctx.fillStyle="rgba(231,243,253,.94)";ctx.font="9px system-ui";ctx.textAlign="left";ctx.fillText(String(x),sx+4,top+12)}
 }
 for(let y=0;y<=rows;y++){
  const isMajor=y%4===0||y===rows,a=worldToScreen([-r.bounds.width/2,-r.bounds.height/2+y]),b=worldToScreen([r.bounds.width/2,-r.bounds.height/2+y]);
  const sy=devicePixel(a[1]),w=isMajor?major:minor;
  ctx.fillStyle=isMajor?"rgba(213,233,251,.96)":"rgba(157,192,224,.76)";
  ctx.fillRect(devicePixel(Math.min(a[0],b[0])),devicePixel(sy-w/2),devicePixel(Math.abs(b[0]-a[0])),w);
  if(isMajor&&viewScale()>=20&&y<rows){ctx.fillStyle="rgba(231,243,253,.94)";ctx.font="9px system-ui";ctx.textAlign="left";ctx.fillText(String(y),left+4,sy-4)}
 }
 if(viewScale()>=14){
  ctx.fillStyle="rgba(226,240,252,.5)";
  const radius=viewScale()>=28?1.5:1;
  for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){
   const c=worldToScreen([-r.bounds.width/2+x+.5,-r.bounds.height/2+y+.5]);
   ctx.beginPath();ctx.arc(devicePixel(c[0]),devicePixel(c[1]),radius,0,Math.PI*2);ctx.fill()
  }
 }
}
function drawRoom(r){
 const tl=worldToScreen([-r.bounds.width/2,r.bounds.height/2]),br=worldToScreen([r.bounds.width/2,-r.bounds.height/2]);
 ctx.fillStyle="#151b23";ctx.fillRect(tl[0],tl[1],br[0]-tl[0],br[1]-tl[1]);drawRoomTiles(r);
 ctx.strokeStyle="rgba(230,242,253,.99)";ctx.lineWidth=Math.max(3,3/dpr);ctx.strokeRect(devicePixel(tl[0]),devicePixel(tl[1]),devicePixel(br[0]-tl[0]),devicePixel(br[1]-tl[1]));
 if(r.playerStart)drawPlayer(r.playerStart);
 r.entities.filter(e=>e.kind==="wall").forEach(drawWall);
 r.entities.filter(e=>e.kind!=="wall").forEach(drawEntity);
 r.doors.forEach(drawDoor);
}
const MAP_SPACING=[12,9],MAP_ROOM_HALF=[4.5,3];
function mapRoomCenter(r){return [r.grid[0]*MAP_SPACING[0],r.grid[1]*MAP_SPACING[1]]}
function mapDoorWorldPosition(r,d){
 const c=mapRoomCenter(r),hx=MAP_ROOM_HALF[0],hy=MAP_ROOM_HALF[1],side=d.side||"East";
 if(side==="East"||side==="West"){const n=clamp((d.position?.[1]||0)/(r.bounds.height/2||1),-.85,.85);return [c[0]+(side==="East"?hx:-hx),c[1]+n*hy*.8]}
 const n=clamp((d.position?.[0]||0)/(r.bounds.width/2||1),-.85,.85);return [c[0]+n*hx*.8,c[1]+(side==="North"?hy:-hy)]
}
function mapDoorTangent(d){return ({East:[1,0],West:[-1,0],North:[0,1],South:[0,-1]})[d.side]||[1,0]}
function drawMapConnection(c,previewEnd=null){
 const from=findDoor(c.fromDoorId),to=findDoor(c.toDoorId);if(!from)return;const a=worldToScreen(mapDoorWorldPosition(from.room,from.door));
 const b=previewEnd||(!to?null:worldToScreen(mapDoorWorldPosition(to.room,to.door)));if(!b)return;
 const ta=mapDoorTangent(from.door),tb=to?mapDoorTangent(to.door):[-ta[0],-ta[1]],curve=Math.max(35,Math.hypot(b[0]-a[0],b[1]-a[1])*.28);
 ctx.strokeStyle=previewEnd?"#fff2a8":"#68bdf3";ctx.lineWidth=previewEnd?3:2;ctx.beginPath();ctx.moveTo(a[0],a[1]);ctx.bezierCurveTo(a[0]+ta[0]*curve,a[1]-ta[1]*curve,b[0]-tb[0]*curve,b[1]+tb[1]*curve,b[0],b[1]);ctx.stroke();
 if(!previewEnd){ctx.fillStyle="#68bdf3";ctx.beginPath();ctx.arc((a[0]+b[0])/2,(a[1]+b[1])/2,3,0,Math.PI*2);ctx.fill()}
}
function drawLevelMap(){
 state.connections.forEach(c=>drawMapConnection(c));
 for(const r of state.rooms){const c=worldToScreen(mapRoomCenter(r)),w=MAP_ROOM_HALF[0]*2*state.editor.zoom,h=MAP_ROOM_HALF[1]*2*state.editor.zoom;
  ctx.fillStyle=r.id===state.activeRoomId?"#26384b":"#1b2430";ctx.strokeStyle=r.id===state.activeRoomId?"#79c9ff":"#6d7b91";ctx.lineWidth=r.id===state.activeRoomId?3:2;ctx.fillRect(c[0]-w/2,c[1]-h/2,w,h);ctx.strokeRect(c[0]-w/2,c[1]-h/2,w,h);
  ctx.fillStyle="#eef3fb";ctx.font="600 12px system-ui";ctx.textAlign="center";ctx.fillText(r.displayName,c[0],c[1]-4);ctx.fillStyle="#9eabc0";ctx.font="10px system-ui";ctx.fillText(`${r.bounds.width}×${r.bounds.height} · ${r.doors.length} doors`,c[0],c[1]+13);
  for(const d of r.doors){const p=worldToScreen(mapDoorWorldPosition(r,d));ctx.fillStyle=d.id===state.editor.selectedId?"#fff":"#ffd166";ctx.strokeStyle="#604d20";ctx.lineWidth=2;ctx.beginPath();ctx.arc(p[0],p[1],7,0,Math.PI*2);ctx.fill();ctx.stroke()}
 }
 if(pointer.mode==="connect-door"&&pointer.sourceDoorId)drawMapConnection({fromDoorId:pointer.sourceDoorId},pointer.last)
}
function mapHitTest(screen){
 for(const r of [...state.rooms].reverse())for(const d of [...r.doors].reverse()){const p=worldToScreen(mapDoorWorldPosition(r,d));if(Math.hypot(screen[0]-p[0],screen[1]-p[1])<=12)return {type:"door",room:r,door:d}}
 const wp=screenToWorld(screen);for(const r of [...state.rooms].reverse()){const c=mapRoomCenter(r);if(Math.abs(wp[0]-c[0])<=MAP_ROOM_HALF[0]&&Math.abs(wp[1]-c[1])<=MAP_ROOM_HALF[1])return {type:"room",room:r}}
 return null
}
function withTransform(e,fn){
 const p=worldToScreen(e.position);ctx.save();ctx.translate(p[0],p[1]);ctx.rotate(-deg2rad(e.rotation||0));fn();ctx.restore();
}
function drawPlayer(p){
 withTransform(p,()=>{const s=10;ctx.fillStyle="#6ff0a0";ctx.beginPath();ctx.moveTo(s,0);ctx.lineTo(-s*.7,-s*.65);ctx.lineTo(-s*.7,s*.65);ctx.closePath();ctx.fill();});
}
function isSelected(id){return state.editor.selectedId===id}
function drawEntity(e){
 withTransform(e,()=>{
  const z=state.editor.zoom,wallSize=e.object==="prop.wall-2x2"?2:e.object==="prop.wall-1x1"?1:0,sz=wallSize?z*wallSize*.5:clamp(z*.35,8,18);
  ctx.fillStyle=e.kind==="enemy"?"#ff737f":e.kind==="teleporter"?"#b784ff":"#68bdf3";
  ctx.strokeStyle=isSelected(e.id)?"#fff":"#16202b";ctx.lineWidth=isSelected(e.id)?3:1;
  if(e.kind==="enemy"){ctx.beginPath();ctx.moveTo(sz,0);ctx.lineTo(-sz*.7,-sz*.7);ctx.lineTo(-sz*.7,sz*.7);ctx.closePath();ctx.fill();ctx.stroke()}
  else if(e.kind==="teleporter"){ctx.lineWidth=isSelected(e.id)?4:3;ctx.beginPath();ctx.arc(0,0,sz,0,Math.PI*2);ctx.stroke()}
  else{ctx.beginPath();ctx.rect(-sz,wallSize?-sz:-sz*.7,sz*2,wallSize?sz*2:sz*1.4);ctx.fill();ctx.stroke()}
 });
 labelEntity(e);
}
function labelEntity(e){const p=worldToScreen(e.position);ctx.fillStyle="#dbe7f6";ctx.font="10px system-ui";ctx.textAlign="center";ctx.fillText((e.id||"").split(".").pop(),p[0],p[1]+25)}
