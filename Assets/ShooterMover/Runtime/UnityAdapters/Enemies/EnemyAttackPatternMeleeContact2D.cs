using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternMeleeContactReporterV1
    {
        void ReportMeleeContact(
            StableId sourceEntityStableId,
            Collider2D candidate);
    }

    /// <summary>
    /// Physics callback adapter for schema-v2 melee and pounce windows. It reports candidates only;
    /// it never evaluates eligibility and never mutates player health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttackPatternMeleeContact2D : MonoBehaviour
    {
        private StableId sourceEntityStableId;
        private IEnemyAttackPatternMeleeContactReporterV1 reporter;

        public bool IsConfigured
        {
            get
            {
                return sourceEntityStableId != null
                    && reporter != null;
            }
        }

        public void Configure(
            StableId sourceEntityStableId,
            IEnemyAttackPatternMeleeContactReporterV1 reporter)
        {
            if (sourceEntityStableId == null)
            {
                throw new ArgumentNullException(nameof(sourceEntityStableId));
            }
            if (reporter == null)
            {
                throw new ArgumentNullException(nameof(reporter));
            }
            if (IsConfigured
                && (this.sourceEntityStableId != sourceEntityStableId
                    || !ReferenceEquals(this.reporter, reporter)))
            {
                throw new InvalidOperationException(
                    "Melee contact adapter cannot be rebound to another authority.");
            }
            this.sourceEntityStableId = sourceEntityStableId;
            this.reporter = reporter;
        }

        public void Unconfigure(IEnemyAttackPatternMeleeContactReporterV1 owner)
        {
            if (ReferenceEquals(reporter, owner))
            {
                reporter = null;
                sourceEntityStableId = null;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Report(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Report(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Report(collision == null ? null : collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            Report(collision == null ? null : collision.collider);
        }

        private void Report(Collider2D candidate)
        {
            if (reporter != null
                && sourceEntityStableId != null
                && candidate != null)
            {
                reporter.ReportMeleeContact(
                    sourceEntityStableId,
                    candidate);
            }
        }
    }
}
