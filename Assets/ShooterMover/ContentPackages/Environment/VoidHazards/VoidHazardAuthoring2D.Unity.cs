using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Environment.VoidHazards
{
    public sealed partial class VoidHazardAuthoring2D
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleContactEnter(FindTarget(other));
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            HandleContactExit(FindTarget(other));
        }

        private static VoidHazardTarget2D FindTarget(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                VoidHazardTarget2D target = current.GetComponent<VoidHazardTarget2D>();
                if (target != null)
                {
                    return target;
                }

                current = current.parent;
            }

            return null;
        }

        private void Present(
            StableId targetId,
            VoidHazardTargetCategory category,
            VoidHazardContactResult result)
        {
            if (_restartParticipantId == null || targetId == null || result == null)
            {
                return;
            }

            VoidHazardPresentationEvent presentationEvent =
                new VoidHazardPresentationEvent(
                    _restartParticipantId,
                    targetId,
                    category,
                    result);
            IVoidHazardPresentationPort port =
                presentationPort as IVoidHazardPresentationPort;
            if (port != null)
            {
                port.Present(presentationEvent);
            }

            Action<VoidHazardPresentationEvent> handler = PresentationRequested;
            if (handler != null)
            {
                handler(presentationEvent);
            }
        }

        private static StableId CreateEventId(
            StableId hazardId,
            StableId targetId,
            long attemptGeneration,
            long contactOrdinal)
        {
            if (hazardId == null)
            {
                throw new ArgumentNullException(nameof(hazardId));
            }

            if (targetId == null)
            {
                throw new ArgumentNullException(nameof(targetId));
            }

            string source = hazardId
                + "|"
                + targetId
                + "|"
                + attemptGeneration
                + "|"
                + contactOrdinal;
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;
                for (int index = 0; index < source.Length; index++)
                {
                    char value = source[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }

                return StableId.Create("void-event", hash.ToString("x16"));
            }
        }

        private string BuildDiagnosticLocation()
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }

        private void OnDrawGizmos()
        {
            Collider2D collider = hazardCollider;
            if (collider == null)
            {
                collider = GetComponent<Collider2D>();
            }

            Color previous = Gizmos.color;
            Gizmos.color = editorRegionColor;
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one);
            }

            Gizmos.color = previous;
        }
    }
}
