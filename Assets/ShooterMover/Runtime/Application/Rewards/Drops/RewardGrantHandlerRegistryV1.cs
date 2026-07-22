using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    public interface IRewardGrantHandlerV1
    {
        StableId HandlerStableId { get; }
        RewardGrantKindV1 Kind { get; }
        void Validate(RewardGrantV1 grant);
    }

    /// <summary>Stable dispatch boundary for reward kinds; content code never switches on enemy or prop type.</summary>
    public sealed class RewardGrantHandlerRegistryV1
    {
        private readonly ReadOnlyDictionary<RewardGrantKindV1, IRewardGrantHandlerV1> handlers;
        public RewardGrantHandlerRegistryV1(IEnumerable<IRewardGrantHandlerV1> handlers)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            var map = new Dictionary<RewardGrantKindV1, IRewardGrantHandlerV1>(); var ids = new HashSet<StableId>();
            foreach (IRewardGrantHandlerV1 handler in handlers)
            {
                if (handler == null || handler.HandlerStableId == null || !ids.Add(handler.HandlerStableId) || map.ContainsKey(handler.Kind)) throw new ArgumentException("Reward grant handlers must be non-null and unique by ID and kind.", nameof(handlers));
                map.Add(handler.Kind, handler);
            }
            this.handlers = new ReadOnlyDictionary<RewardGrantKindV1, IRewardGrantHandlerV1>(map);
        }
        public IRewardGrantHandlerV1 Require(RewardGrantV1 grant)
        {
            if (grant == null) throw new ArgumentNullException(nameof(grant)); IRewardGrantHandlerV1 handler;
            if (!handlers.TryGetValue(grant.Kind, out handler)) throw new InvalidOperationException("No reward grant handler is registered for " + grant.Kind + ".");
            handler.Validate(grant); return handler;
        }
    }
}
