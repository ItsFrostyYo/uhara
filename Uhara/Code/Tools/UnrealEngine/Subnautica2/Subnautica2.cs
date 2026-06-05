using System;

public partial class Tools
{
    public partial class UnrealEngine
    {
        public partial class Subnautica2
        {
            private Default.Events fallbackEventsTool;
            private Default.Utilities fallbackUtilities;
            private readonly PtrResolver resolver;

            public Subnautica2()
            {
                if (!Main.ReloadProcess()) throw new Exception();

                resolver = new PtrResolver();
            }

            private Default.Events GetEventsTool()
            {
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

            private static string HiddenWatcherName(string watcherName, string suffix)
            {
                return "_SN2_" + watcherName + "_" + suffix;
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
