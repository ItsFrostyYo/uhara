using System;
using System.Collections.Generic;

public partial class Tools
{
    public partial class UnrealEngine
    {
        public partial class Subnautica2
        {
            private Default.Events fallbackEventsTool;
            private Default.Utilities fallbackUtilities;
            private readonly PtrResolver resolver;
            private string attachedProcessToken;

            public Subnautica2()
            {
                if (!Main.ReloadProcess()) throw new Exception();

                resolver = new PtrResolver();
                attachedProcessToken = SafeGetToken();
            }

            private Default.Events GetEventsTool()
            {
                EnsureProcessContext();

                try
                {
                    object existingTool = Main.Vars?.Events;
                    if (existingTool is Default.Events sharedEvents) return sharedEvents;
                }
                catch { }

                try
                {
                    if (fallbackEventsTool == null) fallbackEventsTool = new Default.Events();
                    return fallbackEventsTool;
                }
                catch { }
                return null;
            }

            private Default.Utilities GetUtilitiesTool()
            {
                EnsureProcessContext();

                try
                {
                    object existingTool = Main.Vars?.Utils;
                    if (existingTool is Default.Utilities sharedUtilities) return sharedUtilities;
                }
                catch { }

                try
                {
                    if (fallbackUtilities == null) fallbackUtilities = new Default.Utilities();
                    return fallbackUtilities;
                }
                catch { }
                return null;
            }

            private static IntPtr ReadPointer(object value, int offset)
            {
                ulong address = ToAddress(value);
                if (address == 0) return IntPtr.Zero;

                try
                {
                    return (IntPtr)TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + (ulong)offset);
                }
                catch { }
                return IntPtr.Zero;
            }

            private static int ReadInt32(object value, int offset)
            {
                ulong address = ToAddress(value);
                if (address == 0) return 0;

                try
                {
                    return TMemory.ReadMemory<int>(Main.ProcessInstance, address + (ulong)offset);
                }
                catch { }
                return 0;
            }

            private static bool ReadBool(object value, int offset)
            {
                ulong address = ToAddress(value);
                if (address == 0) return false;

                try
                {
                    return TMemory.ReadMemory<byte>(Main.ProcessInstance, address + (ulong)offset) != 0;
                }
                catch { }
                return false;
            }

            private IntPtr[] ReadUnrealPointerArray(object value, int offset)
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
                        ulong item = TMemory.ReadMemory<ulong>(Main.ProcessInstance, dataAddress + ((ulong)i * 0x8));
                        if (item != 0) result.Add((IntPtr)item);
                    }

                    return result.ToArray();
                }
                catch { }
                return Array.Empty<IntPtr>();
            }

            private string ReadPrimaryAssetName(object value, int offset)
            {
                try
                {
                    Default.Utilities utilities = GetUtilitiesTool();
                    if (utilities == null) return null;

                    ulong address = ToAddress(value);
                    if (address == 0) return null;

                    ulong primaryAssetName = TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + (ulong)offset + 0x8);
                    return utilities.FNameToShortString(primaryAssetName);
                }
                catch { }
                return null;
            }

            private string JoinNames(IEnumerable<string> names)
            {
                try
                {
                    if (names == null) return null;

                    List<string> clean = new List<string>();
                    foreach (string name in names)
                    {
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        clean.Add(name.Trim());
                    }

                    if (clean.Count == 0) return null;
                    return string.Join("|", clean);
                }
                catch { }
                return null;
            }

            private static string HiddenWatcherName(string watcherName, string suffix)
            {
                return "_SN2_" + watcherName + "_" + suffix;
            }

            private void EnsureProcessContext()
            {
                try
                {
                    if (!Main.ReloadProcess()) return;

                    string currentProcessToken = SafeGetToken();
                    if (string.IsNullOrWhiteSpace(currentProcessToken)) return;

                    if (string.Equals(attachedProcessToken, currentProcessToken, StringComparison.Ordinal)) return;

                    attachedProcessToken = currentProcessToken;
                    ResetProcessScopedState();
                }
                catch { }
            }

            private void ResetProcessScopedState()
            {
                try
                {
                    fallbackEventsTool = null;
                    fallbackUtilities = null;

                    craftHookInitialized = false;
                    craftHookClassName = null;
                    craftHookObjectName = null;
                    craftHookFunctionName = null;
                    foreach (CraftRecipeWatcher watcher in craftRecipeWatchers.Values)
                    {
                        if (watcher == null) continue;
                        watcher.LastRecipeName = null;
                    }

                    blueprintHookInitialized = false;
                    databankHookInitialized = false;
                    storyHookInitialized = false;
                    ResetUnlockState();
                }
                catch { }
            }

            private static string SafeGetToken()
            {
                try
                {
                    return Main.ProcessInstance == null ? null : TProcess.GetToken(Main.ProcessInstance);
                }
                catch { }
                return null;
            }

            private static ulong ToAddress(object value)
            {
                try
                {
                    if (value == null) return 0;
                    if (value is IntPtr intPtr) return (ulong)intPtr.ToInt64();
                    if (value is UIntPtr uintPtr) return uintPtr.ToUInt64();
                    if (value is ulong ulongValue) return ulongValue;
                    if (value is long longValue) return (ulong)longValue;
                    if (value is uint uintValue) return uintValue;
                    if (value is int intValue) return (ulong)intValue;
                }
                catch { }
                return 0;
            }
        }
    }
}
