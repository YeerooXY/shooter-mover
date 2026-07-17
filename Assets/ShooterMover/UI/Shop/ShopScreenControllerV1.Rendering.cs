using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.Shop
{
    public sealed partial class ShopScreenControllerV1 
    {
        private void DrawBackplate()
        {
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (shopTemplate != null)
            {
                GUI.DrawTexture(full, shopTemplate, ScaleMode.ScaleAndCrop, true);
            }
            else
            {
                GUI.Box(full, GUIContent.none);
            }

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.38f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(
                "MONEY  " + projection.MoneyBalance.ToString("N0", CultureInfo.InvariantCulture),
                balanceStyle,
                GUILayout.MinWidth(220f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "RUN " + ShortId(projection.RunStableId)
                + "   STOCK REV " + projection.RefreshOrdinal.ToString(CultureInfo.InvariantCulture),
                detailStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawFeedback()
        {
            if (projection == null || projection.FeedbackKind == ShopScreenFeedbackKindV1.None)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(projection.FeedbackText, feedbackStyle);
            if (!string.IsNullOrWhiteSpace(projection.FeedbackCode))
            {
                GUILayout.Label(projection.FeedbackCode, detailStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawCategory(string heading, string categoryLabel)
        {
            var matching = new List<ShopScreenStockCardV1>();
            for (int index = 0; index < projection.Stock.Count; index++)
            {
                ShopScreenStockCardV1 card = projection.Stock[index];
                bool match = categoryLabel == null
                    ? card.CategoryLabel != "WEAPON" && card.CategoryLabel != "ARMOR"
                    : string.Equals(card.CategoryLabel, categoryLabel, StringComparison.Ordinal);
                if (match)
                {
                    matching.Add(card);
                }
            }

            if (matching.Count == 0)
            {
                return;
            }

            GUILayout.Label(heading, sectionStyle);
            int columns = Screen.width >= 1200 ? 3 : Screen.width >= 760 ? 2 : 1;
            for (int index = 0; index < matching.Count; index += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int cardIndex = index + column;
                    if (cardIndex < matching.Count)
                    {
                        DrawCard(matching[cardIndex]);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }
        }

        private void DrawCard(ShopScreenStockCardV1 card)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinWidth(260f), GUILayout.ExpandWidth(true));
            GUILayout.Label(card.DisplayName, cardTitleStyle);
            GUILayout.Label(
                card.CategoryLabel
                + "  ·  " + card.QualityLabel
                + "  ·- LEVEL " + card.ItemLevel.ToString(CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label(
                "Augments: " + card.AugmentCount.ToString(CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label(
                "Definition: " + card.DefinitionStableId,
                detailStyle);
            GUILayout.Label(
                "Instance: " + card.EquipmentInstanceStableId,
                detailStyle);
            GUILayout.Label(
                "PRICE  " + card.Price.ToString("N0", CultureInfo.InvariantCulture),
                balanceStyle);

            if (card.IsSold)
            {
                GUILayout.Label("SOLD", feedbackStyle);
            }
            else if (card.CanRetry)
            {
                if (GUILayout.Button("RETRY PENDING PURCHASE", GUILayout.MinHeight(42f)))
               {
                    Retry(card.StockEntryStableId);
                }
            }
            else
            {
                GUI.enabled = card.CanPurchase;
                if (GUILayout.Button("BUY", GUILayout.MinHeight(42f)))
                {
                    Purchase(card.StockEntryStableId);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            balanceStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static string ShortId(StableId value)
        {
            string text = value == null ? string.Empty : value.ToString();
            return text.Length <= 24 ? text : text.Substring(0, 24);
        }
    }
}
