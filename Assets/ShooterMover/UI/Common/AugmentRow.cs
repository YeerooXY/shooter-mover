using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterMover.UI.Common
{
    public readonly struct AugmentLine
    {
        public AugmentLine(string name, int level, Sprite icon = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "An augment display name is required.",
                    nameof(name));
            }
            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            Name = name.Trim();
            Level = level;
            Icon = icon;
        }

        public string Name { get; }
        public int Level { get; }
        public Sprite Icon { get; }
    }

    [DisallowMultipleComponent]
    public sealed class AugmentRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;

        public void Show(AugmentLine line)
        {
            if (nameText != null)
            {
                nameText.text = line.Name;
            }
            if (levelText != null)
            {
                levelText.text = "LV " + line.Level;
            }
            if (icon != null)
            {
                icon.sprite = line.Icon;
                icon.enabled = line.Icon != null;
            }

            gameObject.SetActive(true);
        }

        public void Configure(
            Image iconImage,
            Text augmentName,
            Text augmentLevel)
        {
            icon = iconImage;
            nameText = augmentName;
            levelText = augmentLevel;
        }
    }
}
