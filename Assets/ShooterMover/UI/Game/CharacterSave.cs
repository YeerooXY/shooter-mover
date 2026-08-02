using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Account-backed profile lifecycle used by Character Select. PlayerPrefs may supply
    /// the one-time migration input and thereafter receives only account projections.
    /// </summary>
    public interface ICharacterProfiles
    {
        bool TryExportProfiles(
            out IReadOnlyList<FlowProfileRecord> profiles,
            out string rejectionCode);

        bool TryActivate(
            int slotIndex,
            FlowProfileRecord requestedProfile,
            out FlowProfileRecord authoritativeProfile,
            out string rejectionCode);

        bool TryDelete(
            int slotIndex,
            FlowProfileRecord requestedProfile,
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
    public sealed class CharacterSave :
        MonoBehaviour,
        ICharacterProfiles
    {
        private const string AccountFileName = "player-account-v1.save";
        private const string TemporarySuffix = ".tmp";
        private const string BackupSuffix = ".bak";
        private const string OldGunInventoryPayloadInvalid =
            "gun-holdings-v2-payload-invalid";
        private const string OldGunInventoryLoadRejection =
            "active=" + OldGunInventoryPayloadInvalid
                + ";backup=" + OldGunInventoryPayloadInvalid;
        private static readonly StableId AccountStableId =
            StableId.Parse("account.production-player-v1");
        private static CharacterSave instance;

        private GameFlow flow;
        private PlayerPrefsFlowProfileStore legacyStore;
        private GameSaveFile accountStore;
        private PlayerAccountSaveState accountAuthority;
        private CharacterLiveGraphFactory graphFactory;
        private CharacterSetupFlow composition;
        private FlowProfileRecord currentProfile;
        private string diagnostic = string.Empty;
        private bool initialized;
        private bool failed;
        private bool quitting;

        public CharacterSetupFlow Composition
        {
            get { return composition; }
        }

        public PlayerAccountSnapshot Account
        {
            get { return accountAuthority == null ? null : accountAuthority.Current; }
        }

        public FlowProfileRecord CurrentProfile
        {
            get { return currentProfile; }
        }

        public string Diagnostic
        {
            get { return diagnostic; }
        }

        public static string CurrentDiagnostic
        {
            get
            {
                EnsureInstalled();
                return instance == null
                    ? "character-account-composition-missing"
                    : string.IsNullOrWhiteSpace(instance.diagnostic)
                        ? "character-account-current-runtime-unavailable"
                        : instance.diagnostic;
            }
        }

        public static bool TryResolveCurrent(
            out CharacterLiveGraph graph,
            out FlowProfileRecord profile)
        {
            CharacterSetupFlow ignored;
            return TryResolveCurrent(
                out graph,
                out profile,
                out ignored);
        }

        public static bool TryResolveCurrent(
            out CharacterLiveGraph graph,
            out FlowProfileRecord profile,
            out CharacterSetupFlow currentComposition)
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
                as CharacterLiveGraph;
            profile = instance.currentProfile;
            currentComposition = instance.composition;
            return graph != null
                && profile != null
                && currentComposition != null
                && !graph.IsDisposed;
        }

        public static CharacterSetupResult PersistCurrent(
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
            GameFlow coordinator =
                UnityEngine.Object.FindFirstObjectByType<
                    GameFlow>(
                    FindObjectsInactive.Include);
            if (coordinator == null)
            {
                return;
            }

            CharacterSave existing =
                coordinator.GetComponent<
                    CharacterSave>();
            if (existing == null)
            {
                existing = coordinator.gameObject.AddComponent<
                    CharacterSave>();
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
            flow = GetComponent<GameFlow>();
            try
            {
                Initialize();
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }
                Fail(
                    "character-composition-initialize-threw:"
                        + DescribeException(exception));
            }
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

            legacyStore = new PlayerPrefsFlowProfileStore();
            string activePath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                AccountFileName);
            string temporaryPath = activePath + TemporarySuffix;
            string backupPath = activePath + BackupSuffix;
            var files = new SystemIoAtomicSaveFilePort();
            accountStore = new GameSaveFile(
                files,
                activePath,
                temporaryPath,
                backupPath,
                snapshot => GameSaveRules.Validate(snapshot));

            PlayerAccountStoreResult loaded = accountStore.Load();
            bool resetOldSave = IsOldGunInventorySave(loaded);
            if (resetOldSave)
            {
                string resetError;
                if (!TryDeleteOldAccountFiles(
                        files,
                        activePath,
                        temporaryPath,
                        backupPath,
                        out resetError))
                {
                    Fail(
                        "character-account-old-save-reset-rejected:"
                            + resetError);
                    return;
                }

                loaded = accountStore.Load();
                if (loaded == null
                    || loaded.Status != PlayerAccountStoreStatus.NotFound)
                {
                    Fail(
                        "character-account-old-save-reset-verification-rejected:"
                            + (loaded == null
                                ? "result-null"
                                : loaded.RejectionCode));
                    return;
                }
            }

            bool firstAccount = loaded != null
                && loaded.Status == PlayerAccountStoreStatus.NotFound;
            PlayerAccountSnapshot account;
            if (firstAccount)
            {
                account = PlayerAccountSnapshot.Empty(AccountStableId);
            }
            else if (loaded != null
                && loaded.Succeeded
                && loaded.Snapshot != null)
            {
                account = loaded.Snapshot;
                if (loaded.Status
                    == PlayerAccountStoreStatus.RecoveredLastKnownGood)
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

            graphFactory = CharacterLiveGraphFactory
                .CreateVerticalSliceDefaults();

            if (!firstAccount)
            {
                RetiredGunSaveMigrationResult gunMigration =
                    RetiredGunSaveMigration.Migrate(account);
                if (gunMigration == null || !gunMigration.Succeeded)
                {
                    Fail(
                        "retired-gun-save-migration-rejected:"
                            + (gunMigration == null
                                ? "result-null"
                                : gunMigration.Diagnostic));
                    return;
                }
                if (gunMigration.Changed)
                {
                    PlayerAccountStoreResult migratedSave =
                        accountStore.Save(gunMigration.Account);
                    if (migratedSave == null
                        || !migratedSave.Succeeded
                        || migratedSave.Snapshot == null)
                    {
                        Fail(
                            "retired-gun-save-migration-store-rejected:"
                                + (migratedSave == null
                                    ? "result-null"
                                    : migratedSave.RejectionCode));
                        return;
                    }
                    account = migratedSave.Snapshot;
                }
            }

            if (!firstAccount)
            {
                CharacterSaveUpgradeResult backfill =
                    CharacterSaveUpgrade.Migrate(
                        account,
                        graphFactory);
                if (backfill == null || !backfill.Succeeded)
                {
                    Fail(
                        "required-character-component-backfill-rejected:"
                            + (backfill == null
                                ? "result-null"
                                : backfill.Diagnostic));
                    return;
                }
                if (backfill.Changed)
                {
                    PlayerAccountStoreResult backfilledSave =
                        accountStore.Save(backfill.Account);
                    if (backfilledSave == null
                        || !backfilledSave.Succeeded
                        || backfilledSave.Snapshot == null)
                    {
                        Fail(
                            "required-character-component-backfill-store-rejected:"
                                + (backfilledSave == null
                                    ? "result-null"
                                    : backfilledSave.RejectionCode));
                        return;
                    }
                    account = backfilledSave.Snapshot;
                }
            }

            accountAuthority = new PlayerAccountSaveState(account);
            composition = new CharacterSetupFlow(
                accountAuthority,
                graphFactory,
                accountStore.Save,
                snapshot => GameSaveRules.Validate(snapshot));

            if (firstAccount)
            {
                if (resetOldSave)
                {
                    PlayerAccountStoreResult initialSave =
                        accountStore.Save(accountAuthority.Current);
                    if (initialSave == null
                        || !initialSave.Succeeded
                        || initialSave.Snapshot == null)
                    {
                        Fail(
                            "character-account-old-save-reset-store-rejected:"
                                + (initialSave == null
                                    ? "result-null"
                                    : initialSave.RejectionCode));
                        return;
                    }

                    Debug.LogWarning(
                        "character-account-old-save-reset:"
                            + OldGunInventoryPayloadInvalid,
                        this);
                }
                else
                {
                    SaveMigrationResult migration =
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
                        == CharacterSetupStatus.ExactNoChange)
                    {
                        PlayerAccountStoreResult initialSave =
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
            out IReadOnlyList<FlowProfileRecord> profiles,
            out string rejectionCode)
        {
            profiles = null;
            rejectionCode = string.Empty;
            if (!initialized || failed || accountAuthority == null)
            {
                rejectionCode = "character-account-not-ready";
                return false;
            }

            var projection = new FlowProfileRecord[
                PlayerAccountSnapshot.CharacterSlotCount];
            for (int slotIndex = 0;
                 slotIndex < projection.Length;
                 slotIndex++)
            {
                CharacterInstanceSnapshot character =
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
            FlowProfileRecord requestedProfile,
            out FlowProfileRecord authoritativeProfile,
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
                || slotIndex >= PlayerAccountSnapshot.CharacterSlotCount)
            {
                rejectionCode = "character-activation-slot-invalid";
                return false;
            }

            CharacterInstanceSnapshot character =
                accountAuthority.Current.CharacterAt(slotIndex);
            if (character == null)
            {
                SaveMigrationResult migration =
                    new SaveMigration(
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

            CharacterSetupResult selected = composition.Select(slotIndex);
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
            FlowProfileRecord requestedProfile,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (!initialized || failed || requestedProfile == null)
            {
                rejectionCode = "character-delete-request-invalid";
                return false;
            }
            if (slotIndex < 0
                || slotIndex >= PlayerAccountSnapshot.CharacterSlotCount)
            {
                rejectionCode = "character-delete-slot-invalid";
                return false;
            }

            CharacterInstanceSnapshot character =
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
                CharacterSetupResult persisted =
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

            PlayerAccountSaveStateSnapshot rollback =
                accountAuthority.ExportSnapshot();
            PlayerAccountSaveResult deleted = accountAuthority.Apply(
                PlayerAccountSaveCommand.DeleteCharacter(
                    StableId.Parse(
                        "operation.character-delete-"
                            + Hash(character.Fingerprint)),
                    accountAuthority.Current.Revision,
                    slotIndex,
                    character.CharacterInstanceStableId));
            if (deleted == null
                || (deleted.Status != PlayerAccountSaveStatus.Applied
                    && deleted.Status
                        != PlayerAccountSaveStatus.ExactDuplicateNoChange))
            {
                rejectionCode = deleted == null
                    ? "character-delete-account-result-null"
                    : deleted.RejectionCode;
                return false;
            }

            PlayerAccountStoreResult stored =
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
                diagnostic = "character-composition-not-ready";
                return false;
            }

            FlowProfileRecord selectedProfile = flow.Profile;
            if (selectedProfile == null)
            {
                composition.UnbindActive();
                currentProfile = null;
                diagnostic = "character-composition-selected-profile-missing";
                return false;
            }

            int slotIndex = flow.ActiveProfileSlotIndex;
            CharacterInstanceSnapshot character =
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
                    CharacterSetupResult persisted =
                        PersistCurrentState("character-slot-switch");
                    if (persisted == null || !persisted.Succeeded)
                    {
                        diagnostic = persisted == null
                            ? "character-slot-switch-save-result-null"
                            : persisted.Diagnostic;
                        return false;
                    }
                }

                CharacterSetupResult selected =
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
                is CharacterLiveGraph;
        }

        private CharacterSetupResult PersistCurrentState(string scope)
        {
            if (composition == null || composition.ActiveRuntime == null)
            {
                return null;
            }
            IReadOnlyList<SavePartSnapshot> components;
            try
            {
                components = PlayerAccountRestoreFlow.ExportComponents(
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

        private CharacterSetupResult Persist(
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
            CharacterSetupResult result =
                composition.PersistActive(operationId);
            if (result == null || !result.Succeeded)
            {
                diagnostic = result == null
                    ? "character-composition-save-result-null"
                    : result.Diagnostic;
                return result;
            }

            CharacterInstanceSnapshot persisted = result.Character;
            if (persisted != null)
            {
                string ignored;
                TryProject(persisted, out currentProfile, out ignored);
            }
            diagnostic = string.Empty;
            return result;
        }

        private SaveMigrationResult
            MigrateLegacyAccountOnce()
        {
            var legacy = new List<LegacyCharacterProfile>();
            for (int slotIndex = 0;
                 slotIndex < PlayerAccountSnapshot.CharacterSlotCount;
                 slotIndex++)
            {
                FlowProfileRecord record;
                if (legacyStore.TryLoad(slotIndex, out record))
                {
                    legacy.Add(Legacy(slotIndex, record));
                }
            }
            return new SaveMigration(
                accountAuthority,
                graphFactory,
                accountStore.Save).Migrate(legacy);
        }

        private static LegacyCharacterProfile Legacy(
            int slotIndex,
            FlowProfileRecord record)
        {
            return new LegacyCharacterProfile(
                slotIndex,
                record.DisplayName,
                record.Payload.SelectedCharacterStableId,
                record.Payload.LoadoutProfileStableId,
                record.Payload.Fingerprint,
                record.Payload);
        }

        private static bool TryProject(
            CharacterInstanceSnapshot character,
            out FlowProfileRecord profile,
            out string rejectionCode)
        {
            profile = null;
            rejectionCode = string.Empty;
            SavePartSnapshot mountComponent;
            if (!character.TryGetComponent(
                    LoadoutSavePart.Definition()
                        .ComponentStableId,
                    out mountComponent))
            {
                rejectionCode =
                    "character-projection-canonical-loadout-missing";
                return false;
            }

            LoadoutSnapshot mounts;
            if (!LoadoutSavePart.Codec.TryDecode(
                    mountComponent.CanonicalPayload,
                    out mounts,
                    out rejectionCode))
            {
                rejectionCode =
                    "character-projection-canonical-loadout-invalid:"
                        + rejectionCode;
                return false;
            }

            PlayerRouteProfilePayload routePayload;
            try
            {
                routePayload = LoadoutView.Route(
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    GunMountPolicy.ResolveLayout(
                        character.ClassDefinitionStableId),
                    mounts);
                profile = new FlowProfileRecord(
                    character.DisplayName,
                    routePayload);
                return true;
            }
            catch (Exception exception)
            {
                rejectionCode = "character-projection-threw:"
                    + exception.GetType().Name;
                return false;
            }
        }

        private static bool IsOldGunInventorySave(
            PlayerAccountStoreResult loaded)
        {
            return loaded != null
                && loaded.Status
                    == PlayerAccountStoreStatus.ValidationRejected
                && string.Equals(
                    loaded.RejectionCode,
                    OldGunInventoryLoadRejection,
                    StringComparison.Ordinal);
        }

        private static bool TryDeleteOldAccountFiles(
            IAtomicSaveFilePort files,
            string activePath,
            string temporaryPath,
            string backupPath,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (files == null)
            {
                rejectionCode = "file-port-null";
                return false;
            }

            try
            {
                DeleteIfExists(files, temporaryPath);
                DeleteIfExists(files, backupPath);
                DeleteIfExists(files, activePath);
                if (files.Exists(activePath)
                    || files.Exists(temporaryPath)
                    || files.Exists(backupPath))
                {
                    rejectionCode = "account-save-files-remain";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                rejectionCode = "account-save-delete-io-failure:"
                    + exception.GetType().Name;
                return false;
            }
        }

        private static void DeleteIfExists(
            IAtomicSaveFilePort files,
            string path)
        {
            if (files.Exists(path))
            {
                files.Delete(path);
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

        private static string DescribeException(Exception exception)
        {
            if (exception == null)
            {
                return "Exception";
            }
            Exception root = exception.GetBaseException() ?? exception;
            string description = exception.GetType().Name;
            if (!ReferenceEquals(root, exception))
            {
                description += "->" + root.GetType().Name;
            }
            if (string.IsNullOrWhiteSpace(root.Message))
            {
                return description;
            }
            string message = root.Message
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (message.Length > 256)
            {
                message = message.Substring(0, 256);
            }
            return description + ":" + message;
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

        private sealed class SystemIoAtomicSaveFilePort :
            IAtomicSaveFilePort
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
