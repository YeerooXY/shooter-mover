using System;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    /// <summary>
    /// Transparent presentation decorator installed by the generic enemy registration path.
    /// Authoritative transitions remain owned by the wrapped enemy authority; this wrapper only
    /// forwards immutable accepted step results to presentation.
    /// </summary>
    public sealed class CombatPresentationEnemyActorAuthority2D : IEnemyActor2DAuthority
    {
        private readonly IEnemyActor2DAuthority inner;
        private readonly CombatEnemyPresentationRegistration2D presentation;

        public CombatPresentationEnemyActorAuthority2D(
            IEnemyActor2DAuthority inner,
            CombatEnemyPresentationRegistration2D presentation)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.presentation = presentation
                ?? throw new ArgumentNullException(nameof(presentation));
        }

        public IEnemyActor2DAuthority Inner { get { return inner; } }
        public CombatEnemyPresentationRegistration2D Presentation
        {
            get { return presentation; }
        }

        public bool TryReadState(out EnemyActorState state)
        {
            return inner.TryReadState(out state);
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            EnemyActorStepResult result = inner.Apply(command);
            if (result != null)
            {
                presentation.Observe(result);
            }
            return result;
        }

        public bool Reset()
        {
            bool reset = inner.Reset();
            if (reset)
            {
                presentation.SynchronizeLifecycle();
                presentation.Refresh();
            }
            return reset;
        }
    }
}
