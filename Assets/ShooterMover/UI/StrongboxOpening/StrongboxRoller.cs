using System;
using System.Collections;
using System.Collections.Generic;
using ShooterMover.UI.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    [DisallowMultipleComponent]
    public sealed class StrongboxRoller : MonoBehaviour
    {
        [SerializeField] private RectTransform cardsRoot;
        [SerializeField] private WeaponCard cardPrefab;
        [SerializeField] private StrongboxRollSettings settings =
            new StrongboxRollSettings();

        private readonly List<WeaponCard> cards = new List<WeaponCard>();
        private Coroutine playback;
        private StrongboxRollPlan current;

        public event Action<string> PhaseChanged;

        public StrongboxRollSettings Settings { get { return settings; } }
        public StrongboxRollPlan Current { get { return current; } }
        public bool IsPlaying { get { return playback != null; } }

        public void Prepare(StrongboxRollPlan plan)
        {
            current = plan ?? throw new ArgumentNullException(nameof(plan));
            settings.Validate();
            EnsureCards(plan.Entries.Count);

            for (int index = 0; index < cards.Count; index++)
            {
                bool visible = index < plan.Entries.Count;
                WeaponCard card = cards[index];
                card.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                card.Show(plan.Entries[index], WeaponCardDisplay.Roll);
                RectTransform rect = (RectTransform)card.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, settings.CardHeight);
                rect.anchoredPosition = new Vector2(0f, -index * settings.CardStep);
                rect.localScale = Vector3.one;

                CanvasGroup group = GetOrAddCanvasGroup(card.gameObject);
                group.alpha = 1f;
            }

            SetRollIndex(settings.StartIndex);
            gameObject.SetActive(true);
        }

        public void Play(
            StrongboxOpenMode mode,
            Action locked = null,
            Action revealed = null,
            Action completed = null)
        {
            if (current == null)
            {
                throw new InvalidOperationException("Prepare a Strongbox roll before playing it.");
            }

            if (playback != null)
            {
                StopCoroutine(playback);
            }
            playback = StartCoroutine(
                PlayRoutine(mode, locked, revealed, completed));
        }

        public void Stop()
        {
            if (playback != null)
            {
                StopCoroutine(playback);
                playback = null;
            }
        }

        private IEnumerator PlayRoutine(
            StrongboxOpenMode mode,
            Action locked,
            Action revealed,
            Action completed)
        {
            ResetPreparedCards();

            if (mode != StrongboxOpenMode.RevealOnly)
            {
                RaisePhase("HOLD");
                yield return Wait(settings.Duration(settings.Calm, mode));

                RaisePhase("ACCELERATING");
                yield return Move(
                    settings.StartIndex,
                    settings.AccelerationEndIndex,
                    settings.Duration(settings.Acceleration, mode),
                    EaseInCubic);

                RaisePhase("ROLLING");
                yield return Move(
                    settings.AccelerationEndIndex,
                    settings.FullRollEndIndex,
                    settings.Duration(settings.FullRoll, mode),
                    Linear);

                RaisePhase("SLOWING");
                yield return Move(
                    settings.FullRollEndIndex,
                    current.TensionStopIndex,
                    settings.Duration(settings.Slowdown, mode),
                    EaseOutCubic);

                RaisePhase("LOCKING");
                yield return Move(
                    current.TensionStopIndex,
                    current.WinnerIndex,
                    settings.Duration(settings.Lock, mode),
                    EaseLock);
            }
            else
            {
                SetRollIndex(current.WinnerIndex);
            }

            SetRollIndex(current.WinnerIndex);
            if (locked != null)
            {
                locked();
            }

            RaisePhase("RARITY LOCKED");
            yield return ScaleWinner(
                settings.Duration(settings.WinnerScale, mode));

            RaisePhase("REVEAL");
            yield return Wait(settings.Duration(settings.RarityHold, mode));

            WeaponCard winner = cards[current.WinnerIndex];
            winner.SetDisplay(WeaponCardDisplay.Reveal);
            if (revealed != null)
            {
                revealed();
            }
            yield return Wait(settings.Duration(settings.Reveal, mode));

            RaisePhase("COMPLETE");
            yield return Wait(settings.Duration(settings.Finish, mode));

            playback = null;
            if (completed != null)
            {
                completed();
            }
        }

        private void ResetPreparedCards()
        {
            for (int index = 0; index < current.Entries.Count; index++)
            {
                WeaponCard card = cards[index];
                card.Show(current.Entries[index], WeaponCardDisplay.Roll);
                card.transform.localScale = Vector3.one;
                GetOrAddCanvasGroup(card.gameObject).alpha = 1f;
            }
            SetRollIndex(settings.StartIndex);
        }

        private IEnumerator Move(
            float start,
            float end,
            float duration,
            Func<float, float> easing)
        {
            if (duration <= 0f)
            {
                SetRollIndex(end);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                SetRollIndex(Mathf.LerpUnclamped(
                    start,
                    end,
                    easing(normalized)));
                yield return null;
            }
            SetRollIndex(end);
        }

        private IEnumerator ScaleWinner(float duration)
        {
            WeaponCard winner = cards[current.WinnerIndex];
            RectTransform winnerRect = (RectTransform)winner.transform;

            if (duration <= 0f)
            {
                winnerRect.localScale = Vector3.one * settings.WinnerFinalScale;
                FadeOtherCards(0f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (normalized < 0.78f)
                {
                    float first = normalized / 0.78f;
                    scale = Mathf.LerpUnclamped(
                        1f,
                        settings.WinnerOvershootScale,
                        EaseOutBack(first));
                }
                else
                {
                    float settle = (normalized - 0.78f) / 0.22f;
                    scale = Mathf.Lerp(
                        settings.WinnerOvershootScale,
                        settings.WinnerFinalScale,
                        EaseInOutSine(settle));
                }

                winnerRect.localScale = Vector3.one * scale;
                FadeOtherCards(1f - normalized);
                yield return null;
            }

            winnerRect.localScale = Vector3.one * settings.WinnerFinalScale;
            FadeOtherCards(0f);
        }

        private IEnumerator Wait(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void FadeOtherCards(float alpha)
        {
            for (int index = 0; index < current.Entries.Count; index++)
            {
                if (index == current.WinnerIndex)
                {
                    continue;
                }
                GetOrAddCanvasGroup(cards[index].gameObject).alpha = alpha;
            }
        }

        private void SetRollIndex(float index)
        {
            if (cardsRoot == null)
            {
                return;
            }

            Vector2 position = cardsRoot.anchoredPosition;
            position.y = index * settings.CardStep;
            cardsRoot.anchoredPosition = position;
        }

        private void EnsureCards(int count)
        {
            if (cardsRoot == null || cardPrefab == null)
            {
                throw new InvalidOperationException(
                    "StrongboxRoller requires a cards root and WeaponCard prefab.");
            }

            while (cards.Count < count)
            {
                WeaponCard card = Instantiate(cardPrefab, cardsRoot);
                card.name = "RollCard_" + (cards.Count + 1);
                cards.Add(card);
            }
        }

        private void RaisePhase(string value)
        {
            Action<string> handler = PhaseChanged;
            if (handler != null)
            {
                handler(value);
            }
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private static float Linear(float value) { return value; }
        private static float EaseInCubic(float value) { return value * value * value; }
        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }
        private static float EaseInOutSine(float value)
        {
            return -(Mathf.Cos(Mathf.PI * value) - 1f) * 0.5f;
        }
        private static float EaseOutBack(float value)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float shifted = value - 1f;
            return 1f + c3 * shifted * shifted * shifted
                + c1 * shifted * shifted;
        }
        private static float EaseLock(float value)
        {
            if (value < 0.70f)
            {
                return EaseOutCubic(value / 0.70f) * 1.025f;
            }

            return Mathf.Lerp(
                1.025f,
                1f,
                EaseInOutSine((value - 0.70f) / 0.30f));
        }
    }
}
