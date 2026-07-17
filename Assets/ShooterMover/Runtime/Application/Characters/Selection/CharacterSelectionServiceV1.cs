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
    public enum CharacterSelectionOperationStatusV1
    {
        Highlighted = 1,
        NoChange = 2,
        Rejected = 3,
    }

    public enum CharacterSelectionRouteStatusV1
    {
        Confirmed = 1,
        Back = 2,
    }

    public sealed class CharacterSelectionOperationResultV1
    {
        internal CharacterSelectionOperationResultV1(
            CharacterSelectionOperationStatusV1 status,
            string rejectionCode,
            CharacterSelectionSnapshotV1 snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public CharacterSelectionOperationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public CharacterSelectionSnapshotV1 Snapshot { get; }

        public bool Changed
        {
            get { return Status == CharacterSelectionOperationStatusV1.Highlighted; }
        }
    }

    public sealed class CharacterSelectionSnapshotV1
    {
        internal CharacterSelectionSnapshotV1(
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

    public sealed class CharacterSelectionRouteResultV1
    {
        internal CharacterSelectionRouteResultV1(
            CharacterSelectionRouteStatusV1 status,
            HubRouteV1 targetRoute,
            PlayerRouteProfilePayloadV1 payload,
            string selectionFingerprint)
        {
            Status = status;
            TargetRoute = targetRoute;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            SelectionFingerprint = selectionFingerprint
                ?? throw new ArgumentNullException(nameof(selectionFingerprint));
        }

        public CharacterSelectionRouteStatusV1 Status { get; }

        public HubRouteV1 TargetRoute { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public string SelectionFingerprint { get; }
    }

    /// <summary>
    /// Presentation callback only. Implementations route the immutable result but may not
    /// replace or mutate its payload.
    /// </summary>
    public interface ICharacterSelectionRouteSinkV1
    {
        void Accept(CharacterSelectionRouteResultV1 result);
    }

    /// <summary>
    /// Pure selection coordinator. Highlighting changes only local draft state. Confirm
    /// creates one new immutable HUB payload while copying the incoming concrete equipment
    /// instance identities in their original slot order. Back returns the exact incoming
    /// payload instance.
    /// </summary>
    public sealed class CharacterSelectionServiceV1
    {
        private readonly CharacterSelectionCatalogV1 catalog;
        private readonly PlayerRouteProfilePayloadV1 incomingPayload;
        private readonly CharacterSelectionRouteResultV1 backResult;

        private CharacterSelectionDefinitionV1 highlightedCharacter;
        private CharacterClassProfileDefinitionV1 highlightedProfile;
        private CharacterSelectionRouteResultV1 confirmedResult;

        public CharacterSelectionServiceV1(
            CharacterSelectionCatalogV1 catalog,
            PlayerRouteProfilePayloadV1 incomingPayload)
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
            backResult = new CharacterSelectionRouteResultV1(
                CharacterSelectionRouteStatusV1.Back,
                HubRouteV1.MainMenu,
                incomingPayload,
                ExportSnapshot().SelectionFingerprint);
        }

        public CharacterSelectionCatalogV1 Catalog
        {
            get { return catalog; }
        }

        public PlayerRouteProfilePayloadV1 IncomingPayload
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

        public CharacterSelectionSnapshotV1 ExportSnapshot()
        {
            string fingerprint = BuildSelectionFingerprint(
                catalog.Fingerprint,
                incomingPayload.Fingerprint,
                highlightedCharacter.CharacterStableId,
                highlightedProfile.LoadoutProfileStableId);
            return new CharacterSelectionSnapshotV1(
                highlightedCharacter.CharacterStableId,
                highlightedProfile.LoadoutProfileStableId,
                catalog.Fingerprint,
                incomingPayload.Fingerprint,
                fingerprint);
        }

        public CharacterSelectionOperationResultV1 TryHighlightCharacter(
            StableId characterStableId)
        {
            if (confirmedResult != null)
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-already-confirmed");
            }

            CharacterSelectionDefinitionV1 candidate;
            if (!catalog.TryGetCharacter(characterStableId, out candidate))
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-character-unknown");
            }

            if (candidate.CharacterStableId == highlightedCharacter.CharacterStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.NoChange,
                    "character-selection-character-already-highlighted");
            }

            CharacterClassProfileDefinitionV1 defaultProfile;
            if (!catalog.TryGetProfile(
                candidate.DefaultLoadoutProfileStableId,
                out defaultProfile))
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-character-default-profile-unavailable");
            }

            highlightedCharacter = candidate;
            highlightedProfile = defaultProfile;
            return Operation(
                CharacterSelectionOperationStatusV1.Highlighted,
                string.Empty);
        }

        public CharacterSelectionOperationResultV1 TryHighlightProfile(
            StableId loadoutProfileStableId)
        {
            if (confirmedResult != null)
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-already-confirmed");
            }

            CharacterClassProfileDefinitionV1 candidate;
            if (!catalog.TryGetProfile(loadoutProfileStableId, out candidate))
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-profile-unknown");
            }

            if (candidate.CharacterStableId != highlightedCharacter.CharacterStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.Rejected,
                    "character-selection-profile-character-mismatch");
            }

            if (candidate.LoadoutProfileStableId
                == highlightedProfile.LoadoutProfileStableId)
            {
                return Operation(
                    CharacterSelectionOperationStatusV1.NoChange,
                    "character-selection-profile-already-highlighted");
            }

            highlightedProfile = candidate;
            return Operation(
                CharacterSelectionOperationStatusV1.Highlighted,
                string.Empty);
        }

        public CharacterSelectionRouteResultV1 Confirm()
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

            PlayerRouteProfilePayloadV1 selectedPayload =
                PlayerRouteProfilePayloadV1.Create(
                    highlightedCharacter.CharacterStableId,
                    highlightedProfile.LoadoutProfileStableId,
                    equipmentInstanceIds);
            confirmedResult = new CharacterSelectionRouteResultV1(
                CharacterSelectionRouteStatusV1.Confirmed,
                HubRouteV1.InventoryLoadoutHub,
                selectedPayload,
                ExportSnapshot().SelectionFingerprint);
            return confirmedResult;
        }

        public CharacterSelectionRouteResultV1 Back()
        {
            return backResult;
        }

        private void ResolveInitialSelection()
        {
            CharacterSelectionDefinitionV1 incomingCharacter;
            CharacterClassProfileDefinitionV1 incomingProfile;
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

        private CharacterSelectionOperationResultV1 Operation(
            CharacterSelectionOperationStatusV1 status,
            string rejectionCode)
        {
            return new CharacterSelectionOperationResultV1(
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
