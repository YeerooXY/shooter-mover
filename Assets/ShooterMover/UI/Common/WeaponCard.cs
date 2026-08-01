using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterMover.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class WeaponCard : MonoBehaviour
    {
        [Header("Style")]
        [SerializeField] private WeaponCardTheme theme;
        [SerializeField] private Image background;
        [SerializeField] private Image glow;

        [Header("Content")]
        [SerializeField] private Image weaponArt;
        [SerializeField] private Text nameText;
        [SerializeField] private Text rarityText;
        [SerializeField] private Text levelText;
        [SerializeField] private GameObject detailsRoot;
        [SerializeField] private AugmentList augments;

        private WeaponCardView current;

        public WeaponCardView Current { get { return current; } }

        public void Show(
            WeaponCardView view,
            WeaponCardDisplay display)
        {
            current = view ?? throw new ArgumentNullException(nameof(view));

            Sprite backgroundSprite;
            Color backgroundColor;
            Color glowColor;
            if (theme != null)
            {
                theme.TryGet(
                    view.Rarity,
                    out backgroundSprite,
                    out backgroundColor,
                    out glowColor);
            }
            else
            {
                backgroundSprite = null;
                WeaponCardPalette.GetFallback(
                    view.Rarity,
                    out backgroundColor,
                    out glowColor);
            }

            if (background != null)
            {
                if (backgroundSprite != null)
                {
                    background.sprite = backgroundSprite;
                }
                background.color = backgroundColor;
            }
            if (glow != null)
            {
                glow.color = glowColor;
            }
            if (weaponArt != null)
            {
                weaponArt.sprite = view.Art;
                weaponArt.enabled = view.Art != null;
                weaponArt.preserveAspect = true;
            }

            SetText(nameText, view.Name);
            SetText(rarityText, WeaponRarityText.Label(view.Rarity));
            SetText(
                levelText,
                view.ItemLevel >= 0 ? "LEVEL " + view.ItemLevel : string.Empty);

            bool reveal = display == WeaponCardDisplay.Reveal;
            if (detailsRoot != null)
            {
                detailsRoot.SetActive(reveal);
            }
            if (augments != null)
            {
                if (reveal)
                {
                    augments.Show(view.Augments);
                }
                else
                {
                    augments.Clear();
                }
            }

            gameObject.SetActive(true);
        }

        public void SetDisplay(WeaponCardDisplay display)
        {
            if (current != null)
            {
                Show(current, display);
            }
        }

        public void Configure(
            WeaponCardTheme cardTheme,
            Image cardBackground,
            Image cardGlow,
            Image art,
            Text weaponName,
            Text rarity,
            Text level,
            GameObject details,
            AugmentList augmentList)
        {
            theme = cardTheme;
            background = cardBackground;
            glow = cardGlow;
            weaponArt = art;
            nameText = weaponName;
            rarityText = rarity;
            levelText = level;
            detailsRoot = details;
            augments = augmentList;
        }

        private static void SetText(Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            target.text = value ?? string.Empty;
            target.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
    }
}
