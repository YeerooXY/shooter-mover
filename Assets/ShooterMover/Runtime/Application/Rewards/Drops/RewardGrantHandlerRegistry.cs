using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    public interface IRewardGrantHandler
    {
        StableId HandlerStableId { get; }
        RewardGrantKind Kind { get; }
        void Validate(RewardGrant grant);
    }

    /// <summary>Stable dispatch boundary for reward kinds; content code never switches on enemy or prop type.</summary>
    public sealed class RewardGrantHandlerRegistry
    {
        private readonly ReadOnlyDictionary<RewardGrantKind, IRewardGrantHandler> handlers;
        public RewardGrantHandlerRegistry(IEnumerable<IRewardGrantHandler> handlers)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            var map = new Dictionary<RewardGrantKind, IRewardGrantHandler>(); var ids = new HashSet<StableId>();
            foreach (IRewardGrantHandler handler in handlers)
            {
                if (handler == null || handler.HandlerStableId == null || !ids.Add(handler.HandlerStableId) || map.ContainsKey(handler.Kind)) throw new ArgumentException("Reward grant handlers must be non-null and unique by ID and kind.", nameof(handlers));
                map.Add(handler.Kind, handler);
            }
            this.handlers = new ReadOnlyDictionary<RewardGrantKind, IRewardGrantHandler>(map);
        }
        public IRewardGrantHandler Require(RewardGrant grant)
        {
            if (grant == null) throw new ArgumentNullException(nameof(grant)); IRewardGrantHandler handler;
            if (!handlers.TryGetValue(grant.Kind, out handler)) throw new InvalidOperationException("No reward grant handler is registered for " + grant.Kind + ".");
            handler.Validate(grant); return handler;
        }
    }
}
