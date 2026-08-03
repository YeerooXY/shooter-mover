using System;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    [DisallowMultipleComponent]
    public sealed class Teleporter : MonoBehaviour
    {
        private LineRenderer outer;
        private LineRenderer inner;
        private Material material;
        private bool isOpen;

        public void Bind(Vector2 position, float rotation, bool open)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            if (outer == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                material.hideFlags = HideFlags.HideAndDontSave;
                outer = AddRing("Outer", 0.72f, 0.11f, 40);
                inner = AddRing("Inner", 0.38f, 0.07f, 28);
            }
            SetOpen(open);
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
            Color color = open
                ? new Color(0.68f, 0.34f, 1f, 1f)
                : new Color(0.3f, 0.34f, 0.42f, 1f);
            if (outer != null)
            {
                outer.startColor = color;
                outer.endColor = color;
            }
            if (inner != null)
            {
                Color innerColor = new Color(
                    color.r,
                    color.g,
                    color.b,
                    open ? 0.82f : 0.58f);
                inner.startColor = innerColor;
                inner.endColor = innerColor;
            }
        }

        private void Update()
        {
            if (!isOpen || inner == null) return;
            float pulse = 0.92f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.08f;
            inner.transform.localScale = new Vector3(pulse, pulse, 1f);
            inner.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Time.unscaledTime * 24f);
        }

        private LineRenderer AddRing(
            string objectName,
            float radius,
            float width,
            int segments)
        {
            var ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.sortingOrder = 60;
            for (int index = 0; index < segments; index++)
            {
                float angle = Mathf.PI * 2f * index / segments;
                ring.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
            return ring;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }
    }
}
