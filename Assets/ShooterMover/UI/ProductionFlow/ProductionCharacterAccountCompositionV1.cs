using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Account-backed profile lifecycle used by Character Select. PlayerPrefs may supply
    /// the one-time migration input and thereafter receives only account projections.
    /// </summary>
    public interface IProductionCharacterProfileLifecycleV1
    {
        bool TryExportProfiles(
            out IReadOnlyList<ProductionFlowProfileRecordV1> profiles,
            out string rejectionCode);

        bool TryActivate(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out ProductionFlowProfileRecordV1 authoritativeProfile,
            out string rejectionCode);

        bool TryDelete(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out string rejectionCode);
    }

    /// <summary>
    /// Persistent Unity adapter between the six-slot account aggregate and the existing
    /// production Hub graph. It composes existing authorities, merged save adapters, the
    /// existing account save authority, and the atomic file store. It owns no subsystem
    /// state and creates no replacement XP, holdings, wallet, skill, loadout, or BOX model.
    /// </summary>
    [DefaultExecutionOrder(-31950)]
    [DisallowMultipleComponent]
    public sealed class ProductionCharacterAccountCompositionV1 :
        MonoBehaviour,
        IProductionCharacterProfileLifecycleV1
    {
        private const string AccountFileName = "player-account-v1.save";
        private const string TemporarySuffix = ".tmp";
        private const string BackupSuffix = ".bak";
        private static readonly StableId AccountStableId =
            StableId.Parse("account.production-player-v1");
        private static ProductionCharacterAccountCompositionV1 instance;

        private ProductionFlowCoordinatorV1 flow;
        private PlayerPrefsProductionFlowProfileStoreV1 legacyStore;
        private AtomicPlayerAccountStoreV1 accountStore;
        private PlayerAccountSaveAuthorityV1 accountAuthority;
        private ProductionCharacterRuntimeGraphFactoryV1 graphFactory;
        private CharacterCompositionCoordinatorV1 composition;
        private ProductionFlowProfileRecordV1 currentProfile;
        private string diagnostic = string.Empty;
        private bool initialized;
        private bool failed;
        private bool quitting;

        public CharacterCompositionCoordinatorV1 Composition
        {
            get { return composition; }
        }

        public PlayerAccountSnapshotV1 Account
        {
            get { return accountAuthority == null ? null : accountAuthority.Current; }
        }

        public ProductionFlowProfileRecordV1 CurrentProfile
        {
            get { return currentProfile; }
        }

        public string Diagnostic
        {
            get { return diagnostic; }
        }

        public static bool TryResolveCurrent(
            out ProductionCharacterRuntimeGraphV1 graph,
            out ProductionFlowProfileRecordV1 profile)
        {
            CharacterCompositionCoordinatorV1 ignored;
            return TryResolveCurrent(
                out graph,
                out profile,
                out ignored);
        }

        public static bool TryResolveCurrent(
            out ProductionCharacterRuntimeGraphV1 graph,
            out ProductionFlowProfileRecordV1 profile,
            out CharacterCompositionCoordinatorV1 currentComposition)
        {
            EnsureInstalled();
            if (instance == null || !instance.SynchronizeNow())
            {
                graph = null;
                profile = null;
                currentComposition = null;
                return false;
            }

            graph = instance.composition.ActiveRuntime
                as ProductionCharacterRuntimeGraphV1;
            profile = instance.currentProfile;
            currentComposition = instance.composition;
            return graph != null
                && profile != null
                && currentComposition != null
                && !graph.IsDisposed;
        }

        public static CharacterCompositionResultV1 PersistCurrent(
            string mutationScope,
            string immutableMutationFingerprint)
        {
            EnsureInstalled();
            if (instance == null || !instance.SynchronizeNow())
            {
                return null;
            }
            return instance.Persist(
                mutationScope,
                immutableMutationFingerprint);
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            EnsureInstalled();
        }

        private static void EnsureInstalled()
        {
            ProductionFlowCoordinatorV1 coordinator =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (coordinator == null)
            {
                return;
            }

            ProductionCharacterAccountCompositionV1 existing =
                coordinator.GetComponent<
                    ProductionCharacterAccountCompositionV1>();
            if (existing == null)
            {
                existing = coordinator.gameObject.AddComponent<
                    ProductionCharacterAccountCompositionV1>();
            }
            instance = existing;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            flow = GetComponent<ProductionFlowCoordinatorV1>();
            Initialize();
        }

        private void Update()
        {
            SynchronizeNow();
        }

        private void Initialize()
        {
            if (initialized || failed)
            {
                return;
            }
            if (flow == null)
            {
                Fail("character-composition-flow-coordinator-missing");
                return;
            }

            legacyStore = new PlayerPrefsProductionFlowProfileStoreV1();
            string activePath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                AccountFileName);
            accountStore = new AtomicPlayerAccountStoreV1(
                new SystemIoAtomicSaveFilePortV1(),
                activePath,
                activePath + TemporarySuffix,
                activePath + BackupSuffix,
                snapshot => PlayerAccountComponentSemanticsV1.Validate(snapshot));

            PlayerAccountStoreResultV1 loaded = accountStore.Load();
            bool firstAccount = loaded != null
                && loaded.Status == PlayerAccountStoreStatusV1.NotFound;
            PlayerAccountSnapshotV1 account;
            if (firstAccount)
            {
                account = PlayerAccountSnapshotV1.Empty(AccountStableId);
            }
            else if (loaded != null
                && loaded.Succeeded
                && loaded.Snapshot != null)
            {
                account = loaded.Snapshot;
                if (loaded.Status
                    == PlayerAccountStoreStatusV1.RecoveredLastKnownGood)
                {
                    diagnostic =
                        "character-account-recovered-last-known-good:"
                        + loaded.RejectionCode;
                }
            }
            else
            {
                Fail(
                    "character-account-load-rejected:"
                        + (loaded == null
                            ? "result-null"
                            : loaded.RejectionCode));
                return;
            }

            accountAuthority = new PlayerAccountSaveAuthorityV1(account);
            graphFactory = ProductionCharacterRuntimeGraphFactoryV1
                .CreateVerticalSliceDefaults();
            composition = new CharacterCompositionCoordinatorV1(
                accountAuthority,
                graphFactory,
                accountStore.Save,
                snapshot => PlayerAccountComponentSemanticsV1.Validate(snapshot));

            if (firstAccount)
            {
                LegacyCharacterProfileMigrationResultV1 migration =
                    MigrateLegacyAccountOnce();
                if (migration == null || !migration.Succeeded)
                {
                    Fail(
                        "character-account-migration-rejected:"
                            + (migration == null
                                ? "result-null"
                                : migration.Diagnostic));
                    return;
                }
                if (migration.Status
                    == CharacterCompositionStatusV1.ExactNoChange)
                {
                    PlayerAccountStoreResultV1 initialSave =
                        accountStore.Save(accountAuthority.Current);
                    if (initialSave == null || !initialSave.Succeeded)
                    {
                        Fail(
                            "character-account-initial-save-rejected:"
                                + (initialSave == null
                                    ? "result-null"
                                    : initialSave.RejectionCode));
                        return;
                    }
                }
            }

            initialized = true;
            if (!flow.ConnectCharacterProfileLifecycle(this))
            {
                Fail("character-profile-lifecycle-connect-rejected");
                return;
            }
            SynchronizeNow();
        }

        public bool TryExportProfiles(
            out IReadOnlyList<ProductionFlowProfileRecordV1> profiles,
            out string rejectionCode)
        {
            profiles = null;
            rejectionCode = string.Empty;
            if (!initialized || failed || accountAuthority == null)
            {
                rejectionCode = "character-account-not-ready";
                return false;
            }

            var projection = new ProductionFlowProfileRecordV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            for (int slotIndex = 0;
                slotIndex < projection.Length;
                slotIndex++)
            {
                CharacterInstanceSnapshotV1 character =
                    accountAuthority.Current.CharacterAt(slotIndex);
                if (character == null)
                {
                    continue;
                }
                if (!TryProject(character, out projection[slotIndex],
                    out rejectionCode))
                {
                    return false;
                }
            }
            profiles = projection;
            return true;
        }

        public bool TryActivate(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out ProductionFlowProfileRecordV1 authoritativeProfile,
            out string rejectionCode)
        {
            authoritativeProfile = null;
            rejectionCode = string.Empty;
            if (!initialized || failed || requestedProfile == null)
            {
                rejectionCode = "character-activation-request-invalid";
                return false;
            }
            if (slotIndex < 0
                || slotIndex >= PlayerAccountSnapshotV1.CharacterSlotCount)
            {
                rejectionCode = "character-activation-slot-invalid";
                return false;
            }

            CharacterInstanceSnapshotV1 character =
                accountAuthority.Current.CharacterAt(slotIndex);
            if (character == null)
            {
                LegacyCharacterProfileMigrationResultV1 migration =
                    new LegacyCharacterProfileMigrationV1(
                        accountAuthority,
                        graphFactory,
                        accountStore.Save).Migrate(new[]
                        {
                            Legacy(slotIndex, requestedProfile),
                        });
                if (migration == null || !migration.Succeeded)
                {
                    rejectionCode = migration == null
                        ? "character-create-migration-result-null"
                        : migration.Diagnostic;
                    return false;
                }
                character = accountAuthority.Current.CharacterAt(slotIndex);
            }

            if (!TryProject(
                character,
                out authoritativeProfile,
                out rejectionCode))
            {
                return false;
            }

            CharacterCompositionResultV1 selected = composition.Select(slotIndex);
            if (selected == null || !selected.Succeeded)
            {
                rejectionCode = selected == null
                    ? "character-restore-result-null"
                    : selected.Diagnostic;
                authoritativeProfile = null;
                return false;
            }

            currentProfile = authoritativeProfile;
            diagnostic = string.Empty;
            return true;
        }

        public bool TryDelete(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (!initialized || failed || requestedProfile == null)
            {
                rejectionCode = "character-delete-request-invalid";
                return false;
            }
            if (slotIndex < 0
                || slotIndex >= PlayerAccountSnapshotV1.CharacterSlotCount)
            {
                rejectionCode = "character-delete-slot-invalid";
                return false;
            }

            CharacterInstanceSnapshotV1 character =
                accountAuthority.Current.CharacterAt(slotIndex);
            if (character == null)
            {
                return true;
            }
            if (requestedProfile.Payload.SelectedCharacterStableId
                    != character.CharacterInstanceStableId
                || requestedProfile.Payload.LoadoutProfileStableId
                    != character.ClassDefinitionStableId)
            {
                rejectionCode = "character-delete-identity-mismatch";
                return false;
            }

            if (composition.ActiveSlotIndex == slotIndex)
            {
                CharacterCompositionResultV1 persisted =
                    PersistCurrentState("character-delete");
                if (persisted == null || !persisted.Succeeded)
                {
                    rejectionCode = persisted == null
                        ? "character-delete-pre-save-result-null"
                        : persisted.Diagnostic;
                    return false;
                }
                composition.UnbindActive();
                currentProfile = null;
            }

            PlayerAccountSaveAuthoritySnapshotV1 rollback =
                accountAuthority.ExportSnapshot();
            PlayerAccountSaveResultV1 deleted = accountAuthority.Apply(
                PlayerAccountSaveCommandV1.DeleteCharacter(
                    StableId.Parse(
                        "operation.character-delete-"
                            + Hash(character.Fingerprint)),
                    accountAuthority.Current.Revision,
                    slotIndex,
                    character.CharacterInstanceStableId));
            if (deleted == null
                || (deleted.Status != PlayerAccountSaveStatusV1.Applied
                    && deleted.Status
                        != PlayerAccountSaveStatusV1.ExactDuplicateNoChange))
            {
                rejectionCode = deleted == null
                    ? "character-delete-account-result-null"
                    : deleted.RejectionCode;
                return false;
            }

            PlayerAccountStoreResultV1 stored =
                accountStore.Save(accountAuthority.Current);
            if (stored == null || !stored.Succeeded)
            {
                string rollbackError;
                accountAuthority.TryImport(rollback, out rollbackError);
                rejectionCode = stored == null
                    ? "character-delete-store-result-null"
                    : stored.RejectionCode
                        + (string.IsNullOrEmpty(rollbackError)
                            ? string.Empty
                            : ";rollback=" + rollbackError);
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private bool SynchronizeNow()
        {
            if (failed)
            {
                return false;
            }
            if (!initialized)
            {
                Initialize();
            }
            if (!initialized || composition == null || flow == null)
            {
                return false;
            }

            ProductionFlowProfileRecordV1 selectedProfile = flow.Profile;
            if (selectedProfile == null)
            {
                composition.UnbindActive();
                currentProfile = null;
                return false;
            }

            int slotIndex = flow.ActiveProfileSlotIndex;
            CharacterInstanceSnapshotV1 character =
                accountAuthority.Current.CharacterAt(slotIndex);
            if (character == null)
            {
                diagnostic = "character-composition-active-slot-empty";
                return false;
            }

            bool alreadySelected = composition.ActiveRuntime != null
                && !composition.ActiveRuntime.IsDisposed
                && composition.ActiveSlotIndex == slotIndex
                && composition.ActiveRuntime.Character
                    .CharacterInstanceStableId
                    == character.CharacterInstanceStableId;
            if (!alreadySelected)
            {
                if (composition.ActiveRuntime != null)
                {
                    CharacterCompositionResultV1 persisted =
                        PersistCurrentState("character-slot-switch");
                    if (persisted == null || !persisted.Succeeded)
                    {
                        diagnostic = persisted == null
                            ? "character-slot-switch-save-result-null"
                            : persisted.Diagnostic;
                        return false;
                    }
                }

                CharacterCompositionResultV1 selected =
                    composition.Select(slotIndex);
                if (selected == null || !selected.Succeeded)
                {
                    diagnostic = selected == null
                        ? "character-composition-restore-result-null"
                        : selected.Diagnostic;
                    currentProfile = null;
                    return false;
                }
            }

            if (!TryProject(character, out currentProfile, out diagnostic))
            {
                return false;
            }
            return composition.ActiveRuntime
                is ProductionCharacterRuntimeGraphV1;
        }

        private CharacterCompositionResultV1 PersistCurrentState(string scope)
        {
            if (composition == null || composition.ActiveRuntime == null)
            {
                return null;
            }
            IReadOnlyList<SaveComponentSnapshotV1> components;
            try
            {
                components = PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    composition.ActiveRuntime.SaveAdapters);
            }
            catch (Exception exception)
            {
                diagnostic = "character-state-fingerprint-export-threw:"
                    + exception.GetType().Name;
                return null;
            }

            string fingerprint = string.Join(
                "|",
                components.OrderBy(
                        item => item.ComponentStableId.ToString(),
                        StringComparer.Ordinal)
                    .Select(item => item.Fingerprint));
            return Persist(scope, Hash(fingerprint));
        }

        private CharacterCompositionResultV1 Persist(
            string mutationScope,
            string immutableMutationFingerprint)
        {
            if (string.IsNullOrWhiteSpace(mutationScope)
                || string.IsNullOrWhiteSpace(immutableMutationFingerprint)
                || composition == null)
            {
                return null;
            }

            StableId operationId = StableId.Parse(
                "operation.character-save-"
                    + Hash(
                        mutationScope.Trim()
                            + "|"
                            + immutableMutationFingerprint.Trim()));
            CharacterCompositionResultV1 result =
                composition.PersistActive(operationId);
            if (result == null || !result.Succeeded)
            {
                diagnostic = result == null
                    ? "character-composition-save-result-null"
                    : result.Diagnostic;
                return result;
            }

            CharacterInstanceSnapshotV1 persisted = result.Character;
            if (persisted != null)
            {
                string ignored;
                TryProject(persisted, out currentProfile, out ignored);
            }
            diagnostic = string.Empty;
            return result;
        }

        private LegacyCharacterProfileMigrationResultV1
            MigrateLegacyAccountOnce()
        {
            var legacy = new List<LegacyCharacterProfileV1>();
            for (int slotIndex = 0;
                slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                slotIndex++)
            {
                ProductionFlowProfileRecordV1 record;
                if (legacyStore.TryLoad(slotIndex, out record))
                {
                    legacy.Add(Legacy(slotIndex, record));
                }
            }
            return new LegacyCharacterProfileMigrationV1(
                accountAuthority,
                graphFactory,
                accountStore.Save).Migrate(legacy);
        }

        private static LegacyCharacterProfileV1 Legacy(
            int slotIndex,
            ProductionFlowProfileRecordV1 record)
        {
            return new LegacyCharacterProfileV1(
                slotIndex,
                record.DisplayName,
                record.Payload.SelectedCharacterStableId,
                record.Payload.LoadoutProfileStableId,
                record.Payload.Fingerprint,
                record.Payload);
        }

        private static bool TryProject(
            CharacterInstanceSnapshotV1 character,
            out ProductionFlowProfileRecordV1 profile,
            out string rejectionCode)
        {
            profile = null;
            rejectionCode = string.Empty;
            SaveComponentSnapshotV1 component;
            if (!character.TryGetComponent(
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout()
                    .ComponentStableId,
                out component))
            {
                rejectionCode = "character-projection-loadout-missing";
                return false;
            }

            InventoryLoadoutAuthoritySnapshotV1 loadout;
            if (!KnownSaveComponentCodecsV1.ExactInstanceLoadout.TryDecode(
                component.CanonicalPayload,
                out loadout,
                out rejectionCode))
            {
                rejectionCode =
                    "character-projection-loadout-invalid:" + rejectionCode;
                return false;
            }

            var instances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                instances.Add(loadout.GetBinding(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId)
                    .EquipmentInstanceStableId);
            }

            try
            {
                profile = new ProductionFlowProfileRecordV1(
                    character.DisplayName,
                    PlayerRouteProfilePayloadV1.Create(
                        character.CharacterInstanceStableId,
                        character.ClassDefinitionStableId,
                        instances));
                return true;
            }
            catch (Exception exception)
            {
                rejectionCode = "character-projection-threw:"
                    + exception.GetType().Name;
                return false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && initialized && !failed)
            {
                PersistCurrentState("application-pause");
            }
        }

        private void OnApplicationQuit()
        {
            quitting = true;
            if (initialized && !failed)
            {
                PersistCurrentState("application-quit");
            }
        }

        private void OnDestroy()
        {
            if (!quitting && initialized && !failed)
            {
                PersistCurrentState("composition-destroy");
            }
            if (composition != null)
            {
                composition.Dispose();
            }
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Fail(string rejectionCode)
        {
            diagnostic = rejectionCode ?? "character-composition-failed";
            failed = true;
            currentProfile = null;
            if (composition != null)
            {
                composition.UnbindActive();
            }
            Debug.LogError(diagnostic, this);
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private sealed class SystemIoAtomicSaveFilePortV1 :
            IAtomicSaveFilePortV1
        {
            public bool Exists(string path)
            {
                return File.Exists(path);
            }

            public string ReadAllText(string path)
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }

            public void WriteAllText(string path, string contents)
            {
                EnsureDirectory(path);
                File.WriteAllText(path, contents, new UTF8Encoding(false));
            }

            public void Move(string sourcePath, string destinationPath)
            {
                EnsureDirectory(destinationPath);
                File.Move(sourcePath, destinationPath);
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                EnsureDirectory(destinationPath);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Replace(
                    sourcePath,
                    destinationPath,
                    backupPath,
                    true);
            }

            public void Delete(string path)
            {
                File.Delete(path);
            }

            private static void EnsureDirectory(string path)
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
    }
}
