using System;
using System.Collections.Generic;

public partial class Tools
{
    public partial class UnrealEngine
    {
        public partial class Subnautica2
        {
            private sealed class UnlockWatcher
            {
                public string TargetUnlockName;
                public string LastUnlockName;
                public string LastUnlockKind;
            }

            private sealed class UnlockEventRecord
            {
                public string Kind;
                public string Name;
            }

            private const bool ReleaseDatabankSupportEnabled = false;

            private const int RecipesListEntriesOffset = 0x80;
            private const int RecipesListActiveExtraBlueprintDataOffset = 0x178;
            private const int RecipeCategorySubCategoriesOffset = 0x78;
            private const int RecipeCategoryRecipesOffset = 0x88;
            private const int RecipeViewModelBuilderActionOffset = 0x88;
            private const int RecipeViewModelIsLockedOffset = 0xB1;
            private const int RecipeViewModelIsFirstTimeUnlockedOffset = 0xB8;

            private const int DatabankViewModelEntriesOffset = 0x68;
            private const int DatabankViewModelDatabankEntriesOffset = 0x88;
            private const int DatabankEntryViewModelEntryOffset = 0x68;
            private const int DatabankEntryViewModelIsVisibleOffset = 0x81;

            private const int PlayerStateStoryGoalContainerOffset = 0x410;
            private const int PlayerStateUnlockPlayerStateComponentOffset = 0x450;
            private const int PlayerStateEventTrackerComponentOffset = 0x5E0;
            private const int PlayerStatePawnPrivateOffset = 0x328;
            private const int PawnControllerOffset = 0x2E0;
            private const int PlayerControllerMyHUDOffset = 0x360;
            private const int WorldHUDDatabankViewModelOffset = 0x408;
            private const int WorldHUDFabricatorRecipesListViewModelOffset = 0x438;
            private const int WorldHUDPDARecipesListViewModelOffset = 0x440;
            private const int WorldHUDBuilderRecipesListViewModelOffset = 0x448;
            private const int UnlockPlayerStateComponentAllUnlockablesOffset = 0xC0;
            private const int StoryGoalContainerUnlockRecordsOffset = 0xC8;
            private const int EventTrackerReplicatedEntriesOffset = 0xE8;
            private const int EventTrackerEntriesOffset = 0x210;
            private const int EventArrayEntriesOffset = 0x118;
            private const int EventEntrySize = 0x30;
            private const int EventEntryKeyOffset = 0x0C;
            private const int EventEntryValueOffset = 0x2C;
            private const int EventKeyAssetIdOffset = 0x10;
            private const int StoryGoalUnlockRecordSize = 0x18;
            private const int StoryGoalUnlockRecordPrimaryAssetIdOffset = 0x0;
            private const int StoryGoalUnlockRecordCountOffset = 0x14;

            private readonly Dictionary<string, UnlockWatcher> unlockWatchers = new Dictionary<string, UnlockWatcher>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> seenBlueprintUnlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> seenDatabankUnlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> seenStoryUnlockKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> databankEventCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            private List<UnlockEventRecord> cachedBlueprintUnlockResults = new List<UnlockEventRecord>();
            private List<UnlockEventRecord> cachedDatabankUnlockResults = new List<UnlockEventRecord>();
            private List<UnlockEventRecord> cachedStoryUnlockResults = new List<UnlockEventRecord>();

            private ulong cachedBlueprintEventCounter;
            private ulong cachedDatabankEventCounter;
            private ulong cachedStoryEventCounter;

            private bool blueprintHookInitialized;
            private bool databankHookInitialized;
            private bool storyHookInitialized;

            public void UnlockFlag(string watcherName, string unlockName)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(watcherName)) return;

                    unlockWatchers[watcherName] = new UnlockWatcher
                    {
                        TargetUnlockName = unlockName,
                        LastUnlockName = null,
                        LastUnlockKind = null
                    };

                    EnsureBlueprintUnlockHookInitialized();
                    if (ReleaseDatabankSupportEnabled) EnsureDatabankUnlockHookInitialized();
                    EnsureStoryUnlockHookInitialized();
                }
                catch { }
            }

            public void ResetUnlockState()
            {
                try
                {
                    seenBlueprintUnlocks.Clear();
                    seenDatabankUnlocks.Clear();
                    seenStoryUnlockKeys.Clear();
                    databankEventCounts.Clear();

                    cachedBlueprintUnlockResults = new List<UnlockEventRecord>();
                    cachedDatabankUnlockResults = new List<UnlockEventRecord>();
                    cachedStoryUnlockResults = new List<UnlockEventRecord>();

                    cachedBlueprintEventCounter = 0;
                    cachedDatabankEventCounter = 0;
                    cachedStoryEventCounter = 0;

                    foreach (UnlockWatcher watcher in unlockWatchers.Values)
                    {
                        if (watcher == null) continue;
                        watcher.LastUnlockName = null;
                        watcher.LastUnlockKind = null;
                    }
                }
                catch { }
            }

            public bool UnlockFlag(string watcherName)
            {
                try
                {
                    return CheckUnlockFlag(watcherName);
                }
                catch { }
                return false;
            }

            public bool CheckUnlockFlag(string watcherName)
            {
                try
                {
                    if (!unlockWatchers.TryGetValue(watcherName, out UnlockWatcher watcher)) return false;

                    List<UnlockEventRecord> events = ReadCurrentUnlockResults();
                    if (events.Count == 0) return false;

                    foreach (UnlockEventRecord unlockEvent in events)
                    {
                        if (unlockEvent == null || string.IsNullOrWhiteSpace(unlockEvent.Name)) continue;

                        watcher.LastUnlockName = unlockEvent.Name;
                        watcher.LastUnlockKind = unlockEvent.Kind;

                        if (NamesMatch(unlockEvent.Name, watcher.TargetUnlockName)) return true;
                    }
                }
                catch { }
                return false;
            }

            public string CurrentUnlockName(string watcherName)
            {
                try
                {
                    if (!unlockWatchers.TryGetValue(watcherName, out UnlockWatcher watcher)) return null;

                    List<UnlockEventRecord> events = ReadCurrentUnlockResults();
                    if (events.Count == 0) return null;

                    UnlockEventRecord last = events[events.Count - 1];
                    watcher.LastUnlockName = last.Name;
                    watcher.LastUnlockKind = last.Kind;
                    return last.Name;
                }
                catch { }
                return null;
            }

            public string LastUnlockName(string watcherName)
            {
                try
                {
                    if (unlockWatchers.TryGetValue(watcherName, out UnlockWatcher watcher)) return watcher.LastUnlockName;
                }
                catch { }
                return null;
            }

            public string LastUnlockKind(string watcherName)
            {
                try
                {
                    if (unlockWatchers.TryGetValue(watcherName, out UnlockWatcher watcher)) return watcher.LastUnlockKind;
                }
                catch { }
                return null;
            }

            public bool BlueprintUnlockEvent()
            {
                try
                {
                    return GetBlueprintUnlockResults().Count > 0;
                }
                catch { }
                return false;
            }

            public bool DatabankUnlockEvent()
            {
                try
                {
                    if (!ReleaseDatabankSupportEnabled) return false;
                    return GetDatabankUnlockResults().Count > 0;
                }
                catch { }
                return false;
            }

            public bool StoryUnlockEvent()
            {
                try
                {
                    return GetStoryUnlockResults().Count > 0;
                }
                catch { }
                return false;
            }

            public string CurrentBlueprintUnlockNames()
            {
                try
                {
                    return JoinNames(ProjectNames(GetBlueprintUnlockResults()));
                }
                catch { }
                return null;
            }

            public string CurrentDatabankUnlockNames()
            {
                try
                {
                    if (!ReleaseDatabankSupportEnabled) return null;
                    return JoinNames(ProjectNames(GetDatabankUnlockResults()));
                }
                catch { }
                return null;
            }

            public string CurrentStoryUnlockNames()
            {
                try
                {
                    return JoinNames(ProjectNames(GetStoryUnlockResults()));
                }
                catch { }
                return null;
            }

            private List<UnlockEventRecord> ReadCurrentUnlockResults()
            {
                List<UnlockEventRecord> result = new List<UnlockEventRecord>();

                try
                {
                    AppendRange(result, GetBlueprintUnlockResults());
                    if (ReleaseDatabankSupportEnabled) AppendRange(result, GetDatabankUnlockResults());
                    AppendRange(result, GetStoryUnlockResults());
                }
                catch { }

                return result;
            }

            private List<UnlockEventRecord> GetBlueprintUnlockResults()
            {
                try
                {
                    bool blueprintFlagRaised = resolver.CheckFlag(GlobalBlueprintUnlockFlagWatcherName);
                    bool storyFlagRaised = resolver.CheckFlag(GlobalStoryUnlockFlagWatcherName);
                    if (!blueprintFlagRaised && !storyFlagRaised) return new List<UnlockEventRecord>();

                    ulong eventCounter = blueprintFlagRaised
                        ? ReadWatcherCounter(GlobalBlueprintUnlockFlagWatcherName)
                        : ReadWatcherCounter(GlobalStoryUnlockFlagWatcherName);
                    if (eventCounter == 0) return new List<UnlockEventRecord>();

                    if (eventCounter != cachedBlueprintEventCounter)
                    {
                        cachedBlueprintEventCounter = eventCounter;
                        cachedBlueprintUnlockResults = ResolveBlueprintUnlockResults();
                    }

                    return cachedBlueprintUnlockResults ?? new List<UnlockEventRecord>();
                }
                catch { }
                return new List<UnlockEventRecord>();
            }

            private List<UnlockEventRecord> GetDatabankUnlockResults()
            {
                try
                {
                    if (!ReleaseDatabankSupportEnabled) return new List<UnlockEventRecord>();

                    bool databankFlagRaised = resolver.CheckFlag(GlobalDatabankUnlockFlagWatcherName);
                    bool databankScanFlagRaised = resolver.CheckFlag(GlobalDatabankScanCompleteFlagWatcherName);
                    bool storyFlagRaised = resolver.CheckFlag(GlobalStoryUnlockFlagWatcherName);
                    if (!databankFlagRaised && !databankScanFlagRaised && !storyFlagRaised) return new List<UnlockEventRecord>();

                    ulong eventCounter = ReadWatcherCounter(GlobalDatabankUnlockFlagWatcherName);
                    ulong scanEventCounter = ReadWatcherCounter(GlobalDatabankScanCompleteFlagWatcherName);
                    ulong storyEventCounter = ReadWatcherCounter(GlobalStoryUnlockFlagWatcherName);
                    if (scanEventCounter > eventCounter) eventCounter = scanEventCounter;
                    if (storyEventCounter > eventCounter) eventCounter = storyEventCounter;
                    if (eventCounter == 0) return new List<UnlockEventRecord>();

                    if (eventCounter != cachedDatabankEventCounter)
                    {
                        cachedDatabankEventCounter = eventCounter;
                        cachedDatabankUnlockResults = ResolveDatabankUnlockResults();
                    }

                    return cachedDatabankUnlockResults ?? new List<UnlockEventRecord>();
                }
                catch { }
                return new List<UnlockEventRecord>();
            }

            private List<UnlockEventRecord> GetStoryUnlockResults()
            {
                try
                {
                    if (!resolver.CheckFlag(GlobalStoryUnlockFlagWatcherName)) return new List<UnlockEventRecord>();

                    ulong eventCounter = ReadWatcherCounter(GlobalStoryUnlockFlagWatcherName);
                    if (eventCounter == 0) return new List<UnlockEventRecord>();

                    if (eventCounter != cachedStoryEventCounter)
                    {
                        cachedStoryEventCounter = eventCounter;
                        cachedStoryUnlockResults = ResolveStoryUnlockResults();
                    }

                    return cachedStoryUnlockResults ?? new List<UnlockEventRecord>();
                }
                catch { }
                return new List<UnlockEventRecord>();
            }

            private List<UnlockEventRecord> ResolveBlueprintUnlockResults()
            {
                List<UnlockEventRecord> result = new List<UnlockEventRecord>();

                try
                {
                    HashSet<string> unlockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    HashSet<string> firstTimeUnlockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (object sourceObject in ResolveBlueprintSourceObjects())
                    {
                        if (sourceObject == null) continue;

                        foreach (IntPtr categoryViewModel in ReadUnrealPointerArray(sourceObject, RecipesListEntriesOffset))
                        {
                            CollectBlueprintUnlocksFromCategory(categoryViewModel, unlockedNames, firstTimeUnlockedNames);
                        }

                        foreach (IntPtr extraBlueprint in ReadUnrealPointerArray(sourceObject, RecipesListActiveExtraBlueprintDataOffset))
                        {
                            string extraName = ResolveUnlockAssetName(extraBlueprint);
                            if (string.IsNullOrWhiteSpace(extraName)) continue;
                            unlockedNames.Add(extraName);
                        }
                    }

                    HashSet<string> newUnlocks = firstTimeUnlockedNames.Count > 0
                        ? firstTimeUnlockedNames
                        : ExceptBySeen(unlockedNames, seenBlueprintUnlocks);

                    foreach (string unlockName in newUnlocks)
                    {
                        if (string.IsNullOrWhiteSpace(unlockName)) continue;
                        if (seenBlueprintUnlocks.Contains(unlockName)) continue;

                        seenBlueprintUnlocks.Add(unlockName);
                        result.Add(new UnlockEventRecord { Kind = "Blueprint", Name = unlockName });
                    }

                    foreach (string unlockName in unlockedNames) seenBlueprintUnlocks.Add(unlockName);
                }
                catch { }

                return result;
            }

            private IEnumerable<object> ResolveBlueprintSourceObjects()
            {
                List<object> result = new List<object>();

                try
                {
                    if (Main.current.ContainsKey(GlobalBlueprintUnlockParentWatcherName))
                    {
                        object directViewModel = Main.current[GlobalBlueprintUnlockParentWatcherName];
                        if (ToAddress(directViewModel) != 0) result.Add(directViewModel);
                    }
                }
                catch { }

                try
                {
                    IntPtr hud = ResolveWorldHudFromStoryUnlockComponent();
                    if (hud != IntPtr.Zero)
                    {
                        IntPtr fabricatorRecipesListViewModel = ReadPointer(hud, WorldHUDFabricatorRecipesListViewModelOffset);
                        if (fabricatorRecipesListViewModel != IntPtr.Zero) result.Add(fabricatorRecipesListViewModel);

                        IntPtr pdaRecipesListViewModel = ReadPointer(hud, WorldHUDPDARecipesListViewModelOffset);
                        if (pdaRecipesListViewModel != IntPtr.Zero) result.Add(pdaRecipesListViewModel);

                        IntPtr builderRecipesListViewModel = ReadPointer(hud, WorldHUDBuilderRecipesListViewModelOffset);
                        if (builderRecipesListViewModel != IntPtr.Zero) result.Add(builderRecipesListViewModel);
                    }
                }
                catch { }

                return result;
            }

            private void CollectBlueprintUnlocksFromCategory(object categoryViewModel, HashSet<string> unlockedNames, HashSet<string> firstTimeUnlockedNames)
            {
                try
                {
                    foreach (IntPtr recipeViewModel in ReadUnrealPointerArray(categoryViewModel, RecipeCategoryRecipesOffset))
                    {
                        if (recipeViewModel == IntPtr.Zero) continue;
                        if (ReadBool(recipeViewModel, RecipeViewModelIsLockedOffset)) continue;

                        string unlockName = ResolveRecipeViewModelUnlockName(recipeViewModel);
                        if (string.IsNullOrWhiteSpace(unlockName)) continue;

                        unlockedNames.Add(unlockName);
                        if (ReadBool(recipeViewModel, RecipeViewModelIsFirstTimeUnlockedOffset)) firstTimeUnlockedNames.Add(unlockName);
                    }

                    foreach (IntPtr subCategoryViewModel in ReadUnrealPointerArray(categoryViewModel, RecipeCategorySubCategoriesOffset))
                    {
                        if (subCategoryViewModel == IntPtr.Zero) continue;
                        CollectBlueprintUnlocksFromCategory(subCategoryViewModel, unlockedNames, firstTimeUnlockedNames);
                    }
                }
                catch { }
            }

            private string ResolveRecipeViewModelUnlockName(object recipeViewModel)
            {
                try
                {
                    string relevantAssetName = ResolveUnlockAssetName(ReadPointer(recipeViewModel, RecipeViewModelRelevantAssetOffset));
                    if (!string.IsNullOrWhiteSpace(relevantAssetName)) return relevantAssetName;

                    string recipeName = ResolveUnlockAssetName(ReadPointer(recipeViewModel, RecipeViewModelRecipeOffset));
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    return ResolveUnlockAssetName(ReadPointer(recipeViewModel, RecipeViewModelBuilderActionOffset));
                }
                catch { }
                return null;
            }

            private List<UnlockEventRecord> ResolveDatabankUnlockResults()
            {
                List<UnlockEventRecord> result = new List<UnlockEventRecord>();

                try
                {
                    result = ResolveDatabankUnlockResultsFromEventTracker();
                    if (result.Count > 0) return result;

                    object sourceObject = ResolveDatabankSourceObject();
                    if (sourceObject == null) return result;
                    HashSet<string> unlockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string unlockName in ResolveUnlockComponentAssetNames("DatabankEntry", null))
                    {
                        if (string.IsNullOrWhiteSpace(unlockName)) continue;
                        unlockedNames.Add(unlockName);
                    }

                    foreach (IntPtr databankEntryViewModel in ReadUnrealPointerArray(sourceObject, DatabankViewModelEntriesOffset))
                    {
                        if (databankEntryViewModel == IntPtr.Zero) continue;
                        if (!ReadBool(databankEntryViewModel, DatabankEntryViewModelIsVisibleOffset)) continue;

                        IntPtr entry = ReadPointer(databankEntryViewModel, DatabankEntryViewModelEntryOffset);
                        string entryName = ResolveUnlockAssetName(entry);
                        if (string.IsNullOrWhiteSpace(entryName)) continue;
                        unlockedNames.Add(entryName);
                    }

                    if (unlockedNames.Count == 0)
                    {
                        foreach (IntPtr entry in ReadUnrealPointerArray(sourceObject, DatabankViewModelDatabankEntriesOffset))
                        {
                            string entryName = ResolveUnlockAssetName(entry);
                            if (string.IsNullOrWhiteSpace(entryName)) continue;
                            unlockedNames.Add(entryName);
                        }

                        if (seenDatabankUnlocks.Count == 0)
                        {
                            unlockedNames.Clear();
                        }
                    }

                    foreach (string unlockName in unlockedNames)
                    {
                        if (seenDatabankUnlocks.Contains(unlockName)) continue;

                        seenDatabankUnlocks.Add(unlockName);
                        result.Add(new UnlockEventRecord { Kind = "Databank", Name = unlockName });
                    }
                }
                catch { }

                return result;
            }

            private List<UnlockEventRecord> ResolveDatabankUnlockResultsFromEventTracker()
            {
                List<UnlockEventRecord> result = new List<UnlockEventRecord>();

                try
                {
                    HashSet<string> databankCandidates = ResolveDatabankCandidateNames();
                    if (databankCandidates.Count == 0) return result;

                    IntPtr eventTrackerComponent = ResolveEventTrackerComponent();
                    if (eventTrackerComponent == IntPtr.Zero) return result;

                    CollectDatabankUnlocksFromEventArray(eventTrackerComponent, EventTrackerEntriesOffset, databankCandidates, result);
                    if (result.Count == 0)
                    {
                        CollectDatabankUnlocksFromEventArray(eventTrackerComponent, EventTrackerReplicatedEntriesOffset, databankCandidates, result);
                    }
                }
                catch { }

                return result;
            }

            private HashSet<string> ResolveDatabankCandidateNames()
            {
                HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    foreach (string unlockName in ResolveUnlockComponentAssetNames("DatabankEntry", null))
                    {
                        if (string.IsNullOrWhiteSpace(unlockName)) continue;
                        result.Add(unlockName);
                    }
                }
                catch { }

                return result;
            }

            private void CollectDatabankUnlocksFromEventArray(IntPtr eventTrackerComponent, int eventArrayOffset, HashSet<string> databankCandidates, List<UnlockEventRecord> result)
            {
                try
                {
                    ulong eventTrackerAddress = ToAddress(eventTrackerComponent);
                    if (eventTrackerAddress == 0) return;

                    ulong eventArrayAddress = eventTrackerAddress + (ulong)eventArrayOffset;
                    ulong entriesDataAddress = TMemory.ReadMemory<ulong>(Main.ProcessInstance, eventArrayAddress + (ulong)EventArrayEntriesOffset);
                    int entryCount = TMemory.ReadMemory<int>(Main.ProcessInstance, eventArrayAddress + (ulong)EventArrayEntriesOffset + 0x8);
                    if (entriesDataAddress == 0 || entryCount <= 0 || entryCount > 4096) return;

                    for (int i = 0; i < entryCount; i++)
                    {
                        ulong entryAddress = entriesDataAddress + ((ulong)i * EventEntrySize);
                        string assetName = ReadPrimaryAssetName((IntPtr)entryAddress, EventEntryKeyOffset + EventKeyAssetIdOffset);
                        if (string.IsNullOrWhiteSpace(assetName)) continue;
                        if (!databankCandidates.Contains(assetName)) continue;

                        int currentCount = ReadInt32((IntPtr)entryAddress, EventEntryValueOffset);
                        databankEventCounts.TryGetValue(assetName, out int previousCount);
                        databankEventCounts[assetName] = currentCount;

                        if (currentCount <= 0 || currentCount <= previousCount) continue;
                        if (seenDatabankUnlocks.Contains(assetName)) continue;

                        seenDatabankUnlocks.Add(assetName);
                        result.Add(new UnlockEventRecord { Kind = "Databank", Name = assetName });
                    }
                }
                catch { }
            }

            private object ResolveDatabankSourceObject()
            {
                try
                {
                    if (Main.current.ContainsKey(GlobalDatabankUnlockParentWatcherName))
                    {
                        object databankViewModel = Main.current[GlobalDatabankUnlockParentWatcherName];
                        if (ToAddress(databankViewModel) != 0) return databankViewModel;
                    }
                }
                catch { }

                try
                {
                    IntPtr hud = ResolveWorldHudFromStoryUnlockComponent();
                    if (hud == IntPtr.Zero) return null;

                    IntPtr databankViewModel = ReadPointer(hud, WorldHUDDatabankViewModelOffset);
                    if (databankViewModel == IntPtr.Zero) return null;

                    return databankViewModel;
                }
                catch { }

                return null;
            }

            private IntPtr ResolveWorldHudFromStoryUnlockComponent()
            {
                try
                {
                    IntPtr playerState = ResolvePlayerState();
                    if (playerState == IntPtr.Zero) return IntPtr.Zero;

                    IntPtr pawn = ReadPointer(playerState, PlayerStatePawnPrivateOffset);
                    if (pawn == IntPtr.Zero) return IntPtr.Zero;

                    IntPtr controller = ReadPointer(pawn, PawnControllerOffset);
                    if (controller == IntPtr.Zero) return IntPtr.Zero;

                    return ReadPointer(controller, PlayerControllerMyHUDOffset);
                }
                catch { }

                return IntPtr.Zero;
            }

            private IEnumerable<string> ResolveUnlockComponentAssetNames(string requiredClassSubstring, HashSet<string> seenNames)
            {
                List<string> result = new List<string>();

                try
                {
                    IntPtr unlockPlayerStateComponent = ResolveUnlockPlayerStateComponent();
                    if (unlockPlayerStateComponent == IntPtr.Zero) return result;

                    foreach (IntPtr unlockableAsset in ReadScriptInterfaceObjectArray(unlockPlayerStateComponent, UnlockPlayerStateComponentAllUnlockablesOffset))
                    {
                        if (unlockableAsset == IntPtr.Zero) continue;

                        string className = ResolveUnlockAssetClassName(unlockableAsset);
                        if (string.IsNullOrWhiteSpace(className) || className.IndexOf(requiredClassSubstring, StringComparison.OrdinalIgnoreCase) < 0) continue;

                        string unlockName = ResolveUnlockAssetName(unlockableAsset);
                        if (string.IsNullOrWhiteSpace(unlockName)) continue;
                        if (seenNames != null && seenNames.Contains(unlockName)) continue;

                        seenNames?.Add(unlockName);
                        result.Add(unlockName);
                    }
                }
                catch { }

                return result;
            }

            private IntPtr ResolveUnlockPlayerStateComponent()
            {
                try
                {
                    IntPtr playerState = ResolvePlayerState();
                    if (playerState == IntPtr.Zero) return IntPtr.Zero;

                    return ReadPointer(playerState, PlayerStateUnlockPlayerStateComponentOffset);
                }
                catch { }

                return IntPtr.Zero;
            }

            private IntPtr ResolveEventTrackerComponent()
            {
                try
                {
                    IntPtr playerState = ResolvePlayerState();
                    if (playerState == IntPtr.Zero) return IntPtr.Zero;

                    return ReadPointer(playerState, PlayerStateEventTrackerComponentOffset);
                }
                catch { }

                return IntPtr.Zero;
            }

            private IntPtr ResolvePlayerState()
            {
                try
                {
                    IntPtr playerState = ResolvePlayerStateFromWatcher(GlobalDatabankScanCompleteParentWatcherName);
                    if (playerState != IntPtr.Zero) return playerState;

                    playerState = ResolvePlayerStateFromWatcher(GlobalStoryUnlockParentWatcherName);
                    if (playerState != IntPtr.Zero) return playerState;

                    playerState = ResolvePlayerStateFromWatcher(GlobalDatabankUnlockParentWatcherName);
                    if (playerState != IntPtr.Zero) return playerState;

                    playerState = ResolvePlayerStateFromWatcher(GlobalBlueprintUnlockParentWatcherName);
                    if (playerState != IntPtr.Zero) return playerState;
                }
                catch { }

                return IntPtr.Zero;
            }

            private IntPtr ResolvePlayerStateFromWatcher(string watcherName)
            {
                try
                {
                    if (!Main.current.ContainsKey(watcherName)) return IntPtr.Zero;
                    return ResolvePlayerStateFromSourceObject(Main.current[watcherName]);
                }
                catch { }

                return IntPtr.Zero;
            }

            private IntPtr ResolvePlayerStateFromSourceObject(object sourceObject)
            {
                try
                {
                    if (sourceObject == null) return IntPtr.Zero;

                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return IntPtr.Zero;

                    return utilities.UObjectOuter(sourceObject);
                }
                catch { }

                return IntPtr.Zero;
            }

            private IntPtr[] ReadScriptInterfaceObjectArray(object value, int offset)
            {
                try
                {
                    ulong address = ToAddress(value);
                    if (address == 0) return Array.Empty<IntPtr>();

                    ulong dataAddress = TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + (ulong)offset);
                    int count = TMemory.ReadMemory<int>(Main.ProcessInstance, address + (ulong)offset + 0x8);
                    if (dataAddress == 0 || count <= 0 || count > 4096) return Array.Empty<IntPtr>();

                    List<IntPtr> result = new List<IntPtr>(count);
                    for (int i = 0; i < count; i++)
                    {
                        ulong objectPtr = TMemory.ReadMemory<ulong>(Main.ProcessInstance, dataAddress + ((ulong)i * 0x10));
                        if (objectPtr != 0) result.Add((IntPtr)objectPtr);
                    }

                    return result.ToArray();
                }
                catch { }

                return Array.Empty<IntPtr>();
            }

            private List<UnlockEventRecord> ResolveStoryUnlockResults()
            {
                List<UnlockEventRecord> result = new List<UnlockEventRecord>();

                try
                {
                    if (!Main.current.ContainsKey(GlobalStoryUnlockParentWatcherName)) return result;

                    object sourceObject = Main.current[GlobalStoryUnlockParentWatcherName];
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return result;

                    IntPtr playerState = utilities.UObjectOuter(sourceObject);
                    if (playerState == IntPtr.Zero) return result;

                    IntPtr storyGoalContainer = ReadPointer(playerState, PlayerStateStoryGoalContainerOffset);
                    if (storyGoalContainer == IntPtr.Zero) return result;

                    ulong containerAddress = ToAddress(storyGoalContainer);
                    ulong recordsDataAddress = TMemory.ReadMemory<ulong>(Main.ProcessInstance, containerAddress + StoryGoalContainerUnlockRecordsOffset);
                    int recordCount = TMemory.ReadMemory<int>(Main.ProcessInstance, containerAddress + StoryGoalContainerUnlockRecordsOffset + 0x8);
                    if (recordsDataAddress == 0 || recordCount <= 0 || recordCount > 4096) return result;

                    List<UnlockEventRecord> candidates = new List<UnlockEventRecord>();
                    for (int i = 0; i < recordCount; i++)
                    {
                        ulong recordAddress = recordsDataAddress + ((ulong)i * StoryGoalUnlockRecordSize);
                        string storyGoalName = ReadPrimaryAssetName((IntPtr)recordAddress, StoryGoalUnlockRecordPrimaryAssetIdOffset);
                        if (string.IsNullOrWhiteSpace(storyGoalName)) continue;

                        int count = ReadInt32((IntPtr)recordAddress, StoryGoalUnlockRecordCountOffset);
                        string storyGoalKey = storyGoalName + "#" + count.ToString();

                        candidates.Add(new UnlockEventRecord { Kind = "Story", Name = storyGoalName });
                        if (seenStoryUnlockKeys.Contains(storyGoalKey)) continue;

                        seenStoryUnlockKeys.Add(storyGoalKey);
                        result.Add(new UnlockEventRecord { Kind = "Story", Name = storyGoalName });
                    }

                    if (result.Count == 0 && candidates.Count > 0 && seenStoryUnlockKeys.Count == candidates.Count)
                    {
                        UnlockEventRecord lastCandidate = candidates[candidates.Count - 1];
                        if (lastCandidate != null && !string.IsNullOrWhiteSpace(lastCandidate.Name))
                        {
                            result.Add(lastCandidate);
                        }
                    }
                }
                catch { }

                return result;
            }

            private string ResolveUnlockAssetName(object asset)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(asset);
                }
                catch { }
                return null;
            }

            private string ResolveUnlockAssetClassName(object asset)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectClassName(asset);
                }
                catch { }
                return null;
            }

            private void EnsureBlueprintUnlockHookInitialized()
            {
                try
                {
                    if (blueprintHookInitialized) return;

                    Default.Events eventsTool = GetEventsTool();
                    if (eventsTool == null) return;

                    eventsTool.FunctionFlag(GlobalBlueprintUnlockFlagWatcherName, "SN2RecipesListViewModel", "*", "OnUnlockableUnlocked");
                    eventsTool.FunctionParentPtr<ulong>(GlobalBlueprintUnlockParentWatcherName, "SN2RecipesListViewModel", "*", "OnUnlockableUnlocked");
                    blueprintHookInitialized = true;
                }
                catch { }
            }

            private void EnsureDatabankUnlockHookInitialized()
            {
                try
                {
                    if (!ReleaseDatabankSupportEnabled) return;
                    if (databankHookInitialized) return;

                    Default.Events eventsTool = GetEventsTool();
                    if (eventsTool == null) return;

                    eventsTool.FunctionFlag(GlobalDatabankUnlockFlagWatcherName, "SN2DatabankViewModel", "*", "OnStoryGoalUnlocked");
                    eventsTool.FunctionParentPtr<ulong>(GlobalDatabankUnlockParentWatcherName, "SN2DatabankViewModel", "*", "OnStoryGoalUnlocked");
                    eventsTool.FunctionFlag(GlobalDatabankScanCompleteFlagWatcherName, "UWEScannedActorsComponent", "*", "OnScanCompletedEventFired");
                    eventsTool.FunctionParentPtr<ulong>(GlobalDatabankScanCompleteParentWatcherName, "UWEScannedActorsComponent", "*", "OnScanCompletedEventFired");
                    databankHookInitialized = true;
                }
                catch { }
            }

            private void EnsureStoryUnlockHookInitialized()
            {
                try
                {
                    if (storyHookInitialized) return;

                    Default.Events eventsTool = GetEventsTool();
                    if (eventsTool == null) return;

                    eventsTool.FunctionFlag(GlobalStoryUnlockFlagWatcherName, "SN2UnlockPlayerStateComponent", "*", "OnAnyEventTrackerEventFired");
                    eventsTool.FunctionParentPtr<ulong>(GlobalStoryUnlockParentWatcherName, "SN2UnlockPlayerStateComponent", "*", "OnAnyEventTrackerEventFired");
                    storyHookInitialized = true;
                }
                catch { }
            }

            private static ulong ReadWatcherCounter(string watcherName)
            {
                try
                {
                    if (!Main.current.ContainsKey(watcherName)) return 0;
                    return Convert.ToUInt64(Main.current[watcherName]);
                }
                catch { }
                return 0;
            }

            private static HashSet<string> ExceptBySeen(HashSet<string> source, HashSet<string> seen)
            {
                HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    if (source == null) return result;
                    foreach (string item in source)
                    {
                        if (string.IsNullOrWhiteSpace(item)) continue;
                        if (seen != null && seen.Contains(item)) continue;
                        result.Add(item);
                    }
                }
                catch { }

                return result;
            }

            private static void AppendRange(List<UnlockEventRecord> destination, List<UnlockEventRecord> source)
            {
                try
                {
                    if (destination == null || source == null || source.Count == 0) return;
                    foreach (UnlockEventRecord item in source)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.Name)) continue;
                        destination.Add(item);
                    }
                }
                catch { }
            }

            private static IEnumerable<string> ProjectNames(List<UnlockEventRecord> source)
            {
                List<string> names = new List<string>();

                try
                {
                    if (source == null) return names;
                    foreach (UnlockEventRecord item in source)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.Name)) continue;
                        names.Add(item.Name);
                    }
                }
                catch { }

                return names;
            }

            private static string GlobalBlueprintUnlockFlagWatcherName => HiddenWatcherName("GlobalBlueprintUnlock", "Flag");
            private static string GlobalBlueprintUnlockParentWatcherName => HiddenWatcherName("GlobalBlueprintUnlock", "Parent");
            private static string GlobalDatabankUnlockFlagWatcherName => HiddenWatcherName("GlobalDatabankUnlock", "Flag");
            private static string GlobalDatabankUnlockParentWatcherName => HiddenWatcherName("GlobalDatabankUnlock", "Parent");
            private static string GlobalDatabankScanCompleteFlagWatcherName => HiddenWatcherName("GlobalDatabankScanComplete", "Flag");
            private static string GlobalDatabankScanCompleteParentWatcherName => HiddenWatcherName("GlobalDatabankScanComplete", "Parent");
            private static string GlobalStoryUnlockFlagWatcherName => HiddenWatcherName("GlobalStoryUnlock", "Flag");
            private static string GlobalStoryUnlockParentWatcherName => HiddenWatcherName("GlobalStoryUnlock", "Parent");
        }
    }
}
