using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class Tools
{
	public partial class UnrealEngine
	{
		public partial class Default
		{
			public partial class Utilities
			{
				internal static string DebugClass = "Utilities";
				internal static string ToolUniqueID = "UCyEljVfhjUoJhDU";
				private static readonly object initLock = new object();
				private static string initializedProcessToken;

				private DataRetriever dataRetriever;
				private TextReader textReader;
				private FpsLocker fpsLocker;

                #region PUBLIC_API
				public void ExpandScanUtilitySignatures(string name, string signature)
				{
					try
					{
						if (ScanUtility.ExpandSignatures.ContainsKey(name)) ScanUtility.ExpandSignatures[name].Add(signature);
						else ScanUtility.ExpandSignatures[name] = new List<string> { signature };
                    }
					catch { }
                }

				public void SetFpsLimit(double fps)
				{
					try
					{
						fpsLocker.SetFpsLimit(fps);
					}
					catch { }
				}

                public string FNameToStringLegacy(object fName)
                {
                    try
                    {
                        return textReader.FNameToStringLegacy(fName);
                    }
                    catch { }
                    return null;
                }

                public string FNameToShortStringLegacy(object fName)
                {
                    try
                    {
                        return textReader.FNameToShortStringLegacy(fName);
                    }
                    catch { }
                    return null;
                }

                public string FNameToShortStringLegacy2(object fName)
                {
                    try
                    {
                        return textReader.FNameToShortStringLegacy2(fName);
                    }
                    catch { }
                    return null;
                }

                public string FNameToString(object fName)
				{
					try
					{
						return textReader.FNameToString(fName);
					}
					catch { }
					return null;
				}

                public string FNameToShortString(object fName)
                {
                    try
                    {
                        return textReader.FNameToShortString(fName);
                    }
                    catch { }
                    return null;
                }

                public string FNameToShortString2(object fName)
                {
                    try
                    {
                        return textReader.FNameToShortString2(fName);
                    }
                    catch { }
                    return null;
                }

                public IntPtr UObjectClass(object uObject)
                {
                    try
                    {
                        ulong address = ToAddress(uObject);
                        if (address == 0) return IntPtr.Zero;

                        return (IntPtr)TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + 0x10);
                    }
                    catch { }
                    return IntPtr.Zero;
                }

                public IntPtr UObjectOuter(object uObject)
                {
                    try
                    {
                        ulong address = ToAddress(uObject);
                        if (address == 0) return IntPtr.Zero;

                        return (IntPtr)TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + 0x20);
                    }
                    catch { }
                    return IntPtr.Zero;
                }

                public string UObjectName(object uObject)
                {
                    try
                    {
                        ulong address = ToAddress(uObject);
                        if (address == 0) return null;

                        ulong fName = TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + 0x18);
                        return FNameToString(fName);
                    }
                    catch { }
                    return null;
                }

                public string UObjectShortName(object uObject)
                {
                    try
                    {
                        ulong address = ToAddress(uObject);
                        if (address == 0) return null;

                        ulong fName = TMemory.ReadMemory<ulong>(Main.ProcessInstance, address + 0x18);
                        return FNameToShortString(fName);
                    }
                    catch { }
                    return null;
                }

                public string UObjectClassName(object uObject)
                {
                    try
                    {
                        IntPtr classPtr = UObjectClass(uObject);
                        if (classPtr == IntPtr.Zero) return null;

                        return UObjectShortName(classPtr);
                    }
                    catch { }
                    return null;
                }

                public string UObjectOuterName(object uObject)
                {
                    try
                    {
                        IntPtr outerPtr = UObjectOuter(uObject);
                        if (outerPtr == IntPtr.Zero) return null;

                        return UObjectShortName(outerPtr);
                    }
                    catch { }
                    return null;
                }

                IntPtr _GEngine = IntPtr.Zero;
				public IntPtr GEngine
				{
					get
					{
						if (_GEngine != IntPtr.Zero) return _GEngine;
						else
						{
							_GEngine = dataRetriever.FindData("GEngine");
							return _GEngine;
						}
					}

					set { _GEngine = value; }
				}

				IntPtr _GWorld = IntPtr.Zero;
				public IntPtr GWorld
				{
					get
					{
						if (_GWorld != IntPtr.Zero) return _GWorld;
						else
						{
							_GWorld = dataRetriever.FindData("GWorld");
							return _GWorld;
						}
					}

					set { _GWorld = value; }
				}

                IntPtr _FNamePool = IntPtr.Zero;
                public IntPtr FNamePool
                {
                    get
                    {
                        if (_FNamePool != IntPtr.Zero) return _FNamePool;
                        else
                        {
                            _FNamePool = dataRetriever.FindData("FNames");
                            return _FNamePool;
                        }
                    }

                    set { _FNamePool = value; }
                }
                IntPtr _FNames = IntPtr.Zero;
				public IntPtr FNames
				{
					get
					{
						if (_FNames != IntPtr.Zero) return _FNames;
						else
						{
							_FNames = dataRetriever.FindData("FNames");
							return _FNames;
						}
					}

                    set { _FNames = value; }
                }

				IntPtr _GSync = IntPtr.Zero;
				public IntPtr GSync
				{
					get
					{
						if (_GSync != IntPtr.Zero) return _GSync;
						else
						{
							_GSync = dataRetriever.FindData("GSync");
							return _GSync;
						}
					}

                    set { _GSync = value; }
                }

				public IntPtr FindData(string dataName)
				{
					try
					{
						return dataRetriever.FindData(dataName);
					}
					catch { }
					return IntPtr.Zero;
				}
				#endregion

				public Utilities()
				{
					if (!Main.ReloadProcess()) throw new Exception();
					ulong modBase = TProcess.GetModuleBase(Main.ProcessInstance);
					if (modBase == 0) throw new Exception();

					string processToken = TProcess.GetToken(Main.ProcessInstance) ?? string.Empty;
					lock (initLock)
					{
						if (initializedProcessToken != processToken)
						{
							MemoryManager.ClearMemory(ToolUniqueID);
							initializedProcessToken = processToken;
						}
					}

					// ---
                    dataRetriever = new DataRetriever();
                    textReader = new TextReader();
                    fpsLocker = new FpsLocker();
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
}
