using System;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    public enum StrongboxOpenMode
    {
        Cinematic = 1,
        Fast = 2,
        RevealOnly = 3,
    }

    [Serializable]
    public sealed class StrongboxRollSettings
    {
        [Header("Cinematic phase durations")]
        [Min(0f)] public float Calm = 0.50f;
        [Min(0f)] public float Acceleration = 1.50f;
        [Min(0f)] public float FullRoll = 2.05f;
        [Min(0f)] public float Slowdown = 6.60f;
        [Min(0f)] public float Lock = 0.50f;
        [Min(0f)] public float WinnerScale = 1.15f;
        [Min(0f)] public float RarityHold = 0.20f;
        [Min(0f)] public float Reveal = 0.40f;
        [Min(0f)] public float Finish = 0.70f;

        [Header("Reel")]
        [Min(1)] public int EntryCount = 96;
        [Min(0)] public int WinnerIndex = 88;
        [Min(1f)] public float CardHeight = 154f;
        [Min(0f)] public float CardGap = 14f;
        [Min(0f)] public float StartIndex = 2f;
        [Min(0f)] public float AccelerationEndIndex = 22f;
        [Min(0f)] public float FullRollEndIndex = 61f;
        [Min(0f)] public float EdgePaddingPixels = 6f;

        [Header("Winner")]
        [Min(1f)] public float WinnerFinalScale = 1.26f;
        [Min(1f)] public float WinnerOvershootScale = 1.32f;

        public float CardStep
        {
            get { return CardHeight + CardGap; }
        }

        public float Duration(float value, StrongboxOpenMode mode)
        {
            return mode == StrongboxOpenMode.Fast
                ? value * 0.5f
                : value;
        }

        public void Validate()
        {
            if (EntryCount < 1)
            {
                throw new InvalidOperationException("The roll needs at least one entry.");
            }
            if (WinnerIndex < 0 || WinnerIndex >= EntryCount)
            {
                throw new InvalidOperationException("WinnerIndex must address an entry in the roll.");
            }
            if (CardHeight <= 0f || CardStep <= 0f)
            {
                throw new InvalidOperationException("Card dimensions must be positive.");
            }
            if (FullRollEndIndex >= WinnerIndex)
            {
                throw new InvalidOperationException("Full roll must finish before the winning entry.");
            }
        }
    }
}
