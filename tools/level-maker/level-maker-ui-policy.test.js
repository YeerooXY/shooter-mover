"use strict";

const assert = require("assert");
const policy = require("./level-maker-ui-policy");

assert.equal(policy.isPlaceableAsset({
  id: "prop.crate",
  source: "Assets/ShooterMover/ContentPackages/Props/crate.json",
}), true);

assert.equal(policy.isPlaceableAsset({
  id: "enemy.gunner-droid",
  source: "Content/Enemies/gunner-droid.json",
}), true);

assert.equal(policy.isPlaceableAsset({
  id: "prop.003c93c3",
  source: "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/Level3/Rooms/Room21/props.json",
}), false);

assert.equal(policy.isPlaceableAsset({
  id: "door.generated",
  source: "Assets\\ShooterMover\\Content\\Definitions\\Missions\\Rooms\\Levels\\Level3\\Rooms\\Room21\\doors.json",
}), false);

assert.equal(policy.isPlaceableAsset({
  id: "047c9cd9",
  source: "level-reference",
}), false);

assert.equal(policy.isPlaceableAsset({
  id: "prop.04bc4721",
  source: "level-reference",
}), false);

assert.equal(policy.isPlaceableAsset({
  id: "prop.manual",
  source: "manual",
}), true);

assert.equal(policy.isPlaceableAsset({
  id: "prop.instance-1",
  source: "manual",
}, new Set(["prop.instance-1"])), false);

assert.equal(policy.isOpaqueInstanceId("0bb70945"), true);
assert.equal(policy.isOpaqueInstanceId("enemy.gunner-droid"), false);

console.log("level-maker-ui-policy tests passed");
