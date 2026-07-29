using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;

namespace ShooterMover.UI.Skills
{
    /// <summary>
    /// Presentation target used by the HUB destination adapter. Implementations may be
    /// scene-backed or embedded overlays; neither form owns XP or skill state.
    /// </summary>
    public interface ISkillsScreenPresenter
    {
        void Show(
            SkillsScreenSession session,
            ISkillsScreenNavigationPort navigationPort);

        void Hide();
    }

    /// <summary>
    /// HUB-owned navigation is invoked through this port. The exact immutable incoming
    /// route payload is passed back; the screen never reconstructs route state.
    /// </summary>
    public interface ISkillsScreenNavigationPort
    {
        void ReturnToHub(PlayerRouteProfilePayload routePayload);
    }

    public sealed class DelegateSkillsScreenNavigationPort :
        ISkillsScreenNavigationPort
    {
        private readonly Action<PlayerRouteProfilePayload> returnToHub;

        public DelegateSkillsScreenNavigationPort(
            Action<PlayerRouteProfilePayload> returnToHub)
        {
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
        }

        public void ReturnToHub(PlayerRouteProfilePayload routePayload)
        {
            returnToHub(
                routePayload ?? throw new ArgumentNullException(nameof(routePayload)));
        }
    }

    /// <summary>
    /// HUB-001 destination adapter for SKILLUI-001. Revisit creates a fresh presentation
    /// session over the same injected XP/SKILL authorities, so no local rank state can be
    /// lost or invented by hiding and showing the screen.
    /// </summary>
    public sealed class SkillsHubDestinationBridge :
        IHubRouteDestinationBridge
    {
        private readonly IPlayerExperience experienceAuthority;
        private readonly SkillProgressionState skillAuthority;
        private readonly ISkillsScreenPresenter presenter;
        private readonly ISkillsScreenNavigationPort navigationPort;

        public SkillsHubDestinationBridge(
            IPlayerExperience experienceAuthority,
            SkillProgressionState skillAuthority,
            ISkillsScreenPresenter presenter,
            ISkillsScreenNavigationPort navigationPort)
        {
            this.experienceAuthority = experienceAuthority
                ?? throw new ArgumentNullException(nameof(experienceAuthority));
            this.skillAuthority = skillAuthority
                ?? throw new ArgumentNullException(nameof(skillAuthority));
            this.presenter = presenter
                ?? throw new ArgumentNullException(nameof(presenter));
            this.navigationPort = navigationPort
                ?? throw new ArgumentNullException(nameof(navigationPort));
        }

        public void Present(
            HubRoute route,
            PlayerRouteProfilePayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (route == HubRoute.Skills)
            {
                presenter.Show(
                    new SkillsScreenSession(
                        payload,
                        experienceAuthority,
                        skillAuthority),
                    navigationPort);
                return;
            }

            presenter.Hide();
        }
    }
}
