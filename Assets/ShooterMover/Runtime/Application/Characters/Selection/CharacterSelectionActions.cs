using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Selection;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Characters.Selection
{
    public enum CharacterSelectionOperationStatus
    {
        Highlighted = 1,
        NoChange = 2,
        Rejected = 3,
    }

    public enum CharacterSelectionRouteStatus
    {
        Confirmed = 1,
        Back = 2,
    }

    public sealed class CharacterSelectionOperationResult
    {
        internal CharacterSelectionOperationResult(
            CharacterSelectionOperationStatus status,
            string rejectionCode,
            CharacterSelectionSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public CharacterSelectionOperationStatus Status { get; }

        public string RejectionCode { get; }

        public CharacterSelectionSnapshot Snapshot { get; }

        public bool Changed
        {
            get { return Status == CharacterSelectionOperationStatus.Highlighted; }
        }
    }

    public sealed class CharacterSelectionSnapshot
    {
        internal CharacterSelectionSnapshot(
            StableId highlightedCharacterStableId,
            StableId highlightedLoadoutProfileStableId,
            string catalogFingerprint,
            string incomingPayloadFingerprint,
            string selectionFingerprint)
        {
            HighlightedCharacterStableId = highlightedCharacterStableId
                ?? throw new ArgumentNullException(nameof(highlightedCharacterStableId));
            HighlightedLoadoutProfileStableId = highlightedLoadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(highlightedLoadoutProfileStableId));
            CatalogFingerprint = catalogFingerprint
                ?? throw new ArgumentNullException(nameof(catalogFingerprint));
            IncomingPayloadFingerprint = incomingPayloadFingerprint
                ?? throw new ArgumentNullException(nameof(incomingPayloadFingerprint));
            SelectionFingerprint = selectionFingerprint
                ?? throw new ArgumentNullException(nameof(selectionFingerprint));
        }

        public StableId HighlightedCharacterStableId { get; }

        public StableId HighlightedLoadoutProfileStableId { get; }

        public string CatalogFingerprint { get; }

        public string IncomingPayloadFingerprint { get; }

        public string SelectionFingerprint { get; }
    }

    public sealed class CharacterSelectionRouteResult
    {
        internal CharacterSelectionRouteResult(
            CharacterSelectionRouteStatus status,
            HubRoute targetRoute,
            PlayerRouteProfilePayload payload,
            string selectionFingerprint)
        {
            Status = status;
            TargetRoute = targetRoute;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            SelectionFingerprint = selectionFingerprint
                ?? throw new ArgumentNullException(nameof(selectionFingerprint));
        }

        public CharacterSelectionRouteStatus Status { get; }

        public HubRoute TargetRoute { get; }

        public PlayerRouteProfilePayload Payload { get; }

        public string SelectionFingerprint { get; }
    }

    /// <summary>
    /// Presentation callback only. Implementations route the immutable result but may not
    /// replace or mutate its payload.
    /// </summary>
    public interface ICharacterSelectionRouteSink
    {
        void Accept(CharacterSelectionRouteResult result);
    }

    /// <summary>
    /// Pure selection coordinator. Highlighting changes only local draft state. Confirm
    /// creates one new immutable HUB payload while copying the incoming concrete equipment
    /// instance identities in their original slot order. Back returns the exact incoming
    /// payload instance.
    /// </summary>
    public sealed class CharacterSelectionActions
    {
        private readonly CharacterSelectionCatalog catalog;
        private readonly PlayerRouteProfilePayload incomingPayload;
        private readonly CharacterSelectionRouteResult backResult;

        private CharacterSelectionDefinition highlightedCharacter;
        private CharacterClassProfileDefinition highlightedProfile;
        private CharacterSelectionRouteResult confirmedResult;

        public CharacterSelectionActions(
            CharacterSelectionCatalog catalog,
            PlayerRouteProfilePayload incomingPayload)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.incomingPayload = incomingPayload
                ?? throw new ArgumentNullException(nameof(incomingPayload));
            if (!incomingPayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming HUB route payload fingerprint is inconsistent.",
                    nameof(incomingPayload));
            }

            ResolveInitialSelection();
            backResult = new CharacterSelectionRouteResult(
                CharacterSelectionRouteStatus.Back,
                HubRoute.MainMenu,
                incomingPayload,
                ExportSnapshot().SelectionFingerprint);
        }

        public CharacterSelectionCatalog Catalog
        {
            get { return catalog; }
        }

        public PlayerRouteProfilePayload IncomingPayload
        {
            get { return incomingPayload; }
        }

        public StableId HighlightedCharacterStableId
        {
            get { return highlightedCharacter.CharacterStableId; }
        }

        public StableId HighlightedLoadoutProfileStableId
        {
            get { return highlightedProfile.LoadoutProfileStableId; }
        }

        public CharacterSelectionSnapshot ExportSnapshot()
        {
            string fingerprint = BuildSelectionFingerprint(
                catalog.Fingerprint,
                incomingPayload.Fingerprint,
                highlightedCharacter.CharacterStableId,
                highlightedProfile.LoadoutProfileStableId);
            return new CharacterSelectionSnapshot(
                highlightedCharacter.CharacterStableId,
                highlightedProfile.LoadoutProfileStableId,
                catalog.Fingerprint,
                incomingPayload.Fingerprint,
                fingerprint);
        }

        public CharacterSelectionOperationResult TryHighlightCharacter(
            StableId characterStableId)
        {
            if (confirmedResult != null)
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-already-confirmed");
            }

            CharacterSelectionDefinition candidate;
            if (!catalog.TryGetCharacter(characterStableId, out candidate))
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-character-unknown");
            }

            if (candidate.CharacterStableId == highlightedCharacter.CharacterStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatus.NoChange,
                    "character-selection-character-already-highlighted");
            }

            CharacterClassProfileDefinition defaultProfile;
            if (!catalog.TryGetProfile(
                candidate.DefaultLoadoutProfileStableId,
                out defaultProfile))
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-character-default-profile-unavailable");
            }

            highlightedCharacter = candidate;
            highlightedProfile = defaultProfile;
            return Operation(
                CharacterSelectionOperationStatus.Highlighted,
                string.Empty);
        }

        public CharacterSelectionOperationResult TryHighlightProfile(
            StableId loadoutProfileStableId)
        {
            if (confirmedResult != null)
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-already-confirmed");
            }

            CharacterClassProfileDefinition candidate;
            if (!catalog.TryGetProfile(loadoutProfileStableId, out candidate))
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-profile-unknown");
            }

            if (candidate.CharacterStableId != highlightedCharacter.CharacterStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatus.Rejected,
                    "character-selection-profile-character-mismatch");
            }

            if (candidate.LoadoutProfileStableId
                == highlightedProfile.LoadoutProfileStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatus.NoChange,
                    "character-selection-profile-already-highlighted");
            }

            highlightedProfile = candidate;
            return Operation(
                CharacterSelectionOperationStatus.Highlighted,
                string.Empty);
        }

        public CharacterSelectionRouteResult Confirm()
        {
            if (confirmedResult != null)
            {
                return confirmedResult;
            }

            var equipmentInstanceIds = new List<StableId>(
                incomingPayload.WeaponSlots.Count);
            for (int index = 0; index < incomingPayload.WeaponSlots.Count; index++)
            {
                equipmentInstanceIds.Add(
                    incomingPayload.WeaponSlots[index].EquipmentInstanceStableId);
            }

            PlayerRouteProfilePayload selectedPayload =
                PlayerRouteProfilePayload.Create(
                    highlightedCharacter.CharacterStableId,
                    highlightedProfile.LoadoutProfileStableId,
                    equipmentInstanceIds);
            confirmedResult = new CharacterSelectionRouteResult(
                CharacterSelectionRouteStatus.Confirmed,
                HubRoute.InventoryLoadoutHub,
                selectedPayload,
                ExportSnapshot().SelectionFingerprint);
            return confirmedResult;
        }

        public CharacterSelectionRouteResult Back()
        {
            return backResult;
        }

        private void ResolveInitialSelection()
        {
            CharacterSelectionDefinition incomingCharacter;
            CharacterClassProfileDefinition incomingProfile;
            if (catalog.TryGetCharacter(
                    incomingPayload.SelectedCharacterStableId,
                    out incomingCharacter)
                && catalog.TryGetProfile(
                    incomingPayload.LoadoutProfileStableId,
                    out incomingProfile)
                && incomingProfile.CharacterStableId
                    == incomingCharacter.CharacterStableId)
            {
                highlightedCharacter = incomingCharacter;
                highlightedProfile = incomingProfile;
                return;
            }

            if (catalog.TryGetCharacter(
                incomingPayload.SelectedCharacterStableId,
                out incomingCharacter))
            {
                highlightedCharacter = incomingCharacter;
                if (!catalog.TryGetProfile(
                    incomingCharacter.DefaultLoadoutProfileStableId,
                    out highlightedProfile))
                {
                    throw new InvalidOperationException(
                        "The validated catalog lost a character default profile.");
                }

                return;
            }

            highlightedCharacter = catalog.DefaultCharacter;
            if (!catalog.TryGetProfile(
                highlightedCharacter.DefaultLoadoutProfileStableId,
                out highlightedProfile))
            {
                throw new InvalidOperationException(
                    "The validated catalog lost its default character profile.");
            }
        }

        private CharacterSelectionOperationResult Operation(
            CharacterSelectionOperationStatus status,
            string rejectionCode)
        {
            return new CharacterSelectionOperationResult(
                status,
                rejectionCode,
                ExportSnapshot());
        }

        private static string BuildSelectionFingerprint(
            string catalogFingerprint,
            string incomingPayloadFingerprint,
            StableId characterStableId,
            StableId loadoutProfileStableId)
        {
            var builder = new StringBuilder();
            Append(builder, "catalog", catalogFingerprint);
            Append(builder, "incoming", incomingPayloadFingerprint);
            Append(builder, "character", characterStableId.ToString());
            Append(builder, "profile", loadoutProfileStableId.ToString());

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var result = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                result.Append(
                    digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }
    }
}
