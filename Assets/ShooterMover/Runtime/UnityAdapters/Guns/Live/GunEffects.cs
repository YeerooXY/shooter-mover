using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public sealed class GunEffectEmitter : MonoBehaviour,
        IInventoryGunEffectBatchSink
    {
        private readonly Dictionary<string, AcceptedEmission> accepted =
            new Dictionary<string, AcceptedEmission>(StringComparer.Ordinal);
        private readonly List<GunEffect> emitted =
            new List<GunEffect>();

        public IReadOnlyList<GunEffect> EmittedEffects
        {
            get { return emitted; }
        }

        public int AcceptedBatchCount
        {
            get { return accepted.Count; }
        }

        public GunEffectBatchSinkResult TryAccept(
            InventoryGunEffectBatch batch)
        {
            if (batch == null
                || batch.CoreBatch == null
                || batch.Identity == null
                || batch.CoreBatch.EffectCount < 1)
            {
                return GunEffectBatchSinkResult.Reject(
                    "gun-live-unity-batch-invalid");
            }

            string operationKey = OperationKey(batch.Identity);
            AcceptedEmission existing;
            if (accepted.TryGetValue(operationKey, out existing))
            {
                return string.Equals(
                        existing.Fingerprint,
                        batch.Fingerprint,
                        StringComparison.Ordinal)
                    ? GunEffectBatchSinkResult.AlreadyAccepted()
                    : GunEffectBatchSinkResult.Reject(
                        "gun-live-unity-conflicting-duplicate");
            }

            GameObject batchRoot = new GameObject(
                "GunEffectBatch_" + batch.Identity.FireOperationId);
            batchRoot.transform.SetParent(transform, false);
            batchRoot.SetActive(false);
            var staged = new List<GunEffect>(
                batch.CoreBatch.EffectCount);

            try
            {
                for (int index = 0;
                    index < batch.CoreBatch.Effects.Count;
                    index++)
                {
                    IGunEffectDescription effect =
                        batch.CoreBatch.Effects[index];
                    var effectObject = new GameObject(
                        "GunEffect_" + index + "_" + effect.Kind);
                    effectObject.transform.SetParent(
                        batchRoot.transform,
                        false);
                    var instance = effectObject.AddComponent<
                        GunEffect>();
                    if (!instance.TryConfigure(effect))
                    {
                        throw new InvalidOperationException(
                            "Unity effect configuration rejected ordinal "
                            + index + ".");
                    }

                    staged.Add(instance);
                }

                batchRoot.SetActive(true);
                for (int index = 0; index < staged.Count; index++)
                {
                    if (!staged[index].BeginEmission())
                    {
                        throw new InvalidOperationException(
                            "Unity effect launch rejected ordinal "
                            + index + ".");
                    }
                }

                emitted.AddRange(staged);
                accepted.Add(
                    operationKey,
                    new AcceptedEmission(batch.Fingerprint, batchRoot));
                return GunEffectBatchSinkResult.Accept();
            }
            catch
            {
                if (batchRoot != null)
                {
                    batchRoot.SetActive(false);
                    Destroy(batchRoot);
                }

                return GunEffectBatchSinkResult.Reject(
                    "gun-live-unity-batch-staging-failed");
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

        private static string OperationKey(
            GunEffectIdentity identity)
        {
            return identity.ActorId + "|"
                + identity.LifecycleGeneration + "|"
                + identity.FireOperationId;
        }

        private sealed class AcceptedEmission
        {
            public AcceptedEmission(
                string fingerprint,
                GameObject root)
            {
                Fingerprint = fingerprint;
                Root = root;
            }

            public string Fingerprint { get; }
            public GameObject Root { get; }
        }
    }
}
