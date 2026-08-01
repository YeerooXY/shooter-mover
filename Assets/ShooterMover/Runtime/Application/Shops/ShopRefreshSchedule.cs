using System;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed class ShopRefreshWindow
    {
        public ShopRefreshWindow(
            long ordinal,
            DateTime startsAtUtc,
            DateTime refreshesAtUtc)
        {
            if (ordinal < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            if (startsAtUtc.Kind != DateTimeKind.Utc
                || refreshesAtUtc.Kind != DateTimeKind.Utc
                || refreshesAtUtc <= startsAtUtc)
            {
                throw new ArgumentException(
                    "Shop refresh windows require ordered UTC boundaries.");
            }
            Ordinal = ordinal;
            StartsAtUtc = startsAtUtc;
            RefreshesAtUtc = refreshesAtUtc;
        }

        public long Ordinal { get; }
        public DateTime StartsAtUtc { get; }
        public DateTime RefreshesAtUtc { get; }

        public StableId StockIdentity(StableId characterStableId)
        {
            return Shop.DeriveStableId(
                "shopwindow",
                characterStableId.ToString(),
                Ordinal.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static class ShopRefreshSchedule
    {
        public const int RefreshHours = 6;
        private static readonly DateTime EpochUtc =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static ShopRefreshWindow Resolve(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                utcNow = utcNow.ToUniversalTime();
            }
            long windowTicks = TimeSpan.FromHours(RefreshHours).Ticks;
            long elapsedTicks = checked(utcNow.Ticks - EpochUtc.Ticks);
            if (elapsedTicks < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(utcNow));
            }
            long ordinal = elapsedTicks / windowTicks;
            DateTime starts = EpochUtc.AddTicks(
                checked(ordinal * windowTicks));
            return new ShopRefreshWindow(
                ordinal,
                starts,
                starts.AddTicks(windowTicks));
        }
    }
}
