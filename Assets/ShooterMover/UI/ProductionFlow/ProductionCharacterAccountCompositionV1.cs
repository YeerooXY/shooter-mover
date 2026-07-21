using System;
using System.Collections.Generic;
using System.IO;
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
    /// Persistent Unity adapter between the six-slot account aggregate and the existing
    /// production Hub graph. PlayerPrefs is read only as a legacy migration source. Once
    /// migrated, the atomic account file and PlayerAccountSaveAuthorityV1 are the durable
    /// truth; this component never owns XP, holdings, wallets, skills, loadout, or BOX.
    /// </summary>
    [DefaultExecutionOrder(-31950)]
    [DisallowMultipleComponent]
    public sealed class ProductionCharacterAccountCompositionV1 : MonoBehaviour
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
        private CharacterCompositionCoordinatorV1 composition;
        private ProductionFlowProfileRecordV1 currentProfile;
        private string diagnostic = string.Empty;
        private bool initialized;
        private bool failed;

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
            EnsureInstalled();
            if (instance == null || !instance.SynchronizeNow())
            {
                graph = null;
                profile = null;
                return false;
            }

            graph = instance.composition.ActiveRuntime
                as ProductionCharacterRuntimeGraphV1;
            profile = instance.currentProfile;
            return graph != null && profile != null && !graph.IsDisposed;
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
            string active = Path.Combine(
                Application.persistentDataPath,
                AccountFileName);
            accountStore = new AtomicPlayerAccountStoreV1(
                new SystemIoAtomicSaveFilePortV1(),
                active,
                active + TemporarySuffix,
                active + BackupSuffix,
                PlayerAccountComponentSemanticsV1.Validate);

            PlayerAccountStoreResultV1 loaded = accountStore.Load();
            PlayerAccountSnapshotV1 account;
            if (loaded.Status == PlayerAccountStoreStatusV1.NotFound)
            {
                account = PlayerAccountSnapshotV1.Empty(AccountStableId);
            }
            else if (loaded.Succeeded && loaded.Snapshot != null)
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
            var graphFactory = ProductionCharacterRuntimeGraphFactoryV1
                .CreateVerticalSliceDefaults();
            composition = new CharacterCompositionCoordinatorV1(
                accountAuthority,
                graphFactory,
                accountStore.Save,
                PlayerAccountComponentSemanticsV1.Validate);

            LegacyCharacterProfileMigrationResultV1 migration =
                MigrateMissingLegacySlots(graphFactory);
            if (migration != null && !migration.Succeeded)
            {
                Fail(
                    "character-account-migration-rejected:"
                        + migration.Diagnostic);
                return;
            }

            if (loaded.Status == PlayerAccountStoreStatusV1.NotFound
                && (migration == null
                    || migration.Status
                        == CharacterCompositionStatusV1.ExactNoChange))
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

            initialized = true;
            SynchronizeNow();
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

            ProductionFlowProfileRecordV1 legacyProfile = flow.Profile;
            if (legacyProfile == null)
            {
                composition.UnbindActive();
                currentProfile = null;
                return false;
            }

            int slotIndex = flow.ActiveProfileSlotIndex;
            if (slotIndex < 0
                || slotIndex >= PlayerAccountSnapshotV1.CharacterSlotCount)
            {
                Fail("character-composition-active-slot-invalid");
                return false;
            }

            CharacterInstanceSnapshotV1 accountCharacter =
                accountAuthority.Current.CharacterAt(slotIndex);
            if (accountCharacter == null)
            {
                var graphFactory = ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
                LegacyCharacterProfileMigrationResultV1 migration =
                    MigrateOne(slotIndex, legacyProfile, graphFactory);
                if (migration == null || !migration.Succeeded)
                {
                    Fail(
                        "character-composition-selected-slot-migration-failed:"
                            + (migration == null
                                ? "result-null"
                                : migration.Diagnostic));
                    return false;
                }
                accountCharacter =
                    accountAuthority.Current.CharacterAt(slotIndex);
            }

            bool alreadySelected = composition.ActiveRuntime != null
                && !composition.ActiveRuntime.IsDisposed
                && composition.ActiveSlotIndex == slotIndex
                && composition.ActiveRuntime.Character
                    .CharacterInstanceStableId
                    == accountCharacter.CharacterInstanceStableId;
            if (!alreadySelected)
            {
                CharacterCompositionResultV1 selected =
                    composition.Select(slotIndex);
                if (selected == null || !selected.Succeeded)
                {
                    diagnostic =
                        "character-composition-restore-rejected:"
                        + (selected == null
                            ? "result-null"
                            : selected.Diagnostic);
                    return false;
                }
            }

            ProductionCharacterRuntimeGraphV1 graph =
                composition.ActiveRuntime
                    as ProductionCharacterRuntimeGraphV1;
            if (graph == null || graph.IsDisposed)
            {
                diagnostic =
                    "character-composition-production-graph-unavailable";
                return false;
            }

            currentProfile = new ProductionFlowProfileRecordV1(
                graph.Character.DisplayName,
                BuildCurrentRoute(graph));
            return true;
        }

        private CharacterCompositionResultV1 Persist(
            string mutationScope,
            string immutableMutationFingerprint)
        {
            if (string.IsNullOrWhiteSpace(mutationScope)
                || string.IsNullOrWhiteSpace(immutableMutationFingerprint))
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
                diagnostic =
                    "character-composition-save-rejected:"
                    + (result == null
                        ? "result-null"
                        : result.Diagnostic);
                return result;
            }

            ProductionCharacterRuntimeGraphV1 graph =
                composition.ActiveRuntime
                    as ProductionCharacterRuntimeGraphV1;
            if (graph != null && !graph.IsDisposed)
            {
                currentProfile = new ProductionFlowProfileRecordV1(
                    graph.Character.DisplayName,
                    BuildCurrentRoute(graph));
            }
            return result;
        }

        private LegacyCharacterProfileMigrationResultV1
            MigrateMissingLegacySlots(
                ProductionCharacterRuntimeGraphFactoryV1 graphFactory)
        {
            var legacy = new List<LegacyCharacterProfileV1>();
            for (int slotIndex = 0;
                slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                slotIndex++)
            {
                if (accountAuthority.Current.CharacterAt(slotIndex) != null)
                {
                    continue;
                }
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

        private LegacyCharacterProfileMigrationResultV1 MigrateOne(
            int slotIndex,
            ProductionFlowProfileRecordV1 record,
            ProductionCharacterRuntimeGraphFactoryV1 graphFactory)
        {
            return new LegacyCharacterProfileMigrationV1(
                accountAuthority,
                graphFactory,
                accountStore.Save).Migrate(new[] { Legacy(slotIndex, record) });
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

        private static PlayerRouteProfilePayloadV1 BuildCurrentRoute(
            ProductionCharacterRuntimeGraphV1 graph)
        {
            InventoryLoadoutAuthoritySnapshotV1 snapshot =
                graph.LoadoutRuntime.LoadoutAuthority.ExportSnapshot();
            var instances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                instances.Add(snapshot.GetBinding(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId)
                    .EquipmentInstanceStableId);
            }
            return PlayerRouteProfilePayloadV1.Create(
                graph.Character.CharacterInstanceStableId,
                graph.Character.ClassDefinitionStableId,
                instances);
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

        private void OnDestroy()
        {
            if (composition != null)
            {
                composition.Dispose();
            }
            if (instance == this)
            {
                instance = null;
            }
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
