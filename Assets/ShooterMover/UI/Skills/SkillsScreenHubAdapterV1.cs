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
    public interface ISkillsScreenPresenterV1
    {
        void Show(
            SkillsScreenSessionV1 session,
            ISkillsScreenNavigationPortV1 navigationPort);

        void Hide();
    }

    /// <summary>
    /// HUB-owned navigation is invoked through this port. The exact immutable incoming
    /// route payload is passed back; the screen never reconstructs route state.
    /// </summary>
    public interface ISkillsScreenNavigationPortV1
    {
        void ReturnToHub(PlayerRouteProfilePayloadV1 routePayload);
    }

    public sealed class DelegateSkillsScreenNavigationPortV1 :
        ISkillsScreenNavigationPortV1
    {
        private readonly Action<PlayerRouteProfilePayloadV1> returnToHub;

        public DelegateSkillsScreenNavigationPortV1(
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
        }

        public void ReturnToHub(PlayerRouteProfilePayloadV1 routePayload)
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
    public sealed class SkillsHubDestinationAdapterV1 :
        IHubRouteDestinationAdapterV1
    {
        private readonly IPlayerExperienceAuthorityV1 experienceAuthority;
        private readonly SkillProgressionAuthorityV1 skillAuthority;
        private readonly ISkillsScreenPresenterV1 presenter;
        private readonly ISkillsScreenNavigationPortV1 navigationPort;

        public SkillsHubDestinationAdapterV1(
            IPlayerExperienceAuthorityV1 experienceAuthority,
            SkillProgressionAuthorityV1 skillAuthority,
            ISkillsScreenPresenterV1 presenter,
            ISkillsScreenNavigationPortV1 navigationPort)
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
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (route == HubRouteV1.Skills)
            {
                presenter.Show(
                    new SkillsScreenSessionV1(
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
