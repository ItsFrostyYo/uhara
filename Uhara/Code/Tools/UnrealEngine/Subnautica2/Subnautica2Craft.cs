using System;
using System.Collections.Generic;

public partial class Tools
{
    public partial class UnrealEngine
    {
        public partial class Subnautica2
        {
            private sealed class CraftRecipeWatcher
            {
                public string TargetRecipeName;
                public string LastRecipeName;
            }

            private const int BaseItemItemTypeOffset = 0x358;
            private const int DraggedItemItemTypeOffset = 0x80;
            private const int QuickSlotEntryItemTypeOffset = 0x90;
            private const int FabricatorScreenViewModelOffset = 0x5B0;
            private const int FabricatorScreenRecipeViewModelOffset = 0x5B8;
            private const int FabricatorViewModelRecipeOffset = 0xB0;
            private const int RecipeViewModelRecipeOffset = 0x80;
            private const int RecipeViewModelRelevantAssetOffset = 0x90;

            private readonly Dictionary<string, CraftRecipeWatcher> craftRecipeWatchers = new Dictionary<string, CraftRecipeWatcher>(StringComparer.OrdinalIgnoreCase);
            private bool craftHookInitialized;
            private string craftHookClassName;
            private string craftHookObjectName;
            private string craftHookFunctionName;
            private ulong craftEventCacheUpdateCounter = ulong.MaxValue;
            private bool craftEventCacheTriggered;
            private string craftEventCacheRecipeName;

            public void CraftRecipeFlag(string watcherName, string recipeName)
            {
                try
                {
                    CraftRecipeFlag(watcherName, "WBP_FabricatorScreen_C", "*", "CraftingStarted", recipeName);
                }
                catch { }
            }

            public void CraftRecipeFlag(string watcherName, string className, string objectName, string functionName, string recipeName)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(watcherName)) return;

                    CraftRecipeWatcher watcher = new CraftRecipeWatcher
                    {
                        TargetRecipeName = recipeName,
                        LastRecipeName = null
                    };

                    EnsureCraftHookInitialized(className, objectName, functionName);
                    craftRecipeWatchers[watcherName] = watcher;
                }
                catch { }
            }

            public bool CraftRecipeFlag(string watcherName)
            {
                try
                {
                    return CheckCraftRecipeFlag(watcherName);
                }
                catch { }
                return false;
            }

            public bool CheckCraftRecipeEvent(string watcherName)
            {
                try
                {
                    if (!craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return false;
                    if (!TryResolveCraftRecipeEvent(out string currentRecipeName)) return false;

                    watcher.LastRecipeName = currentRecipeName;
                    return true;
                }
                catch { }
                return false;
            }

            public bool CheckCraftRecipeFlag(string watcherName)
            {
                try
                {
                    if (!CheckCraftRecipeEvent(watcherName)) return false;
                    if (!craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return false;

                    return NamesMatch(watcher.LastRecipeName, watcher.TargetRecipeName);
                }
                catch { }
                return false;
            }

            public bool CheckCraftRecipeFlag(string watcherName, string recipeName)
            {
                try
                {
                    if (!CheckCraftRecipeEvent(watcherName)) return false;
                    if (!craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return false;

                    return NamesMatch(watcher.LastRecipeName, recipeName);
                }
                catch { }
                return false;
            }

            public string CurrentCraftRecipeName(string watcherName)
            {
                try
                {
                    if (!craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return null;

                    string currentRecipeName = ReadCraftRecipeName(watcher);
                    if (!string.IsNullOrWhiteSpace(currentRecipeName)) watcher.LastRecipeName = currentRecipeName;
                    return currentRecipeName;
                }
                catch { }
                return null;
            }

            public string LastCraftRecipeName(string watcherName)
            {
                try
                {
                    if (craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return watcher.LastRecipeName;
                }
                catch { }
                return null;
            }

            public void SetCraftRecipeTarget(string watcherName, string recipeName)
            {
                try
                {
                    if (craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) watcher.TargetRecipeName = recipeName;
                }
                catch { }
            }

            public string GetCraftRecipeTarget(string watcherName)
            {
                try
                {
                    if (craftRecipeWatchers.TryGetValue(watcherName, out CraftRecipeWatcher watcher)) return watcher.TargetRecipeName;
                }
                catch { }
                return null;
            }

            public IntPtr ItemTypeFromBaseItemActor(object baseItemActor)
            {
                try
                {
                    return ReadPointer(baseItemActor, BaseItemItemTypeOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public string ItemTypeNameFromBaseItemActor(object baseItemActor)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(ItemTypeFromBaseItemActor(baseItemActor));
                }
                catch { }
                return null;
            }

            public IntPtr ItemTypeFromDraggedItemViewModel(object draggedItemViewModel)
            {
                try
                {
                    return ReadPointer(draggedItemViewModel, DraggedItemItemTypeOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public string ItemTypeNameFromDraggedItemViewModel(object draggedItemViewModel)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(ItemTypeFromDraggedItemViewModel(draggedItemViewModel));
                }
                catch { }
                return null;
            }

            public IntPtr ItemTypeFromQuickSlotEntryViewModel(object quickSlotEntryViewModel)
            {
                try
                {
                    return ReadPointer(quickSlotEntryViewModel, QuickSlotEntryItemTypeOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public string ItemTypeNameFromQuickSlotEntryViewModel(object quickSlotEntryViewModel)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(ItemTypeFromQuickSlotEntryViewModel(quickSlotEntryViewModel));
                }
                catch { }
                return null;
            }

            public IntPtr RecipeFromRecipeViewModel(object recipeViewModel)
            {
                try
                {
                    return ReadPointer(recipeViewModel, RecipeViewModelRecipeOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public string RecipeNameFromRecipeViewModel(object recipeViewModel)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    IntPtr recipe = RecipeFromRecipeViewModel(recipeViewModel);
                    string recipeName = utilities.UObjectShortName(recipe);
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    IntPtr relevantAsset = ReadPointer(recipeViewModel, RecipeViewModelRelevantAssetOffset);
                    return utilities.UObjectShortName(relevantAsset);
                }
                catch { }
                return null;
            }

            public IntPtr FabricatorViewModelFromScreen(object fabricatorScreen)
            {
                try
                {
                    return ReadPointer(fabricatorScreen, FabricatorScreenViewModelOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public IntPtr RecipeViewModelFromFabricatorScreen(object fabricatorScreen)
            {
                try
                {
                    return ReadPointer(fabricatorScreen, FabricatorScreenRecipeViewModelOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public IntPtr RecipeFromFabricatorViewModel(object fabricatorViewModel)
            {
                try
                {
                    return ReadPointer(fabricatorViewModel, FabricatorViewModelRecipeOffset);
                }
                catch { }
                return IntPtr.Zero;
            }

            public string RecipeNameFromFabricatorViewModel(object fabricatorViewModel)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(RecipeFromFabricatorViewModel(fabricatorViewModel));
                }
                catch { }
                return null;
            }

            public string RecipeNameFromFabricatorScreen(object fabricatorScreen)
            {
                try
                {
                    string recipeName = RecipeNameFromRecipeViewModel(RecipeViewModelFromFabricatorScreen(fabricatorScreen));
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    return RecipeNameFromFabricatorViewModel(FabricatorViewModelFromScreen(fabricatorScreen));
                }
                catch { }
                return null;
            }

            private string ReadCraftRecipeName(CraftRecipeWatcher watcher)
            {
                try
                {
                    if (watcher == null) return null;
                    if (!Main.current.ContainsKey(GlobalCraftParentWatcherName)) return null;

                    object sourceObject = Main.current[GlobalCraftParentWatcherName];

                    string recipeName = RecipeNameFromFabricatorScreen(sourceObject);
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    recipeName = RecipeNameFromFabricatorViewModel(sourceObject);
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    recipeName = RecipeNameFromRecipeViewModel(sourceObject);
                    if (!string.IsNullOrWhiteSpace(recipeName)) return recipeName;

                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;
                    return utilities.UObjectShortName(sourceObject);
                }
                catch { }
                return null;
            }

            private bool TryResolveCraftRecipeEvent(out string recipeName)
            {
                try
                {
                    ulong currentUpdateCounter = Main.UpdateCounter;
                    if (craftEventCacheUpdateCounter != currentUpdateCounter)
                    {
                        craftEventCacheUpdateCounter = currentUpdateCounter;
                        craftEventCacheTriggered = false;
                        craftEventCacheRecipeName = null;

                        if (!resolver.CheckFlag(GlobalCraftFlagWatcherName))
                        {
                            recipeName = null;
                            return false;
                        }

                        string resolvedRecipeName = ReadCraftRecipeName(null);
                        if (!string.IsNullOrWhiteSpace(resolvedRecipeName))
                        {
                            craftEventCacheTriggered = true;
                            craftEventCacheRecipeName = resolvedRecipeName;
                        }
                    }

                    recipeName = craftEventCacheRecipeName;
                    return craftEventCacheTriggered;
                }
                catch { }

                recipeName = null;
                return false;
            }

            private void EnsureCraftHookInitialized(string className, string objectName, string functionName)
            {
                try
                {
                    string hookClassName = string.IsNullOrWhiteSpace(className) ? "WBP_FabricatorScreen_C" : className;
                    string hookObjectName = string.IsNullOrWhiteSpace(objectName) ? "*" : objectName;
                    string hookFunctionName = string.IsNullOrWhiteSpace(functionName) ? "CraftingStarted" : functionName;

                    if (craftHookInitialized)
                    {
                        if (string.Equals(craftHookClassName, hookClassName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(craftHookObjectName, hookObjectName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(craftHookFunctionName, hookFunctionName, StringComparison.OrdinalIgnoreCase))
                            return;

                        return;
                    }

                    Default.Events eventsTool = GetEventsTool();
                    if (eventsTool == null) return;

                    eventsTool.FunctionFlag(GlobalCraftFlagWatcherName, hookClassName, hookObjectName, hookFunctionName);
                    eventsTool.FunctionParentPtr<ulong>(GlobalCraftParentWatcherName, hookClassName, hookObjectName, hookFunctionName);

                    craftHookClassName = hookClassName;
                    craftHookObjectName = hookObjectName;
                    craftHookFunctionName = hookFunctionName;
                    craftHookInitialized = true;
                }
                catch { }
            }

            private static string GlobalCraftFlagWatcherName => HiddenWatcherName("GlobalCraft", "Flag");

            private static string GlobalCraftParentWatcherName => HiddenWatcherName("GlobalCraft", "Parent");

            private static bool NamesMatch(string left, string right)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
                    if (right.Trim() == "*") return true;
                    return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                return false;
            }
        }
    }
}
