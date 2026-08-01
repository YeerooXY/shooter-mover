using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterMover.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class ItemCard : MonoBehaviour
    {
        [SerializeField] private Image art;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text quantityText;
        [SerializeField] private AugmentList augments;

        public void Show(
            Sprite artSprite,
            string title,
            string subtitle,
            string detail,
            string quantity,
            IReadOnlyList<AugmentLine> augmentLines)
        {
            if (art != null)
            {
                art.sprite = artSprite;
                art.enabled = artSprite != null;
                art.preserveAspect = true;
            }

            SetText(titleText, title);
            SetText(subtitleText, subtitle);
            SetText(detailText, detail);
            SetText(quantityText, quantity);

            if (augments != null)
            {
                augments.Show(augmentLines);
            }

            gameObject.SetActive(true);
        }

        public void Configure(
            Image itemArt,
            Text title,
            Text subtitle,
            Text detail,
            Text quantity,
            AugmentList augmentList)
        {
            art = itemArt;
            titleText = title;
            subtitleText = subtitle;
            detailText = detail;
            quantityText = quantity;
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
