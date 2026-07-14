using System;

namespace ShooterMover.Contracts.Input
{
    /// <summary>
    /// Immutable two-axis intent constrained to the unit circle.
    /// </summary>
    public readonly struct NormalizedIntentVector2 : IEquatable<NormalizedIntentVector2>
    {
        private NormalizedIntentVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public float MagnitudeSquared
        {
            get { return (X * X) + (Y * Y); }
        }

        public static NormalizedIntentVector2 Zero
        {
            get { return new NormalizedIntentVector2(0f, 0f); }
        }

        /// <summary>
        /// Creates an intent vector. Finite values outside the unit circle are
        /// normalized while values inside it preserve their analogue magnitude.
        /// </summary>
        public static NormalizedIntentVector2 Create(float x, float y)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Intent vector components must be finite.");
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Intent vector components must be finite.");
            }

            double magnitudeSquared = ((double)x * x) + ((double)y * y);
            if (magnitudeSquared <= 1d)
            {
                return new NormalizedIntentVector2(x, y);
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            return new NormalizedIntentVector2(
                (float)(x * inverseMagnitude),
                (float)(y * inverseMagnitude));
        }

        public bool Equals(NormalizedIntentVector2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is NormalizedIntentVector2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(
            NormalizedIntentVector2 left,
            NormalizedIntentVector2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            NormalizedIntentVector2 left,
            NormalizedIntentVector2 right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Immutable final held state plus edge transitions observed in one sample.
    /// </summary>
    public readonly struct ButtonIntent : IEquatable<ButtonIntent>
    {
        public ButtonIntent(bool isHeld, bool wasPressed, bool wasReleased)
        {
            if (wasPressed && !wasReleased && !isHeld)
            {
                throw new ArgumentException(
                    "A press without a release must finish held.",
                    nameof(isHeld));
            }

            if (wasReleased && !wasPressed && isHeld)
            {
                throw new ArgumentException(
                    "A release without a press must finish released.",
                    nameof(isHeld));
            }

            IsHeld = isHeld;
            WasPressed = wasPressed;
            WasReleased = wasReleased;
        }

        public bool IsHeld { get; }

        public bool WasPressed { get; }

        public bool WasReleased { get; }

        public static ButtonIntent Inactive
        {
            get { return new ButtonIntent(false, false, false); }
        }

        public static ButtonIntent Held
        {
            get { return new ButtonIntent(true, false, false); }
        }

        public static ButtonIntent Pressed
        {
            get { return new ButtonIntent(true, true, false); }
        }

        public static ButtonIntent Released
        {
            get { return new ButtonIntent(false, false, true); }
        }

        public static ButtonIntent Tap
        {
            get { return new ButtonIntent(false, true, true); }
        }

        public static ButtonIntent ReleaseThenPress
        {
            get { return new ButtonIntent(true, true, true); }
        }

        public bool Equals(ButtonIntent other)
        {
            return IsHeld == other.IsHeld
                && WasPressed == other.WasPressed
                && WasReleased == other.WasReleased;
        }

        public override bool Equals(object obj)
        {
            return obj is ButtonIntent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsHeld ? 1 : 0;
                hash = (hash * 397) ^ (WasPressed ? 1 : 0);
                hash = (hash * 397) ^ (WasReleased ? 1 : 0);
                return hash;
            }
        }

        public static bool operator ==(ButtonIntent left, ButtonIntent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ButtonIntent left, ButtonIntent right)
        {
            return !left.Equals(right);
        }

        internal ButtonIntent ReleaseForFocusLoss()
        {
            return IsHeld ? Released : Inactive;
        }
    }

    /// <summary>
    /// One immutable, engine-independent sample of player intent.
    /// </summary>
    public readonly struct PlayerIntentFrame
    {
        public PlayerIntentFrame(
            NormalizedIntentVector2 move,
            NormalizedIntentVector2 aim,
            ButtonIntent fire,
            ButtonIntent powerModifier,
            ButtonIntent thruster,
            ButtonIntent interact,
            ButtonIntent map,
            ButtonIntent pauseMenu,
            NormalizedIntentVector2 uiNavigation)
            : this(
                move,
                aim,
                fire,
                powerModifier,
                thruster,
                interact,
                map,
                pauseMenu,
                uiNavigation,
                false)
        {
        }

        private PlayerIntentFrame(
            NormalizedIntentVector2 move,
            NormalizedIntentVector2 aim,
            ButtonIntent fire,
            ButtonIntent powerModifier,
            ButtonIntent thruster,
            ButtonIntent interact,
            ButtonIntent map,
            ButtonIntent pauseMenu,
            NormalizedIntentVector2 uiNavigation,
            bool wasFocusLost)
        {
            Move = move;
            Aim = aim;
            Fire = fire;
            PowerModifier = powerModifier;
            Thruster = thruster;
            Interact = interact;
            Map = map;
            PauseMenu = pauseMenu;
            UiNavigation = uiNavigation;
            WasFocusLost = wasFocusLost;
        }

        public NormalizedIntentVector2 Move { get; }

        public NormalizedIntentVector2 Aim { get; }

        public ButtonIntent Fire { get; }

        public ButtonIntent PowerModifier { get; }

        public ButtonIntent Thruster { get; }

        public ButtonIntent Interact { get; }

        public ButtonIntent Map { get; }

        public ButtonIntent PauseMenu { get; }

        public NormalizedIntentVector2 UiNavigation { get; }

        public bool WasFocusLost { get; }

        public static PlayerIntentFrame Neutral
        {
            get
            {
                return new PlayerIntentFrame(
                    NormalizedIntentVector2.Zero,
                    NormalizedIntentVector2.Zero,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    NormalizedIntentVector2.Zero);
            }
        }

        /// <summary>
        /// Produces the safe boundary sample for focus loss: all axes are neutral,
        /// held actions receive one release edge, and prior transient edges are discarded.
        /// </summary>
        public static PlayerIntentFrame FromFocusLoss(PlayerIntentFrame previous)
        {
            return new PlayerIntentFrame(
                NormalizedIntentVector2.Zero,
                NormalizedIntentVector2.Zero,
                previous.Fire.ReleaseForFocusLoss(),
                previous.PowerModifier.ReleaseForFocusLoss(),
                previous.Thruster.ReleaseForFocusLoss(),
                previous.Interact.ReleaseForFocusLoss(),
                previous.Map.ReleaseForFocusLoss(),
                previous.PauseMenu.ReleaseForFocusLoss(),
                NormalizedIntentVector2.Zero,
                true);
        }
    }
}
