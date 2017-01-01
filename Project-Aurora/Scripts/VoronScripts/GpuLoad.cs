//
// Voron Scripts - GpuLoad
// v1.0-beta.4
// for Aurora v0.6.0
// https://github.com/VoronFX/Aurora
// Copyright (C) 2016 Voronin Igor <Voron.exe@gmail.com>
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aurora.Devices;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Scripts.VoronScripts.OpenHardwareMonitor;
using Aurora.Scripts.VoronScripts.OpenHardwareMonitor.OpenHardwareMonitor.Hardware.ATI;
using Aurora.Scripts.VoronScripts.OpenHardwareMonitor.OpenHardwareMonitor.Hardware.Nvidia;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{

	public class GpuLoad
	{
		public string ID = "GpuLoad";

		public KeySequence DefaultKeys = new KeySequence(
			new[] { DeviceKeys.G5, DeviceKeys.G4, DeviceKeys.G3, DeviceKeys.G2, DeviceKeys.G1 });

		// Independant RainbowLoop
		private readonly ColorSpectrum rainbowLoop = new ColorSpectrum(
			Color.FromArgb(255, 0, 0),
			Color.FromArgb(255, 127, 0),
			Color.FromArgb(255, 255, 0),
			Color.FromArgb(0, 255, 0),
			Color.FromArgb(0, 0, 255),
			Color.FromArgb(75, 0, 130),
			Color.FromArgb(139, 0, 255),
			Color.FromArgb(255, 0, 0)
			);

		private readonly ColorSpectrum loadGradient = new ColorSpectrum(Color.Lime, Color.Orange, Color.Red);

		private readonly DeviceKeys[] gpuRainbowKeys =
			{ DeviceKeys.PRINT_SCREEN, DeviceKeys.SCROLL_LOCK, DeviceKeys.PAUSE_BREAK };

		private float blinkingThreshold = 0.95f;
		private int blinkingSpeed = 1000;

		public EffectLayer[] UpdateLights(ScriptSettings settings, IGameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			EffectLayer GPULayer = new EffectLayer(ID + " - GpuLoad");
			EffectLayer GPURainbowLayer = new EffectLayer(ID + " - GpuRainbowLoad");

			var value = Gpu.GetValue() / 100f;
			//(Utils.Time.GetMillisecondsSinceEpoch() % 3000) / 30.0f / 100f;

			var blinkingLevel = (value - blinkingThreshold) / (1 - blinkingThreshold);

			blinkingLevel = Math.Max(0, Math.Min(1, blinkingLevel))
				* Math.Abs(1f - (Utils.Time.GetMillisecondsSinceEpoch() % blinkingSpeed) / (blinkingSpeed / 2f));

			if (DefaultKeys.type == KeySequenceType.Sequence)
			{
				// Animating by key sequence manually cause of bug in PercentEffect in Aurora v0.5.1d

				for (int i = 0; i < DefaultKeys.keys.Count; i++)
				{
					var blendLevel = Math.Min(1, Math.Max(0,
						(value - (i / (float)DefaultKeys.keys.Count)) / (1f / DefaultKeys.keys.Count)));

					GPULayer.Set(DefaultKeys.keys[i], Color.FromArgb((byte)(blendLevel * 255),
						loadGradient.GetColorAt(i / (DefaultKeys.keys.Count - 1f))));

					if (blinkingThreshold <= 1)
					{
						GPULayer.Set(DefaultKeys.keys[i], (Color)EffectColor.BlendColors(
							new EffectColor(GPULayer.Get(DefaultKeys.keys[i])),
							new EffectColor(Color.Black),
								blendLevel * i / (DefaultKeys.keys.Count - 1f) * blinkingLevel));
					}
				}
			}
			else
			{
				GPULayer.PercentEffect(loadGradient, DefaultKeys, value, 1, PercentEffectType.Progressive_Gradual);
				GPULayer.PercentEffect(
					new ColorSpectrum(
						Color.FromArgb(0, Color.Black),
						Color.FromArgb((byte)(255 * blinkingLevel), Color.Black)),
					DefaultKeys, value, 1, PercentEffectType.Progressive_Gradual);
			}

			rainbowLoop.Shift((float)(-0.003 + -0.01 * value));

			for (int i = 0; i < gpuRainbowKeys.Length; i++)
			{
				GPURainbowLayer.Set(gpuRainbowKeys[i],
					Color.FromArgb((byte)(255 * value),
					rainbowLoop.GetColorAt(i, gpuRainbowKeys.Length * 3)));
			}

			layers.Enqueue(GPULayer);
			layers.Enqueue(GPURainbowLayer);

			return layers.ToArray();
		}

		private static readonly GpuCounter Gpu = new GpuCounter();

		internal class GpuCounter : EasedPerformanceCounter<float>
		{
			private readonly NvPhysicalGpuHandle? nvidiaGpu;
			private readonly int? atiGpu;


			public GpuCounter()
			{
				var nvidiaGpus = GetNvidiaGpus();
				if (nvidiaGpus.Length > 0)
					nvidiaGpu = nvidiaGpus[0].Key;
				else
				{
					var atiGpus = GetAtiGpus();
					if (atiGpus.Length > 0)
					{
						atiGpu = atiGpus[0];
					}
				}
			}

			protected override float GetEasedValue(CounterFrame<float> currentFrame)
			{
				return currentFrame.PreviousValue + (currentFrame.CurrentValue - currentFrame.PreviousValue) *
					   Math.Min(Utils.Time.GetMillisecondsSinceEpoch() - currentFrame.Timestamp, UpdateInterval) / UpdateInterval;
			}

			protected override float UpdateValue()
			{
				if (nvidiaGpu.HasValue)
				{
					NvPStates states = new NvPStates
					{
						Version = NVAPI.GPU_PSTATES_VER,
						PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
					};
					if (NVAPI.NvAPI_GPU_GetPStates != null &&
					  NVAPI.NvAPI_GPU_GetPStates(nvidiaGpu.Value, ref states) == NvStatus.OK)
					{
						return states.PStates[0].Percentage;
					}

					NvUsages usages = new NvUsages
					{
						Version = NVAPI.GPU_USAGES_VER,
						Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
					};
					if (NVAPI.NvAPI_GPU_GetUsages != null &&
						NVAPI.NvAPI_GPU_GetUsages(nvidiaGpu.Value, ref usages) == NvStatus.OK)
					{
						return usages.Usage[2];
					}
				}

				if (atiGpu.HasValue)
				{
					ADLPMActivity adlp = new ADLPMActivity();
					if (ADL.ADL_Overdrive5_CurrentActivity_Get(atiGpu.Value, ref adlp)
					  == ADL.ADL_OK)
					{
						return Math.Min(adlp.ActivityPercent, 100);
					}
				}
				return 0;
			}

			private static int[] GetAtiGpus()
			{
				var agpus = new List<ADLAdapterInfo>();
				try
				{
					int status = ADL.ADL_Main_Control_Create(1);

					Global.logger.LogLine("AMD Display Library");
					Global.logger.LogLine("Status: " + (status == ADL.ADL_OK ?
						"OK" : status.ToString(CultureInfo.InvariantCulture)));

					if (status == ADL.ADL_OK)
					{
						int numberOfAdapters = 0;
						ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

						Global.logger.LogLine("Number of adapters:" + numberOfAdapters);

						if (numberOfAdapters > 0)
						{
							ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
							if (ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADL.ADL_OK)
							{
								for (int i = 0; i < numberOfAdapters; i++)
								{
									int isActive;
									ADL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex,
										out isActive);
									int adapterID;
									ADL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex,
										out adapterID);
									Global.logger.LogLine("AdapterIndex:" + i);
									Global.logger.LogLine("isActive:" + isActive);
									Global.logger.LogLine("AdapterName:" + adapterInfo[i].AdapterName);
									Global.logger.LogLine("UDID:" + adapterInfo[i].UDID);
									Global.logger.LogLine("Present:" + adapterInfo[i].Present);
									Global.logger.LogLine("VendorID: 0x" + adapterInfo[i].VendorID);
									Global.logger.LogLine("BusNumber:" + adapterInfo[i].BusNumber);
									Global.logger.LogLine("DeviceNumber:" + adapterInfo[i].DeviceNumber);
									Global.logger.LogLine("FunctionNumber:" + adapterInfo[i].FunctionNumber);
									Global.logger.LogLine("AdapterID: 0x" + adapterID);

									if (!string.IsNullOrEmpty(adapterInfo[i].UDID) &&
										adapterInfo[i].VendorID == ADL.ATI_VENDOR_ID)
									{
										bool found = false;
										foreach (var gpu in agpus)
											if (gpu.BusNumber == adapterInfo[i].BusNumber &&
												gpu.DeviceNumber == adapterInfo[i].DeviceNumber)
											{
												found = true;
												break;
											}
										if (!found)
											agpus.Add(adapterInfo[i]);
									}
								}
							}
						}
					}

				}
				catch (DllNotFoundException) { }
				catch (EntryPointNotFoundException e)
				{
					Global.logger.LogLine("Error: " + e, Logging_Level.Error);
				}
				return agpus.Select(x => x.AdapterIndex).ToArray();
			}

			private static KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>[] GetNvidiaGpus()
			{
				var ngpus = new List<KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>>();
				if (NVAPI.IsAvailable)
				{
					Global.logger.LogLine("NVAPI");

					string version;
					if (NVAPI.NvAPI_GetInterfaceVersionString(out version) == NvStatus.OK)
					{
						Global.logger.LogLine("Version: " + version);
					}

					NvPhysicalGpuHandle[] handles =
						new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
					int count;
					if (NVAPI.NvAPI_EnumPhysicalGPUs == null)
					{
						Global.logger.LogLine("Error: NvAPI_EnumPhysicalGPUs not available", Logging_Level.Error);
						return ngpus.ToArray();
					}
					else
					{
						NvStatus status = NVAPI.NvAPI_EnumPhysicalGPUs(handles, out count);
						if (status != NvStatus.OK)
						{
							Global.logger.LogLine("Status: " + status);
							return ngpus.ToArray();
						}
					}

					var displayHandles = new Dictionary<NvPhysicalGpuHandle, NvDisplayHandle>();

					if (NVAPI.NvAPI_EnumNvidiaDisplayHandle != null &&
						NVAPI.NvAPI_GetPhysicalGPUsFromDisplay != null)
					{
						NvStatus status = NvStatus.OK;
						int i = 0;
						while (status == NvStatus.OK)
						{
							NvDisplayHandle displayHandle = new NvDisplayHandle();
							status = NVAPI.NvAPI_EnumNvidiaDisplayHandle(i, ref displayHandle);
							i++;

							if (status == NvStatus.OK)
							{
								NvPhysicalGpuHandle[] handlesFromDisplay =
									new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
								uint countFromDisplay;
								if (NVAPI.NvAPI_GetPhysicalGPUsFromDisplay(displayHandle,
										handlesFromDisplay, out countFromDisplay) == NvStatus.OK)
								{
									for (int j = 0; j < countFromDisplay; j++)
									{
										if (!displayHandles.ContainsKey(handlesFromDisplay[j]))
											displayHandles.Add(handlesFromDisplay[j], displayHandle);
									}
								}
							}
						}
					}

					Global.logger.LogLine("Number of GPUs: " + count);

					for (int i = 0; i < count; i++)
					{
						NvDisplayHandle displayHandle;
						displayHandles.TryGetValue(handles[i], out displayHandle);
						ngpus.Add(new KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>(handles[i], displayHandle));
					}
				}
				return ngpus.ToArray();
			}
		}

		internal abstract class EasedPerformanceCounter<T>
		{
			public int UpdateInterval { get; set; }
			public int IdleTimeout { get; set; }

			protected struct CounterFrame<T2>
			{
				public readonly T2 PreviousValue;
				public readonly T2 CurrentValue;
				public readonly long Timestamp;

				public CounterFrame(T2 previousValue, T2 currentValue)
				{
					PreviousValue = previousValue;
					CurrentValue = currentValue;
					Timestamp = Utils.Time.GetMillisecondsSinceEpoch();
				}
			}

			private CounterFrame<T> frame;
			private T lastEasedValue;

			private readonly Timer timer;
			private int counterUsage;
			private bool sleeping = true;
			private int awakening;

			protected abstract T GetEasedValue(CounterFrame<T> currentFrame);
			protected abstract T UpdateValue();

			public T GetValue(bool easing = true)
			{
				counterUsage = IdleTimeout;
				if (sleeping)
				{
					if (Interlocked.CompareExchange(ref awakening, 1, 0) == 1)
					{
						sleeping = false;
						timer.Change(0, Timeout.Infinite);
					}
				}

				if (easing)
				{
					lastEasedValue = GetEasedValue(frame);
					return lastEasedValue;
				}
				return frame.CurrentValue;
			}

			protected EasedPerformanceCounter()
			{
				UpdateInterval = 1000;
				IdleTimeout = 3;
				timer = new Timer(UpdateTick, null, Timeout.Infinite, Timeout.Infinite);
			}

			private void UpdateTick(object state)
			{
				try
				{
					frame = new CounterFrame<T>(lastEasedValue, UpdateValue());
				}
				catch (Exception exc)
				{
					Global.logger.LogLine("EasedPerformanceCounter exception: " + exc, Logging_Level.Error);
				}
				finally
				{
					counterUsage--;
					if (counterUsage <= 0)
					{
						awakening = 0;
						sleeping = true;
					}
					else
					{
						timer.Change(UpdateInterval, Timeout.Infinite);
					}
				}
			}
		}

	}

	#region OpenHardwareMonitor

	// These pieces of code are taken from OpenHardwareMonitor project
	// https://github.com/openhardwaremonitor/openhardwaremonitor

	namespace OpenHardwareMonitor
	{
		/*

		  This Source Code Form is subject to the terms of the Mozilla Public
		  License, v. 2.0. If a copy of the MPL was not distributed with this
		  file, You can obtain one at http://mozilla.org/MPL/2.0/.

		  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
			Copyright (C) 2011 Christian Vallières

		*/

		namespace OpenHardwareMonitor.Hardware.ATI
		{

			[StructLayout(LayoutKind.Sequential)]
			internal struct ADLAdapterInfo
			{
				public int Size;
				public int AdapterIndex;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string UDID;
				public int BusNumber;
				public int DeviceNumber;
				public int FunctionNumber;
				public int VendorID;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string AdapterName;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string DisplayName;
				public int Present;
				public int Exist;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string DriverPath;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string DriverPathExt;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADL.ADL_MAX_PATH)]
				public string PNPString;
				public int OSDisplayIndex;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct ADLPMActivity
			{
				public int Size;
				public int EngineClock;
				public int MemoryClock;
				public int Vddc;
				public int ActivityPercent;
				public int CurrentPerformanceLevel;
				public int CurrentBusSpeed;
				public int CurrentBusLanes;
				public int MaximumBusLanes;
				public int Reserved;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct ADLTemperature
			{
				public int Size;
				public int Temperature;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct ADLFanSpeedValue
			{
				public int Size;
				public int SpeedType;
				public int FanSpeed;
				public int Flags;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct ADLFanSpeedInfo
			{
				public int Size;
				public int Flags;
				public int MinPercent;
				public int MaxPercent;
				public int MinRPM;
				public int MaxRPM;
			}

			internal class ADL
			{
				public const int ADL_MAX_PATH = 256;
				public const int ADL_MAX_ADAPTERS = 40;
				public const int ADL_MAX_DISPLAYS = 40;
				public const int ADL_MAX_DEVICENAME = 32;
				public const int ADL_OK = 0;
				public const int ADL_ERR = -1;
				public const int ADL_DRIVER_OK = 0;
				public const int ADL_MAX_GLSYNC_PORTS = 8;
				public const int ADL_MAX_GLSYNC_PORT_LEDS = 8;
				public const int ADL_MAX_NUM_DISPLAYMODES = 1024;

				public const int ADL_DL_FANCTRL_SPEED_TYPE_PERCENT = 1;
				public const int ADL_DL_FANCTRL_SPEED_TYPE_RPM = 2;

				public const int ADL_DL_FANCTRL_SUPPORTS_PERCENT_READ = 1;
				public const int ADL_DL_FANCTRL_SUPPORTS_PERCENT_WRITE = 2;
				public const int ADL_DL_FANCTRL_SUPPORTS_RPM_READ = 4;
				public const int ADL_DL_FANCTRL_SUPPORTS_RPM_WRITE = 8;
				public const int ADL_DL_FANCTRL_FLAG_USER_DEFINED_SPEED = 1;

				public const int ATI_VENDOR_ID = 0x1002;

				private delegate int ADL_Main_Control_CreateDelegate(
				  ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters);
				private delegate int ADL_Adapter_AdapterInfo_GetDelegate(IntPtr info,
				  int size);

				public delegate int ADL_Main_Control_DestroyDelegate();
				public delegate int ADL_Adapter_NumberOfAdapters_GetDelegate(
				  ref int numAdapters);
				public delegate int ADL_Adapter_ID_GetDelegate(int adapterIndex,
				  out int adapterID);
				public delegate int ADL_Display_AdapterID_GetDelegate(int adapterIndex,
				  out int adapterID);
				public delegate int ADL_Adapter_Active_GetDelegate(int adapterIndex,
				  out int status);
				public delegate int ADL_Overdrive5_CurrentActivity_GetDelegate(
				  int iAdapterIndex, ref ADLPMActivity activity);
				public delegate int ADL_Overdrive5_Temperature_GetDelegate(int adapterIndex,
					int thermalControllerIndex, ref ADLTemperature temperature);
				public delegate int ADL_Overdrive5_FanSpeed_GetDelegate(int adapterIndex,
					int thermalControllerIndex, ref ADLFanSpeedValue fanSpeedValue);
				public delegate int ADL_Overdrive5_FanSpeedInfo_GetDelegate(
				  int adapterIndex, int thermalControllerIndex,
				  ref ADLFanSpeedInfo fanSpeedInfo);
				public delegate int ADL_Overdrive5_FanSpeedToDefault_SetDelegate(
				  int adapterIndex, int thermalControllerIndex);
				public delegate int ADL_Overdrive5_FanSpeed_SetDelegate(int adapterIndex,
				  int thermalControllerIndex, ref ADLFanSpeedValue fanSpeedValue);

				private static ADL_Main_Control_CreateDelegate
				  _ADL_Main_Control_Create;
				private static ADL_Adapter_AdapterInfo_GetDelegate
				  _ADL_Adapter_AdapterInfo_Get;

				public static ADL_Main_Control_DestroyDelegate
				  ADL_Main_Control_Destroy;
				public static ADL_Adapter_NumberOfAdapters_GetDelegate
				  ADL_Adapter_NumberOfAdapters_Get;
				public static ADL_Adapter_ID_GetDelegate
				  _ADL_Adapter_ID_Get;
				public static ADL_Display_AdapterID_GetDelegate
				  _ADL_Display_AdapterID_Get;
				public static ADL_Adapter_Active_GetDelegate
				  ADL_Adapter_Active_Get;
				public static ADL_Overdrive5_CurrentActivity_GetDelegate
				  ADL_Overdrive5_CurrentActivity_Get;
				public static ADL_Overdrive5_Temperature_GetDelegate
				  ADL_Overdrive5_Temperature_Get;
				public static ADL_Overdrive5_FanSpeed_GetDelegate
				  ADL_Overdrive5_FanSpeed_Get;
				public static ADL_Overdrive5_FanSpeedInfo_GetDelegate
				  ADL_Overdrive5_FanSpeedInfo_Get;
				public static ADL_Overdrive5_FanSpeedToDefault_SetDelegate
				  ADL_Overdrive5_FanSpeedToDefault_Set;
				public static ADL_Overdrive5_FanSpeed_SetDelegate
				  ADL_Overdrive5_FanSpeed_Set;

				private static string dllName;

				private static void GetDelegate<T>(string entryPoint, out T newDelegate)
				  where T : class
				{
					DllImportAttribute attribute = new DllImportAttribute(dllName);
					attribute.CallingConvention = CallingConvention.Cdecl;
					attribute.PreserveSig = true;
					attribute.EntryPoint = entryPoint;
					PInvokeDelegateFactory.CreateDelegate(attribute, out newDelegate);
				}

				private static void CreateDelegates(string name)
				{
					int p = (int)Environment.OSVersion.Platform;
					if ((p == 4) || (p == 128))
						dllName = name + ".so";
					else
						dllName = name + ".dll";

					GetDelegate("ADL_Main_Control_Create",
					  out _ADL_Main_Control_Create);
					GetDelegate("ADL_Adapter_AdapterInfo_Get",
					  out _ADL_Adapter_AdapterInfo_Get);
					GetDelegate("ADL_Main_Control_Destroy",
					  out ADL_Main_Control_Destroy);
					GetDelegate("ADL_Adapter_NumberOfAdapters_Get",
					  out ADL_Adapter_NumberOfAdapters_Get);
					GetDelegate("ADL_Adapter_ID_Get",
					  out _ADL_Adapter_ID_Get);
					GetDelegate("ADL_Display_AdapterID_Get",
					  out _ADL_Display_AdapterID_Get);
					GetDelegate("ADL_Adapter_Active_Get",
					  out ADL_Adapter_Active_Get);
					GetDelegate("ADL_Overdrive5_CurrentActivity_Get",
					  out ADL_Overdrive5_CurrentActivity_Get);
					GetDelegate("ADL_Overdrive5_Temperature_Get",
					  out ADL_Overdrive5_Temperature_Get);
					GetDelegate("ADL_Overdrive5_FanSpeed_Get",
					  out ADL_Overdrive5_FanSpeed_Get);
					GetDelegate("ADL_Overdrive5_FanSpeedInfo_Get",
					  out ADL_Overdrive5_FanSpeedInfo_Get);
					GetDelegate("ADL_Overdrive5_FanSpeedToDefault_Set",
					  out ADL_Overdrive5_FanSpeedToDefault_Set);
					GetDelegate("ADL_Overdrive5_FanSpeed_Set",
					  out ADL_Overdrive5_FanSpeed_Set);
				}

				static ADL()
				{
					CreateDelegates("atiadlxx");
				}

				private ADL() { }

				public static int ADL_Main_Control_Create(int enumConnectedAdapters)
				{
					try
					{
						try
						{
							return _ADL_Main_Control_Create(Main_Memory_Alloc,
							  enumConnectedAdapters);
						}
						catch
						{
							CreateDelegates("atiadlxy");
							return _ADL_Main_Control_Create(Main_Memory_Alloc,
							  enumConnectedAdapters);
						}
					}
					catch
					{
						return ADL_ERR;
					}
				}

				public static int ADL_Adapter_AdapterInfo_Get(ADLAdapterInfo[] info)
				{
					int elementSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
					int size = info.Length * elementSize;
					IntPtr ptr = Marshal.AllocHGlobal(size);
					int result = _ADL_Adapter_AdapterInfo_Get(ptr, size);
					for (int i = 0; i < info.Length; i++)
						info[i] = (ADLAdapterInfo)
						  Marshal.PtrToStructure((IntPtr)((long)ptr + i * elementSize),
						  typeof(ADLAdapterInfo));
					Marshal.FreeHGlobal(ptr);

					// the ADLAdapterInfo.VendorID field reported by ADL is wrong on 
					// Windows systems (parse error), so we fix this here
					for (int i = 0; i < info.Length; i++)
					{
						// try Windows UDID format
						Match m = Regex.Match(info[i].UDID, "PCI_VEN_([A-Fa-f0-9]{1,4})&.*");
						if (m.Success && m.Groups.Count == 2)
						{
							info[i].VendorID = Convert.ToInt32(m.Groups[1].Value, 16);
							continue;
						}
						// if above failed, try Unix UDID format
						m = Regex.Match(info[i].UDID, "[0-9]+:[0-9]+:([0-9]+):[0-9]+:[0-9]+");
						if (m.Success && m.Groups.Count == 2)
						{
							info[i].VendorID = Convert.ToInt32(m.Groups[1].Value, 10);
						}
					}

					return result;
				}

				public static int ADL_Adapter_ID_Get(int adapterIndex,
				  out int adapterID)
				{
					try
					{
						return _ADL_Adapter_ID_Get(adapterIndex, out adapterID);
					}
					catch (EntryPointNotFoundException)
					{
						try
						{
							return _ADL_Display_AdapterID_Get(adapterIndex, out adapterID);
						}
						catch (EntryPointNotFoundException)
						{
							adapterID = 1;
							return ADL_OK;
						}
					}
				}

				private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);

				// create a Main_Memory_Alloc delegate and keep it alive
				private static ADL_Main_Memory_AllocDelegate Main_Memory_Alloc =
				  delegate (int size)
				  {
					  return Marshal.AllocHGlobal(size);
				  };

				private static void Main_Memory_Free(IntPtr buffer)
				{
					if (IntPtr.Zero != buffer)
						Marshal.FreeHGlobal(buffer);
				}
			}
		}

		namespace OpenHardwareMonitor.Hardware.Nvidia
		{

			internal enum NvStatus
			{
				OK = 0,
				ERROR = -1,
				LIBRARY_NOT_FOUND = -2,
				NO_IMPLEMENTATION = -3,
				API_NOT_INTIALIZED = -4,
				INVALID_ARGUMENT = -5,
				NVIDIA_DEVICE_NOT_FOUND = -6,
				END_ENUMERATION = -7,
				INVALID_HANDLE = -8,
				INCOMPATIBLE_STRUCT_VERSION = -9,
				HANDLE_INVALIDATED = -10,
				OPENGL_CONTEXT_NOT_CURRENT = -11,
				NO_GL_EXPERT = -12,
				INSTRUMENTATION_DISABLED = -13,
				EXPECTED_LOGICAL_GPU_HANDLE = -100,
				EXPECTED_PHYSICAL_GPU_HANDLE = -101,
				EXPECTED_DISPLAY_HANDLE = -102,
				INVALID_COMBINATION = -103,
				NOT_SUPPORTED = -104,
				PORTID_NOT_FOUND = -105,
				EXPECTED_UNATTACHED_DISPLAY_HANDLE = -106,
				INVALID_PERF_LEVEL = -107,
				DEVICE_BUSY = -108,
				NV_PERSIST_FILE_NOT_FOUND = -109,
				PERSIST_DATA_NOT_FOUND = -110,
				EXPECTED_TV_DISPLAY = -111,
				EXPECTED_TV_DISPLAY_ON_DCONNECTOR = -112,
				NO_ACTIVE_SLI_TOPOLOGY = -113,
				SLI_RENDERING_MODE_NOTALLOWED = -114,
				EXPECTED_DIGITAL_FLAT_PANEL = -115,
				ARGUMENT_EXCEED_MAX_SIZE = -116,
				DEVICE_SWITCHING_NOT_ALLOWED = -117,
				TESTING_CLOCKS_NOT_SUPPORTED = -118,
				UNKNOWN_UNDERSCAN_CONFIG = -119,
				TIMEOUT_RECONFIGURING_GPU_TOPO = -120,
				DATA_NOT_FOUND = -121,
				EXPECTED_ANALOG_DISPLAY = -122,
				NO_VIDLINK = -123,
				REQUIRES_REBOOT = -124,
				INVALID_HYBRID_MODE = -125,
				MIXED_TARGET_TYPES = -126,
				SYSWOW64_NOT_SUPPORTED = -127,
				IMPLICIT_SET_GPU_TOPOLOGY_CHANGE_NOT_ALLOWED = -128,
				REQUEST_USER_TO_CLOSE_NON_MIGRATABLE_APPS = -129,
				OUT_OF_MEMORY = -130,
				WAS_STILL_DRAWING = -131,
				FILE_NOT_FOUND = -132,
				TOO_MANY_UNIQUE_STATE_OBJECTS = -133,
				INVALID_CALL = -134,
				D3D10_1_LIBRARY_NOT_FOUND = -135,
				FUNCTION_NOT_FOUND = -136
			}

			internal enum NvThermalController
			{
				NONE = 0,
				GPU_INTERNAL,
				ADM1032,
				MAX6649,
				MAX1617,
				LM99,
				LM89,
				LM64,
				ADT7473,
				SBMAX6649,
				VBIOSEVT,
				OS,
				UNKNOWN = -1,
			}

			internal enum NvThermalTarget
			{
				NONE = 0,
				GPU = 1,
				MEMORY = 2,
				POWER_SUPPLY = 4,
				BOARD = 8,
				ALL = 15,
				UNKNOWN = -1
			};

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvSensor
			{
				public NvThermalController Controller;
				public uint DefaultMinTemp;
				public uint DefaultMaxTemp;
				public uint CurrentTemp;
				public NvThermalTarget Target;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvGPUThermalSettings
			{
				public uint Version;
				public uint Count;
				[MarshalAs(UnmanagedType.ByValArray,
				  SizeConst = NVAPI.MAX_THERMAL_SENSORS_PER_GPU)]
				public NvSensor[] Sensor;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct NvDisplayHandle
			{
				private readonly IntPtr ptr;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct NvPhysicalGpuHandle
			{
				private readonly IntPtr ptr;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvClocks
			{
				public uint Version;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI.MAX_CLOCKS_PER_GPU)]
				public uint[] Clock;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvPState
			{
				public bool Present;
				public int Percentage;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvPStates
			{
				public uint Version;
				public uint Flags;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI.MAX_PSTATES_PER_GPU)]
				public NvPState[] PStates;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvUsages
			{
				public uint Version;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI.MAX_USAGES_PER_GPU)]
				public uint[] Usage;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvCooler
			{
				public int Type;
				public int Controller;
				public int DefaultMin;
				public int DefaultMax;
				public int CurrentMin;
				public int CurrentMax;
				public int CurrentLevel;
				public int DefaultPolicy;
				public int CurrentPolicy;
				public int Target;
				public int ControlType;
				public int Active;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvGPUCoolerSettings
			{
				public uint Version;
				public uint Count;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI.MAX_COOLER_PER_GPU)]
				public NvCooler[] Cooler;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvLevel
			{
				public int Level;
				public int Policy;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvGPUCoolerLevels
			{
				public uint Version;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI.MAX_COOLER_PER_GPU)]
				public NvLevel[] Levels;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvMemoryInfo
			{
				public uint Version;
				[MarshalAs(UnmanagedType.ByValArray, SizeConst =
				  NVAPI.MAX_MEMORY_VALUES_PER_GPU)]
				public uint[] Values;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 8)]
			internal struct NvDisplayDriverVersion
			{
				public uint Version;
				public uint DriverVersion;
				public uint BldChangeListNum;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = NVAPI.SHORT_STRING_MAX)]
				public string BuildBranch;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = NVAPI.SHORT_STRING_MAX)]
				public string Adapter;
			}

			internal class NVAPI
			{

				public const int MAX_PHYSICAL_GPUS = 64;
				public const int SHORT_STRING_MAX = 64;

				public const int MAX_THERMAL_SENSORS_PER_GPU = 3;
				public const int MAX_CLOCKS_PER_GPU = 0x120;
				public const int MAX_PSTATES_PER_GPU = 8;
				public const int MAX_USAGES_PER_GPU = 33;
				public const int MAX_COOLER_PER_GPU = 20;
				public const int MAX_MEMORY_VALUES_PER_GPU = 5;

				public static readonly uint GPU_THERMAL_SETTINGS_VER = (uint)
				  Marshal.SizeOf(typeof(NvGPUThermalSettings)) | 0x10000;
				public static readonly uint GPU_CLOCKS_VER = (uint)
				  Marshal.SizeOf(typeof(NvClocks)) | 0x20000;
				public static readonly uint GPU_PSTATES_VER = (uint)
				  Marshal.SizeOf(typeof(NvPStates)) | 0x10000;
				public static readonly uint GPU_USAGES_VER = (uint)
				  Marshal.SizeOf(typeof(NvUsages)) | 0x10000;
				public static readonly uint GPU_COOLER_SETTINGS_VER = (uint)
				  Marshal.SizeOf(typeof(NvGPUCoolerSettings)) | 0x20000;
				public static readonly uint GPU_MEMORY_INFO_VER = (uint)
				  Marshal.SizeOf(typeof(NvMemoryInfo)) | 0x20000;
				public static readonly uint DISPLAY_DRIVER_VERSION_VER = (uint)
				  Marshal.SizeOf(typeof(NvDisplayDriverVersion)) | 0x10000;
				public static readonly uint GPU_COOLER_LEVELS_VER = (uint)
				  Marshal.SizeOf(typeof(NvGPUCoolerLevels)) | 0x10000;

				private delegate IntPtr nvapi_QueryInterfaceDelegate(uint id);
				private delegate NvStatus NvAPI_InitializeDelegate();
				private delegate NvStatus NvAPI_GPU_GetFullNameDelegate(
				  NvPhysicalGpuHandle gpuHandle, StringBuilder name);

				public delegate NvStatus NvAPI_GPU_GetThermalSettingsDelegate(
				  NvPhysicalGpuHandle gpuHandle, int sensorIndex,
				  ref NvGPUThermalSettings nvGPUThermalSettings);
				public delegate NvStatus NvAPI_EnumNvidiaDisplayHandleDelegate(int thisEnum,
				  ref NvDisplayHandle displayHandle);
				public delegate NvStatus NvAPI_GetPhysicalGPUsFromDisplayDelegate(
				  NvDisplayHandle displayHandle, [Out] NvPhysicalGpuHandle[] gpuHandles,
				  out uint gpuCount);
				public delegate NvStatus NvAPI_EnumPhysicalGPUsDelegate(
				  [Out] NvPhysicalGpuHandle[] gpuHandles, out int gpuCount);
				public delegate NvStatus NvAPI_GPU_GetTachReadingDelegate(
				  NvPhysicalGpuHandle gpuHandle, out int value);
				public delegate NvStatus NvAPI_GPU_GetAllClocksDelegate(
				  NvPhysicalGpuHandle gpuHandle, ref NvClocks nvClocks);
				public delegate NvStatus NvAPI_GPU_GetPStatesDelegate(
				  NvPhysicalGpuHandle gpuHandle, ref NvPStates nvPStates);
				public delegate NvStatus NvAPI_GPU_GetUsagesDelegate(
				  NvPhysicalGpuHandle gpuHandle, ref NvUsages nvUsages);
				public delegate NvStatus NvAPI_GPU_GetCoolerSettingsDelegate(
				  NvPhysicalGpuHandle gpuHandle, int coolerIndex,
				  ref NvGPUCoolerSettings nvGPUCoolerSettings);
				public delegate NvStatus NvAPI_GPU_SetCoolerLevelsDelegate(
				  NvPhysicalGpuHandle gpuHandle, int coolerIndex,
				  ref NvGPUCoolerLevels NvGPUCoolerLevels);
				public delegate NvStatus NvAPI_GPU_GetMemoryInfoDelegate(
				  NvDisplayHandle displayHandle, ref NvMemoryInfo nvMemoryInfo);
				public delegate NvStatus NvAPI_GetDisplayDriverVersionDelegate(
				  NvDisplayHandle displayHandle, [In, Out] ref NvDisplayDriverVersion
				  nvDisplayDriverVersion);
				public delegate NvStatus NvAPI_GetInterfaceVersionStringDelegate(
				  StringBuilder version);
				public delegate NvStatus NvAPI_GPU_GetPCIIdentifiersDelegate(
				  NvPhysicalGpuHandle gpuHandle, out uint deviceId, out uint subSystemId,
				  out uint revisionId, out uint extDeviceId);

				private static readonly bool available;
				private static readonly nvapi_QueryInterfaceDelegate nvapi_QueryInterface;
				private static readonly NvAPI_InitializeDelegate NvAPI_Initialize;
				private static readonly NvAPI_GPU_GetFullNameDelegate
				  _NvAPI_GPU_GetFullName;
				private static readonly NvAPI_GetInterfaceVersionStringDelegate
				  _NvAPI_GetInterfaceVersionString;

				public static readonly NvAPI_GPU_GetThermalSettingsDelegate
				  NvAPI_GPU_GetThermalSettings;
				public static readonly NvAPI_EnumNvidiaDisplayHandleDelegate
				  NvAPI_EnumNvidiaDisplayHandle;
				public static readonly NvAPI_GetPhysicalGPUsFromDisplayDelegate
				  NvAPI_GetPhysicalGPUsFromDisplay;
				public static readonly NvAPI_EnumPhysicalGPUsDelegate
				  NvAPI_EnumPhysicalGPUs;
				public static readonly NvAPI_GPU_GetTachReadingDelegate
				  NvAPI_GPU_GetTachReading;
				public static readonly NvAPI_GPU_GetAllClocksDelegate
				  NvAPI_GPU_GetAllClocks;
				public static readonly NvAPI_GPU_GetPStatesDelegate
				  NvAPI_GPU_GetPStates;
				public static readonly NvAPI_GPU_GetUsagesDelegate
				  NvAPI_GPU_GetUsages;
				public static readonly NvAPI_GPU_GetCoolerSettingsDelegate
				  NvAPI_GPU_GetCoolerSettings;
				public static readonly NvAPI_GPU_SetCoolerLevelsDelegate
				  NvAPI_GPU_SetCoolerLevels;
				public static readonly NvAPI_GPU_GetMemoryInfoDelegate
				  NvAPI_GPU_GetMemoryInfo;
				public static readonly NvAPI_GetDisplayDriverVersionDelegate
				  NvAPI_GetDisplayDriverVersion;
				public static readonly NvAPI_GPU_GetPCIIdentifiersDelegate
				  NvAPI_GPU_GetPCIIdentifiers;

				private NVAPI() { }

				public static NvStatus NvAPI_GPU_GetFullName(NvPhysicalGpuHandle gpuHandle,
				  out string name)
				{
					StringBuilder builder = new StringBuilder(SHORT_STRING_MAX);
					NvStatus status;
					if (_NvAPI_GPU_GetFullName != null)
						status = _NvAPI_GPU_GetFullName(gpuHandle, builder);
					else
						status = NvStatus.FUNCTION_NOT_FOUND;
					name = builder.ToString();
					return status;
				}

				public static NvStatus NvAPI_GetInterfaceVersionString(out string version)
				{
					StringBuilder builder = new StringBuilder(SHORT_STRING_MAX);
					NvStatus status;
					if (_NvAPI_GetInterfaceVersionString != null)
						status = _NvAPI_GetInterfaceVersionString(builder);
					else
						status = NvStatus.FUNCTION_NOT_FOUND;
					version = builder.ToString();
					return status;
				}

				private static string GetDllName()
				{
					if (IntPtr.Size == 4)
					{
						return "nvapi.dll";
					}
					else
					{
						return "nvapi64.dll";
					}
				}

				private static void GetDelegate<T>(uint id, out T newDelegate)
				  where T : class
				{
					IntPtr ptr = nvapi_QueryInterface(id);
					if (ptr != IntPtr.Zero)
					{
						newDelegate =
						  Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
					}
					else
					{
						newDelegate = null;
					}
				}

				static NVAPI()
				{
					DllImportAttribute attribute = new DllImportAttribute(GetDllName());
					attribute.CallingConvention = CallingConvention.Cdecl;
					attribute.PreserveSig = true;
					attribute.EntryPoint = "nvapi_QueryInterface";
					PInvokeDelegateFactory.CreateDelegate(attribute,
					  out nvapi_QueryInterface);

					try
					{
						GetDelegate(0x0150E828, out NvAPI_Initialize);
					}
					catch (DllNotFoundException) { return; }
					catch (EntryPointNotFoundException) { return; }
					catch (ArgumentNullException) { return; }

					if (NvAPI_Initialize() == NvStatus.OK)
					{
						GetDelegate(0xE3640A56, out NvAPI_GPU_GetThermalSettings);
						GetDelegate(0xCEEE8E9F, out _NvAPI_GPU_GetFullName);
						GetDelegate(0x9ABDD40D, out NvAPI_EnumNvidiaDisplayHandle);
						GetDelegate(0x34EF9506, out NvAPI_GetPhysicalGPUsFromDisplay);
						GetDelegate(0xE5AC921F, out NvAPI_EnumPhysicalGPUs);
						GetDelegate(0x5F608315, out NvAPI_GPU_GetTachReading);
						GetDelegate(0x1BD69F49, out NvAPI_GPU_GetAllClocks);
						GetDelegate(0x60DED2ED, out NvAPI_GPU_GetPStates);
						GetDelegate(0x189A1FDF, out NvAPI_GPU_GetUsages);
						GetDelegate(0xDA141340, out NvAPI_GPU_GetCoolerSettings);
						GetDelegate(0x891FA0AE, out NvAPI_GPU_SetCoolerLevels);
						GetDelegate(0x774AA982, out NvAPI_GPU_GetMemoryInfo);
						GetDelegate(0xF951A4D1, out NvAPI_GetDisplayDriverVersion);
						GetDelegate(0x01053FA5, out _NvAPI_GetInterfaceVersionString);
						GetDelegate(0x2DDFB66E, out NvAPI_GPU_GetPCIIdentifiers);

						available = true;
					}
				}

				public static bool IsAvailable
				{
					get { return available; }
				}

			}
		}

		internal struct Pair<F, S>
		{
			private F first;
			private S second;

			public Pair(F first, S second)
			{
				this.first = first;
				this.second = second;
			}

			public F First
			{
				get { return first; }
				set { first = value; }
			}

			public S Second
			{
				get { return second; }
				set { second = value; }
			}

			public override int GetHashCode()
			{
				return (first != null ? first.GetHashCode() : 0) ^
				  (second != null ? second.GetHashCode() : 0);
			}
		}

		internal static class PInvokeDelegateFactory
		{

			private static readonly ModuleBuilder moduleBuilder =
			  AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("PInvokeDelegateFactoryInternalAssembly"),
				AssemblyBuilderAccess.Run).DefineDynamicModule(
				"PInvokeDelegateFactoryInternalModule");

			private static readonly IDictionary<Pair<DllImportAttribute, Type>, Type> wrapperTypes =
			  new Dictionary<Pair<DllImportAttribute, Type>, Type>();

			public static void CreateDelegate<T>(DllImportAttribute dllImportAttribute,
			  out T newDelegate) where T : class
			{
				Type wrapperType;
				Pair<DllImportAttribute, Type> key =
				  new Pair<DllImportAttribute, Type>(dllImportAttribute, typeof(T));
				wrapperTypes.TryGetValue(key, out wrapperType);

				if (wrapperType == null)
				{
					wrapperType = CreateWrapperType(typeof(T), dllImportAttribute);
					wrapperTypes.Add(key, wrapperType);
				}

				newDelegate = Delegate.CreateDelegate(typeof(T), wrapperType,
				  dllImportAttribute.EntryPoint) as T;
			}


			private static Type CreateWrapperType(Type delegateType,
			  DllImportAttribute dllImportAttribute)
			{

				TypeBuilder typeBuilder = moduleBuilder.DefineType(
				  "PInvokeDelegateFactoryInternalWrapperType" + wrapperTypes.Count);

				MethodInfo methodInfo = delegateType.GetMethod("Invoke");

				ParameterInfo[] parameterInfos = methodInfo.GetParameters();
				int parameterCount = parameterInfos.GetLength(0);

				Type[] parameterTypes = new Type[parameterCount];
				for (int i = 0; i < parameterCount; i++)
					parameterTypes[i] = parameterInfos[i].ParameterType;

				MethodBuilder methodBuilder = typeBuilder.DefinePInvokeMethod(
				  dllImportAttribute.EntryPoint, dllImportAttribute.Value,
				  MethodAttributes.Public | MethodAttributes.Static |
				  MethodAttributes.PinvokeImpl, CallingConventions.Standard,
				  methodInfo.ReturnType, parameterTypes,
				  dllImportAttribute.CallingConvention,
				  dllImportAttribute.CharSet);

				foreach (ParameterInfo parameterInfo in parameterInfos)
					methodBuilder.DefineParameter(parameterInfo.Position + 1,
					  parameterInfo.Attributes, parameterInfo.Name);

				if (dllImportAttribute.PreserveSig)
					methodBuilder.SetImplementationFlags(MethodImplAttributes.PreserveSig);

				return typeBuilder.CreateType();
			}
		}
	}
	#endregion

}