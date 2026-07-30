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
 const enemies=r.entities.filter(e=>e.kind==="enemy").map(e=>({id:e.id,object:e.object,level:e.level||1,position:e.position.map(round),rotation:round(e.rotation||0)}));
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
 const target=cleanSlug(state.level.targetFolder),base=`Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/${target}`;
 const roomBuilds=state.rooms.map(runtimeRoomFiles);
 const roomIndex=state.rooms.map((r,i)=>({room_id:r.id,grid_position:r.grid,slot:r.slot||1,folder:roomBuilds[i].folder}));
 const nodes=state.rooms.map(r=>({room_id:r.id,grid_position:r.grid,slot:r.slot||1,label:r.displayName,visible_on_map:r.visibleOnMap!==false}));
 const endpoint=doorId=>{const f=findDoor(doorId);return{room_id:f?.room.id||"",door_id:doorId||""}};
 const connections=state.connections.map(c=>({connection_id:c.id,from:endpoint(c.fromDoorId),to:endpoint(c.toDoorId),travel_policy:c.travelPolicy||"Bidirectional"}));
 const level={schema_version:2,level_id:state.level.id,authoring_state:"validated-playable",runtime_import_status:"compiler-ready",start_room_id:state.level.startRoomId,final_exit:{room_id:state.level.finalRoomId,door_id:state.level.finalExitDoorId||""},room_ids:state.rooms.map(r=>r.id),rooms:roomIndex};
 const files={};
 files[`${base}/level.json`]=pretty(level);files[`${base}/map.json`]=pretty({schema_version:2,nodes,connections});
 roomBuilds.forEach(rb=>Object.entries(rb.documents).forEach(([name,obj])=>files[`${base}/Rooms/${rb.folder}/${name}`]=pretty(obj)));
 files[`${base}/web-level-pack.smlvlpack`]=`Shooter Mover web level pack\nlevel_id=${state.level.id}\ntarget=${target}\n`;
 files[`LevelDesigner/${target}.smlvl.json`]=pretty(state);
 files[`LevelDesigner/${target}.catalogue.snapshot.json`]=pretty({generated_at:new Date().toISOString(),assets:state.catalog.map(({previewUrl,...a})=>a)});
 files["README_IMPORT.txt"]=`SHOOTER MOVER WEB LEVEL PACK\n\n1. Extract this ZIP into the Shooter Mover Unity project root.\n2. The level source will land in:\n   ${base}\n3. With the included bridge installed, Unity automatically compiles the package after import.\n4. Default target Level1 replaces the existing playable Level 1 source slot.\n5. Custom target folders compile a Resources/Levels/<Target>RoomContent.asset. If the game's level-selection catalogue is explicit, register that new resource there.\n\nOpen LevelDesigner/${target}.smlvl.json in the HTML editor to continue editing.\n\nCompatibility notes:\n- Grid-painted floor cells are rectangle-compressed into floor.json; enemies, rotation, level, props, walls-as-props, rooms, doors, room completion and dragged map connections use the current schema-v2 package.\n- Per-instance drop chance/profile, wall dimensions, custom logic and teleporters are retained in the editor project. They require matching runtime support before they affect gameplay.\n- The compiler validates object IDs against Shooter Mover's built-in room content catalogue, so scan the current Unity project and use discovered IDs.\n`;
 if(state.level.includeBridge!==false)files["Assets/ShooterMover/Editor/LevelDesign/Web/WebLevelPackCompiler.cs"]=UNITY_BRIDGE;
 return files;
}
function pretty(x){return JSON.stringify(x,null,2)+"\n"}

const UNITY_BRIDGE=`#if UNITY_EDITOR
using System;
using System.IO;
using ShooterMover.Editor.LevelDesign.Foundation;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Web
{
    public sealed class WebLevelPackCompiler : AssetPostprocessor
    {
        private const string MarkerName = "web-level-pack.smlvlpack";

        static WebLevelPackCompiler()
        {
            // The bridge itself may compile after the marker is first imported. Scanning after
            // every editor-domain reload guarantees a freshly extracted pack is picked up.
            EditorApplication.delayCall += CompileAll;
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int index = 0; index < importedAssets.Length; index++)
            {
                string path = importedAssets[index].Replace('\\\\', '/');
                if (path.EndsWith("/" + MarkerName, StringComparison.OrdinalIgnoreCase))
                {
                    string captured = path;
                    EditorApplication.delayCall += () => CompileMarker(captured);
                }
            }
        }

        [MenuItem("Tools/Shooter Mover/Level Design/Compile Imported Web Levels", priority = 252)]
        private static void CompileAll()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string levelsRoot = Path.Combine(
                projectRoot,
                "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels");
            if (!Directory.Exists(levelsRoot))
            {
                Debug.LogWarning("Shooter Mover level source folder was not found.");
                return;
            }

            string[] markers = Directory.GetFiles(
                levelsRoot,
                MarkerName,
                SearchOption.AllDirectories);
            Array.Sort(markers, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < markers.Length; index++)
            {
                CompileMarker(ToAssetPath(markers[index]));
            }
        }

        private static void CompileMarker(string markerAssetPath)
        {
            try
            {
                string sourceRoot = Path.GetDirectoryName(markerAssetPath)
                    .Replace('\\\\', '/');
                string target = Path.GetFileName(sourceRoot);
                string generated =
                    "Assets/ShooterMover/Content/Generated/Missions/Rooms/Levels/" + target;
                string resource =
                    "Assets/ShooterMover/Resources/Levels/" + target + "RoomContent.asset";

                LevelGridAssetCompiler.CompileToAsset(sourceRoot, generated, resource);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "Compiled web level pack '" + target + "' to " + resource + ".");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Web level pack compilation failed for '" + markerAssetPath
                    + "': " + exception);
            }
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName
                .Replace('\\\\', '/').TrimEnd('/');
            string normalized = Path.GetFullPath(absolutePath).Replace('\\\\', '/');
            return normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length + 1)
                : normalized;
        }
    }
}
#endif
`;

function crcTable(){
 const t=[];for(let n=0;n<256;n++){let c=n;for(let k=0;k<8;k++)c=(c&1)?0xedb88320^(c>>>1):c>>>1;t[n]=c>>>0}return t
}
const CRC_TABLE=crcTable();
function crc32(bytes){let c=0xffffffff;for(const b of bytes)c=CRC_TABLE[(c^b)&255]^(c>>>8);return(c^0xffffffff)>>>0}
function u16(n){return new Uint8Array([n&255,(n>>>8)&255])}
function u32(n){return new Uint8Array([n&255,(n>>>8)&255,(n>>>16)&255,(n>>>24)&255])}
function joinBytes(parts){const len=parts.reduce((a,b)=>a+b.length,0),out=new Uint8Array(len);let o=0;for(const p of parts){out.set(p,o);o+=p.length}return out}
function zipStore(files){
 const enc=new TextEncoder(),locals=[],centrals=[];let offset=0;
 for(const [name,text] of Object.entries(files)){
  const nb=enc.encode(name),data=enc.encode(text),crc=crc32(data);
  const local=joinBytes([u32(0x04034b50),u16(20),u16(0x800),u16(0),u16(0),u16(0),u32(crc),u32(data.length),u32(data.length),u16(nb.length),u16(0),nb,data]);
  locals.push(local);
  const central=joinBytes([u32(0x02014b50),u16(20),u16(20),u16(0x800),u16(0),u16(0),u16(0),u32(crc),u32(data.length),u32(data.length),u16(nb.length),u16(0),u16(0),u16(0),u16(0),u32(0),u32(offset),nb]);
  centrals.push(central);offset+=local.length;
 }
 const cd=joinBytes(centrals),body=joinBytes(locals),end=joinBytes([u32(0x06054b50),u16(0),u16(0),u16(centrals.length),u16(centrals.length),u32(cd.length),u32(body.length),u16(0)]);
 return new Blob([body,cd,end],{type:"application/zip"});
}
function download(blob,name){const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(a.href),3000)}
function exportZip(){
 const v=validate(),errors=v.filter(x=>x.severity==="error");
 if(errors.length){showValidation();setStatus("Export blocked by validation errors.","bad");return}
 const files=buildExportFiles(),blob=zipStore(files),name=`shooter-mover-${cleanSlug(state.level.targetFolder).toLowerCase()}-level-pack.zip`;download(blob,name);setStatus(`Exported ${name} (${Object.keys(files).length} files).`,"good")
}
