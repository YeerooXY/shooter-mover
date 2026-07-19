using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public sealed class InventoryWeaponEffectEmitter2D : MonoBehaviour,
        IInventoryWeaponEffectBatchSink
    {
        private readonly Dictionary<string, AcceptedEmission> accepted =
            new Dictionary<string, AcceptedEmission>(StringComparer.Ordinal);
        private readonly List<InventoryWeaponEffectInstance2D> emitted =
            new List<InventoryWeaponEffectInstance2D>();

        public IReadOnlyList<InventoryWeaponEffectInstance2D> EmittedEffects
        {
            get { return emitted; }
        }

        public int AcceptedBatchCount { get { return accepted.Count; } }

        public WeaponEffectBatchSinkResult TryAccept(InventoryWeaponEffectBatch batch)
        {
            if (batch == null
                || batch.CoreBatch == null
                || batch.Identity == null
                || batch.CoreBatch.EffectCount < 1)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "weapon-live-unity-batch-invalid");
            }

            string operationKey = OperationKey(batch.Identity);
            AcceptedEmission existing;
            if (accepted.TryGetValue(operationKey, out existing))
            {
                return string.Equals(
                        existing.Fingerprint,
                        batch.Fingerprint,
                        StringComparison.Ordinal)
                    ? WeaponEffectBatchSinkResult.AlreadyAccepted()
                    : WeaponEffectBatchSinkResult.Reject(
                        "weapon-live-unity-conflicting-duplicate");
            }

            GameObject batchRoot = new GameObject(
                "WeaponEffectBatch_" + batch.Identity.FireOperationId);
            batchRoot.transform.SetParent(transform, false);
            batchRoot.SetActive(false);
            var staged = new List<InventoryWeaponEffectInstance2D>(
                batch.CoreBatch.EffectCount);

            try
            {
                for (int index = 0; index < batch.CoreBatch.Effects.Count; index++)
                {
                    IWeaponEffectDescription effect = batch.CoreBatch.Effects[index];
                    var effectObject = new GameObject(
                        "WeaponEffect_" + index + "_" + effect.Kind);
                    effectObject.transform.SetParent(batchRoot.transform, false);
                    var instance = effectObject.AddComponent<InventoryWeaponEffectInstance2D>();
                    if (!instance.TryConfigure(effect))
                    {
                        throw new InvalidOperationException(
                            "Unity effect configuration rejected ordinal " + index + ".");
                    }

                    staged.Add(instance);
                }

                batchRoot.SetActive(true);
                emitted.AddRange(staged);
                accepted.Add(
                    operationKey,
                    new AcceptedEmission(batch.Fingerprint, batchRoot));
                return WeaponEffectBatchSinkResult.Accept();
            }
            catch
            {
                if (batchRoot != null)
                {
                    Destroy(batchRoot);
                }

                return WeaponEffectBatchSinkResult.Reject(
                    "weapon-live-unity-batch-staging-failed");
            }
        }

        public void ClearEmittedEffects()
        {
            foreach (AcceptedEmission emission in accepted.Values)
            {
                if (emission.Root != null)
                {
                    Destroy(emission.Root);
                }
            }

            accepted.Clear();
            emitted.Clear();
        }

        private static string OperationKey(WeaponEffectIdentity identity)
        {
            return identity.ActorId + "|"
                + identity.LifecycleGeneration + "|"
                + identity.FireOperationId;
        }

        private sealed class AcceptedEmission
        {
            public AcceptedEmission(string fingerprint, GameObject root)
            {
                Fingerprint = fingerprint;
                Root = root;
            }

            public string Fingerprint { get; }
            public GameObject Root { get; }
        }
    }

}
