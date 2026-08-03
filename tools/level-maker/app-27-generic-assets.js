"use strict";

{
  const previousPlaceAt = placeAt;

  placeAt = function placeGenericGroupedAsset(tool, position) {
    if (tool !== "prop") return previousPlaceAt(tool, position);

    const asset = state.assets.find(value => value.id === state.editor.selectedAssetId);
    if (!asset || asset.type === "prop") return previousPlaceAt(tool, position);
    if (["enemy", "floor", "door"].includes(asset.type)) return previousPlaceAt(tool, position);

    const room = currentRoom();
    const cellPosition = snapToRoomCellCenter(room, position);
    const group = AuthoringUx.assetGroup(asset);
    let entity = room.entities.find(value =>
      value.kind === "prop"
      && Math.abs(value.position[0] - cellPosition[0]) < 0.001
      && Math.abs(value.position[1] - cellPosition[1]) < 0.001
    );

    if (!entity) {
      entity = {
        id: uid("prop"),
        kind: "prop",
        object: asset.id,
        position: cellPosition,
        rotation: 0,
        blocksMovement: false,
        layer: group,
      };
      room.entities.push(entity);
    }

    entity.object = asset.id;
    entity.position = cellPosition;
    entity.rotation = Number(entity.rotation) || 0;
    entity.layer = group;
    entity.blocksMovement = group === "static" && asset.type !== "decor";
    state.editor.selectedId = entity.id;
    return entity;
  };
}
