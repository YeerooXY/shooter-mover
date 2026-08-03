"use strict";

(function expose(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.EnemySchema = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function build() {
  const SCHEMA = 1;
  const ID = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
  const MOVES = new Set(["direct", "wander", "strafe", "fly", "stationary"]);
  const ATTACK_KINDS = new Set(["shot", "contact", "suicide"]);
  const FIRE_PATTERNS = new Set(["single", "simultaneous", "alternate", "round-robin"]);
  const DISTRIBUTIONS = new Set(["even", "random"]);
  const DAMAGE_TYPES = new Set(["kinetic", "thermal", "electric", "explosive", "impact"]);
  const STACK_RULES = new Set(["refresh", "stack", "replace"]);
  const DELIVERY_KINDS = new Set(["projectile"]);
  const SHAPES = Object.freeze({
    circle: "supported",
    box: "planned",
    ellipse: "planned",
    polygon: "planned"
  });

  function isObject(value) {
    return value !== null && typeof value === "object" && !Array.isArray(value);
  }

  function finite(value) {
    return typeof value === "number" && Number.isFinite(value);
  }

  function positive(value) {
    return finite(value) && value > 0;
  }

  function nonNegative(value) {
    return finite(value) && value >= 0;
  }

  function optionalText(errors, value, path) {
    if (value !== undefined && (typeof value !== "string" || value.trim() === "")) {
      errors.push(`${path} must be a non-empty string when provided.`);
    }
  }

  function validateKnownKeys(errors, value, allowed, path) {
    if (!isObject(value)) return;
    Object.keys(value).forEach(key => {
      if (!allowed.has(key)) errors.push(`${path ? `${path}.` : ""}${key} is not supported.`);
    });
  }

  function validateId(errors, value, path) {
    if (typeof value !== "string" || !ID.test(value)) {
      errors.push(`${path} must use lower-case letters, digits, and single hyphens.`);
    }
  }

  function validatePoint(errors, value, path) {
    if (!isObject(value)) {
      errors.push(`${path} must be an object.`);
      return;
    }
    if (!finite(value.x)) errors.push(`${path}.x must be a finite number.`);
    if (!finite(value.y)) errors.push(`${path}.y must be a finite number.`);
    validateKnownKeys(errors, value, new Set(["x", "y"]), path);
  }

  function validateBody(errors, body) {
    if (!isObject(body)) {
      errors.push("body is required and must be an object.");
      return;
    }
    if (typeof body.shape !== "string" || !Object.hasOwn(SHAPES, body.shape)) {
      errors.push("body.shape must be circle, box, ellipse, or polygon.");
      return;
    }
    if (SHAPES[body.shape] !== "supported") {
      errors.push(`body.shape '${body.shape}' is reserved but not supported yet.`);
      return;
    }
    if (!positive(body.radius)) errors.push("body.radius must be greater than zero for a circle.");
    validatePoint(errors, body.offset, "body.offset");
    validateKnownKeys(errors, body, new Set(["shape", "radius", "offset"]), "body");
  }

  function validateMovement(errors, movement) {
    if (!isObject(movement)) {
      errors.push("movement is required and must be an object.");
      return;
    }
    if (!MOVES.has(movement.kind)) {
      errors.push("movement.kind must be direct, wander, strafe, fly, or stationary.");
    }
    if (!nonNegative(movement.speed)) errors.push("movement.speed must be zero or greater.");
    if (movement.kind !== "stationary" && movement.speed === 0) {
      errors.push("movement.speed must be greater than zero unless movement.kind is stationary.");
    }
    validateKnownKeys(errors, movement, new Set(["kind", "speed"]), "movement");
  }

  function validateMounts(errors, mounts) {
    if (!Array.isArray(mounts)) {
      errors.push("mounts must be an array.");
      return new Set();
    }
    const ids = new Set();
    mounts.forEach((mount, index) => {
      const path = `mounts[${index}]`;
      if (!isObject(mount)) {
        errors.push(`${path} must be an object.`);
        return;
      }
      validateId(errors, mount.id, `${path}.id`);
      if (ids.has(mount.id)) errors.push(`Duplicate mount ID '${mount.id}'.`);
      ids.add(mount.id);
      validatePoint(errors, mount.position, `${path}.position`);
      if (!finite(mount.rotation)) errors.push(`${path}.rotation must be a finite number of degrees.`);
      optionalText(errors, mount.art, `${path}.art`);
      validateKnownKeys(errors, mount, new Set(["id", "position", "rotation", "art"]), path);
    });
    return ids;
  }

  function validateRange(errors, range, path) {
    if (!isObject(range)) {
      errors.push(`${path} must be an object.`);
      return;
    }
    if (!nonNegative(range.min)) errors.push(`${path}.min must be zero or greater.`);
    if (!positive(range.max)) errors.push(`${path}.max must be greater than zero.`);
    if (finite(range.min) && finite(range.max) && range.max < range.min) {
      errors.push(`${path}.max must be greater than or equal to ${path}.min.`);
    }
    validateKnownKeys(errors, range, new Set(["min", "max"]), path);
  }

  function validateDamage(errors, damage, path) {
    if (!Array.isArray(damage) || damage.length === 0) {
      errors.push(`${path} must contain at least one damage component.`);
      return;
    }
    damage.forEach((component, index) => {
      const itemPath = `${path}[${index}]`;
      if (!isObject(component)) {
        errors.push(`${itemPath} must be an object.`);
        return;
      }
      if (!DAMAGE_TYPES.has(component.type)) {
        errors.push(`${itemPath}.type must be kinetic, thermal, electric, explosive, or impact.`);
      }
      const direct = component.amount !== undefined;
      const dot = component.perSecond !== undefined || component.duration !== undefined || component.stack !== undefined;
      if (direct === dot) {
        errors.push(`${itemPath} must define either amount or the complete perSecond/duration/stack package.`);
      } else if (direct) {
        if (!positive(component.amount)) errors.push(`${itemPath}.amount must be greater than zero.`);
      } else {
        if (!positive(component.perSecond)) errors.push(`${itemPath}.perSecond must be greater than zero.`);
        if (!positive(component.duration)) errors.push(`${itemPath}.duration must be greater than zero.`);
        if (!STACK_RULES.has(component.stack)) errors.push(`${itemPath}.stack must be refresh, stack, or replace.`);
      }
      validateKnownKeys(errors, component, new Set(["type", "amount", "perSecond", "duration", "stack"]), itemPath);
    });
  }

  function validateShotAttack(errors, attack, path, mountIds) {
    validateId(errors, attack.shot, `${path}.shot`);
    if (!Array.isArray(attack.emitters) || attack.emitters.length === 0) {
      errors.push(`${path}.emitters must contain at least one mount ID.`);
    } else {
      const seen = new Set();
      attack.emitters.forEach((emitter, index) => {
        if (typeof emitter !== "string" || !ID.test(emitter)) {
          errors.push(`${path}.emitters[${index}] must be a valid mount ID.`);
        } else if (!mountIds.has(emitter)) {
          errors.push(`${path}.emitters[${index}] references unknown mount '${emitter}'.`);
        }
        if (seen.has(emitter)) errors.push(`${path}.emitters contains duplicate mount '${emitter}'.`);
        seen.add(emitter);
      });
    }
    if (!FIRE_PATTERNS.has(attack.firePattern)) {
      errors.push(`${path}.firePattern must be single, simultaneous, alternate, or round-robin.`);
    } else if (Array.isArray(attack.emitters)) {
      if (attack.firePattern === "single" && attack.emitters.length !== 1) {
        errors.push(`${path}.firePattern single requires exactly one emitter.`);
      }
      if ((attack.firePattern === "alternate" || attack.firePattern === "round-robin") && attack.emitters.length < 2) {
        errors.push(`${path}.firePattern ${attack.firePattern} requires at least two emitters.`);
      }
    }
    if (!isObject(attack.sequence)) {
      errors.push(`${path}.sequence must be an object.`);
    } else {
      if (!Number.isInteger(attack.sequence.triggers) || attack.sequence.triggers < 1) {
        errors.push(`${path}.sequence.triggers must be an integer of at least 1.`);
      }
      if (!nonNegative(attack.sequence.interval)) errors.push(`${path}.sequence.interval must be zero or greater.`);
      validateKnownKeys(errors, attack.sequence, new Set(["triggers", "interval"]), `${path}.sequence`);
    }
    if (!isObject(attack.volley)) {
      errors.push(`${path}.volley must be an object.`);
    } else {
      if (!Number.isInteger(attack.volley.shotsPerTrigger) || attack.volley.shotsPerTrigger < 1) {
        errors.push(`${path}.volley.shotsPerTrigger must be an integer of at least 1.`);
      }
      if (!nonNegative(attack.volley.spread)) errors.push(`${path}.volley.spread must be zero or greater.`);
      if (!DISTRIBUTIONS.has(attack.volley.distribution)) {
        errors.push(`${path}.volley.distribution must be even or random.`);
      }
      validateKnownKeys(errors, attack.volley, new Set(["shotsPerTrigger", "spread", "distribution"]), `${path}.volley`);
    }
  }

  function validateAttacks(errors, attacks, mountIds) {
    if (!Array.isArray(attacks)) {
      errors.push("attacks must be an array.");
      return;
    }
    const ids = new Set();
    attacks.forEach((attack, index) => {
      const path = `attacks[${index}]`;
      if (!isObject(attack)) {
        errors.push(`${path} must be an object.`);
        return;
      }
      validateId(errors, attack.id, `${path}.id`);
      if (ids.has(attack.id)) errors.push(`Duplicate attack ID '${attack.id}'.`);
      ids.add(attack.id);
      if (!ATTACK_KINDS.has(attack.kind)) errors.push(`${path}.kind must be shot, contact, or suicide.`);
      if (!nonNegative(attack.cooldown)) errors.push(`${path}.cooldown must be zero or greater.`);
      validateRange(errors, attack.range, `${path}.range`);
      validateDamage(errors, attack.damage, `${path}.damage`);
      if (attack.kind === "shot") validateShotAttack(errors, attack, path, mountIds);
      if (attack.kind !== "shot") {
        ["shot", "emitters", "firePattern", "sequence", "volley"].forEach(key => {
          if (attack[key] !== undefined) errors.push(`${path}.${key} is only valid for shot attacks.`);
        });
      }
      validateKnownKeys(
        errors,
        attack,
        new Set(["id", "kind", "shot", "emitters", "firePattern", "cooldown", "sequence", "volley", "range", "damage"]),
        path
      );
    });
  }

  function validateEnemy(enemy, expectedId) {
    const errors = [];
    if (!isObject(enemy)) return ["Enemy root must be an object."];
    if (enemy.schema !== SCHEMA) errors.push(`schema must be ${SCHEMA}.`);
    validateId(errors, enemy.id, "id");
    if (expectedId && enemy.id !== expectedId) errors.push(`id must match the file name '${expectedId}'.`);
    if (typeof enemy.name !== "string" || enemy.name.trim() === "") errors.push("name is required.");
    if (!Array.isArray(enemy.tags)) {
      errors.push("tags must be an array.");
    } else {
      const seen = new Set();
      enemy.tags.forEach((tag, index) => {
        validateId(errors, tag, `tags[${index}]`);
        if (seen.has(tag)) errors.push(`Duplicate tag '${tag}'.`);
        seen.add(tag);
      });
    }
    if (!positive(enemy.hp)) errors.push("hp must be greater than zero.");
    if (!positive(enemy.healthPower)) errors.push("healthPower must be greater than zero.");
    validateMovement(errors, enemy.movement);
    if (!positive(enemy.detectionRange)) errors.push("detectionRange must be greater than zero.");
    const mountIds = validateMounts(errors, enemy.mounts);
    validateAttacks(errors, enemy.attacks, mountIds);
    optionalText(errors, enemy.traitPool, "traitPool");
    optionalText(errors, enemy.drops, "drops");
    optionalText(errors, enemy.art, "art");
    validateBody(errors, enemy.body);
    validateKnownKeys(
      errors,
      enemy,
      new Set(["schema", "id", "name", "tags", "hp", "healthPower", "movement", "detectionRange", "mounts", "attacks", "traitPool", "drops", "art", "body"]),
      ""
    );
    return errors;
  }

  function validateShot(shot, expectedId) {
    const errors = [];
    if (!isObject(shot)) return ["Shot root must be an object."];
    if (shot.schema !== SCHEMA) errors.push(`schema must be ${SCHEMA}.`);
    validateId(errors, shot.id, "id");
    if (expectedId && shot.id !== expectedId) errors.push(`id must match the file name '${expectedId}'.`);
    if (!isObject(shot.delivery)) {
      errors.push("delivery is required and must be an object.");
    } else {
      if (!DELIVERY_KINDS.has(shot.delivery.kind)) errors.push("delivery.kind must be projectile.");
      if (!positive(shot.delivery.speed)) errors.push("delivery.speed must be greater than zero.");
      if (!positive(shot.delivery.radius)) errors.push("delivery.radius must be greater than zero.");
      if (!positive(shot.delivery.range)) errors.push("delivery.range must be greater than zero.");
      validateKnownKeys(errors, shot.delivery, new Set(["kind", "speed", "radius", "range"]), "delivery");
    }
    if (!isObject(shot.impact)) {
      errors.push("impact is required and must be an object.");
    } else {
      if (!Number.isInteger(shot.impact.pierce) || shot.impact.pierce < 1) errors.push("impact.pierce must be an integer of at least 1.");
      if (!Number.isInteger(shot.impact.ricochet) || shot.impact.ricochet < 0) errors.push("impact.ricochet must be an integer of zero or greater.");
      if (!nonNegative(shot.impact.knockback)) errors.push("impact.knockback must be zero or greater.");
      validateKnownKeys(errors, shot.impact, new Set(["pierce", "ricochet", "knockback"]), "impact");
    }
    if (!isObject(shot.art)) {
      errors.push("art is required and must be an object.");
    } else {
      optionalText(errors, shot.art.delivery, "art.delivery");
      optionalText(errors, shot.art.trail, "art.trail");
      optionalText(errors, shot.art.impact, "art.impact");
      if (!shot.art.delivery) errors.push("art.delivery is required.");
      validateKnownKeys(errors, shot.art, new Set(["delivery", "trail", "impact"]), "art");
    }
    validateKnownKeys(errors, shot, new Set(["schema", "id", "delivery", "impact", "art"]), "");
    return errors;
  }

  function validateLeveling(value) {
    const errors = [];
    if (!isObject(value)) return ["Leveling root must be an object."];
    if (!Number.isInteger(value.minLevel) || value.minLevel !== 1) errors.push("minLevel must be 1.");
    if (!Number.isInteger(value.maxLevel) || value.maxLevel < 2) errors.push("maxLevel must be an integer greater than 1.");
    if (!positive(value.strengthAtMax) || value.strengthAtMax < 1) errors.push("strengthAtMax must be at least 1.");
    if (!positive(value.damagePower)) errors.push("damagePower must be greater than zero.");
    if (!Array.isArray(value.colors) || value.colors.length < 2) {
      errors.push("colors must contain at least two level/color stops.");
    } else {
      let previous = -Infinity;
      value.colors.forEach((stop, index) => {
        if (!isObject(stop)) {
          errors.push(`colors[${index}] must be an object.`);
          return;
        }
        if (!Number.isInteger(stop.level)) errors.push(`colors[${index}].level must be an integer.`);
        if (Number.isInteger(stop.level) && stop.level <= previous) errors.push("Color stop levels must be strictly increasing.");
        previous = stop.level;
        if (typeof stop.color !== "string" || !/^#[0-9a-fA-F]{6}$/.test(stop.color)) {
          errors.push(`colors[${index}].color must be a six-digit hex color.`);
        }
      });
      if (value.colors[0]?.level !== value.minLevel) errors.push("The first color stop must match minLevel.");
      if (value.colors[value.colors.length - 1]?.level !== value.maxLevel) errors.push("The last color stop must match maxLevel.");
    }
    validateKnownKeys(errors, value, new Set(["minLevel", "maxLevel", "strengthAtMax", "damagePower", "colors"]), "");
    return errors;
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function strengthAt(level, leveling) {
    const t = clamp((level - leveling.minLevel) / (leveling.maxLevel - leveling.minLevel), 0, 1);
    return Math.pow(leveling.strengthAtMax, t);
  }

  function parseHex(value) {
    return [1, 3, 5].map(start => Number.parseInt(value.slice(start, start + 2), 16));
  }

  function toHex(channels) {
    return `#${channels.map(value => Math.round(clamp(value, 0, 255)).toString(16).padStart(2, "0")).join("")}`.toUpperCase();
  }

  function levelColor(level, leveling) {
    const stops = leveling.colors;
    if (level <= stops[0].level) return stops[0].color.toUpperCase();
    if (level >= stops[stops.length - 1].level) return stops[stops.length - 1].color.toUpperCase();
    for (let index = 1; index < stops.length; index += 1) {
      const right = stops[index];
      if (level > right.level) continue;
      const left = stops[index - 1];
      const t = (level - left.level) / (right.level - left.level);
      const a = parseHex(left.color);
      const b = parseHex(right.color);
      return toHex(a.map((value, channel) => value + (b[channel] - value) * t));
    }
    return stops[stops.length - 1].color.toUpperCase();
  }

  function damageMultiplierAt(level, leveling) {
    return Math.pow(strengthAt(level, leveling), leveling.damagePower);
  }

  function resolvedStats(enemy, level, leveling) {
    const strength = strengthAt(level, leveling);
    const hpMultiplier = Math.pow(strength, enemy.healthPower);
    return {
      strength,
      hpMultiplier,
      damageMultiplier: damageMultiplierAt(level, leveling),
      hp: enemy.hp * hpMultiplier,
      color: levelColor(level, leveling)
    };
  }

  function emittersPerTrigger(attack) {
    if (attack.kind !== "shot") return 0;
    if (attack.firePattern === "simultaneous") return attack.emitters.length;
    return 1;
  }

  function projectilesPerSequence(attack) {
    if (attack.kind !== "shot") return 0;
    return attack.sequence.triggers * emittersPerTrigger(attack) * attack.volley.shotsPerTrigger;
  }

  function directDamagePerHit(attack) {
    return attack.damage.reduce((sum, component) => sum + (component.amount || 0), 0);
  }

  return {
    SCHEMA,
    ID,
    MOVES,
    ATTACK_KINDS,
    FIRE_PATTERNS,
    DISTRIBUTIONS,
    DAMAGE_TYPES,
    SHAPES,
    validateEnemy,
    validateShot,
    validateLeveling,
    strengthAt,
    damageMultiplierAt,
    levelColor,
    resolvedStats,
    emittersPerTrigger,
    projectilesPerSequence,
    directDamagePerHit
  };
});
