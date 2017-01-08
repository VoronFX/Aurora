using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Aurora.Devices;
using Microsoft.VisualBasic.Devices;
using OpenHardwareMonitor.Hardware.ATI;
using OpenHardwareMonitor.Hardware.Nvidia;

namespace Aurora.Profiles.PerformanceCounters
{
	public class CpuTotal : EasedPerformanceCounterFloat
	{
		private PerformanceCounter counter;

		protected override float UpdateValue()
		{
			if (counter == null)
				counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
			return counter.NextValue();
		}
	}

	public class CpuPerCore : EasedPerformanceCounterMultiFloat
	{
		private PerformanceCounter[] counters;

		public CpuPerCore() : base(new float[Environment.ProcessorCount]) { }

		protected override float[] UpdateValue()
		{
			if (counters == null)
				counters = new PerformanceCounter[Environment.ProcessorCount]
					.Select((x, i) => new PerformanceCounter("Processor", "% Processor Time", i.ToString())).ToArray();

			return counters.Select(x => x.NextValue()).ToArray();
		}
	}

	public class Memory : EasedPerformanceCounter<long>
	{

		public long GetPhysicalAvailableMemoryInMiB()
		{
			return Convert.ToInt64(GetValue() / 1048576);
		}

		public long GetTotalMemoryInMiB()
		{
			return Convert.ToInt64(computerInfo.TotalPhysicalMemory / 1048576);
		}

		private readonly ComputerInfo computerInfo = new ComputerInfo();

		protected override long GetEasedValue(CounterFrame<long> currentFrame)
		{
			return currentFrame.PreviousValue + (currentFrame.CurrentValue - currentFrame.PreviousValue)
				* Math.Min(Utils.Time.GetMillisecondsSinceEpoch() - currentFrame.Timestamp, UpdateInterval) / UpdateInterval;
		}

		protected override long UpdateValue()
		{
			return Convert.ToInt64(computerInfo.AvailablePhysicalMemory);
		}

		public static void Register()
		{
			var propNames = new[] { "AvailablePhysicalMemoryInMiB", "AvailableVirtualMemoryInMiB",
				"% TotalPhysicalMemoryInMiB", "TotalVirtualMemoryInMiB", "% PhysicalMemoryUsed", "% VirtualMemoryUsed" };
			for (var i = 0; i < propNames.Length; i++)
			{
				var indexCopy = i;

				PerformanceCounterManager.RegisterInternal("Aurora Internal", nameof(ComputerInfo),
					propNames[i], () =>
					{
						var computerInfo = new ComputerInfo();

						switch (indexCopy)
						{
							case 0:
								return computerInfo.AvailablePhysicalMemory / 1048576f;
							case 1:
								return computerInfo.AvailableVirtualMemory / 1048576f;
							case 2:
								return computerInfo.TotalPhysicalMemory / 1048576f;
							case 3:
								return computerInfo.TotalVirtualMemory / 1048576f;
							case 4:
								return (computerInfo.TotalPhysicalMemory - computerInfo.AvailablePhysicalMemory) / 1048576f;
							case 5:
								return (computerInfo.TotalVirtualMemory - computerInfo.AvailableVirtualMemory) / 1048576f;
						}
						return 0;
					});
			}
		}
	}


	public class GpuPerformance
	{
		private static readonly KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>[] NvidiaGpus;
		private static readonly int[] AtiGpus;
		public static string InitLog;

		static GpuPerformance()
		{
#if DEBUG
			var log = new StringBuilder();
			var stringWriter = new StringWriter(log);
			var listener = new TextWriterTraceListener(stringWriter);
			Debug.Listeners.Add(listener); ;
#endif

			NvidiaGpus = GetNvidiaGpus();
			AtiGpus = GetAtiGpus();

#if DEBUG
			Debug.Listeners.Remove(listener);
			listener.Close();
			stringWriter.Close();
			InitLog = log.ToString();
#endif

			for (var i = 0; i < NvidiaGpus.Length; i++)
			{
				RegisterNvidiaFan("Aurora Internal", $"NvidiaGpu #{i}", NvidiaGpus[i].Key);
				RegisterNvidiaTemperatures("Aurora Internal", $"NvidiaGpu #{i}", NvidiaGpus[i].Key);
				RegisterNvidiaClocks("Aurora Internal", $"NvidiaGpu #{i}", NvidiaGpus[i].Key);
				RegisterNvidiaUsages("Aurora Internal", $"NvidiaGpu #{i}", NvidiaGpus[i].Key);
				RegisterNvidiaMemory("Aurora Internal", $"NvidiaGpu #{i}", NvidiaGpus[i].Value);
			}

			for (int i = 0; i < AtiGpus.Length; i++)
			{
				RegisterAtiFan("Aurora Internal", $"AtiGpu #{i}", AtiGpus[i]);
				RegisterAtiTemperature("Aurora Internal", $"AtiGpu #{i}", AtiGpus[i]);
				RegisterAtiActivity("Aurora Internal", $"AtiGpu #{i}", AtiGpus[i]);
			}
		}

		private static void RegisterNvidiaFan(string categoryName, string counterName, NvPhysicalGpuHandle gpu)
		{
			PerformanceCounterManager.RegisterInternal(categoryName, counterName,
				"FanRpm", () =>
				{
					int value;
					NVAPI.NvAPI_GPU_GetTachReading(gpu, out value);
					return value;
				});

			PerformanceCounterManager.RegisterInternal(categoryName, counterName,
				"% FanUsage", () =>
				{
					NvGPUCoolerSettings settings = new NvGPUCoolerSettings
					{
						Version = NVAPI.GPU_COOLER_SETTINGS_VER,
						Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
					};
					if (NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
							NVAPI.NvAPI_GPU_GetCoolerSettings(gpu, 0, ref settings) == NvStatus.OK)
						return settings.Cooler[0].CurrentLevel;
					return 0;
				});
		}

		private static void RegisterNvidiaTemperatures(string categoryName, string counterName, NvPhysicalGpuHandle gpu)
		{
			foreach (var target in new[] { NvThermalTarget.BOARD, NvThermalTarget.GPU,
					NvThermalTarget.MEMORY, NvThermalTarget.POWER_SUPPLY })
			{

				PerformanceCounterManager.RegisterInternal(categoryName, counterName,
					$"Temperature {Enum.GetName(typeof(NvThermalTarget), target)}", () =>
					{
						NvGPUThermalSettings settings = new NvGPUThermalSettings
						{
							Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
							Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
							Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetThermalSettings != null &&
								  NVAPI.NvAPI_GPU_GetThermalSettings(gpu, (int)NvThermalTarget.ALL,
									  ref settings) == NvStatus.OK)
						{
							return settings.Sensor.FirstOrDefault(s => s.Target == target).CurrentTemp;
						}
						return 0;
					});
			}
		}

		private static void RegisterNvidiaClocks(string categoryName, string counterName, NvPhysicalGpuHandle gpu)
		{
			var nvClocksNames = new[] { "Core", "Memory", "Shader" };
			for (var i = 0; i < nvClocksNames.Length; i++)
			{
				var indexCopy = i;

				PerformanceCounterManager.RegisterInternal(categoryName, counterName,
					$"Clock {nvClocksNames[i]}", () =>
					{
						NvClocks allClocks = new NvClocks
						{
							Version = NVAPI.GPU_CLOCKS_VER,
							Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetAllClocks == null ||
							NVAPI.NvAPI_GPU_GetAllClocks(gpu, ref allClocks) != NvStatus.OK)
							return 0;

						var values = allClocks.Clock;
						var clocks = new float[3];
						clocks[1] = 0.001f * values[8];
						if (values[30] != 0)
						{
							clocks[0] = 0.0005f * values[30];
							clocks[2] = 0.001f * values[30];
						}
						else
						{
							clocks[0] = 0.001f * values[0];
							clocks[2] = 0.001f * values[14];
						}
						return clocks[indexCopy];
					});
			}
		}

		private static void RegisterNvidiaUsages(string categoryName, string counterName, NvPhysicalGpuHandle gpu)
		{
			var nvUsageNames = new[] { "Core", "Memory Controller", "Video Engine" };
			for (var i = 0; i < nvUsageNames.Length; i++)
			{
				var indexCopy = i;

				PerformanceCounterManager.RegisterInternal(categoryName, counterName,
					$"% Load {nvUsageNames[i]}", () =>
					{
						NvPStates states = new NvPStates
						{
							Version = NVAPI.GPU_PSTATES_VER,
							PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetPStates != null &&
							NVAPI.NvAPI_GPU_GetPStates(gpu, ref states) == NvStatus.OK)
						{
							return states.PStates[indexCopy].Present ? (float)states.PStates[indexCopy].Percentage : 0;
						}

						NvUsages usages = new NvUsages
						{
							Version = NVAPI.GPU_USAGES_VER,
							Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetUsages != null &&
							NVAPI.NvAPI_GPU_GetUsages(gpu, ref usages) == NvStatus.OK)
						{
							switch (indexCopy)
							{
								case 0:
									return usages.Usage[2];
								case 1:
									return usages.Usage[6];
								case 2:
									return usages.Usage[10];
							}
						}
						return 0;
					});
			}
		}

		private static void RegisterNvidiaMemory(string categoryName, string counterName, NvDisplayHandle gpu)
		{
			var nvMemoryNames = new[] { "Memory Free", "Memory Used", "Memory Total", "% Memory Usage" };
			for (var i = 0; i < nvMemoryNames.Length; i++)
			{
				var indexCopy = i;

				PerformanceCounterManager.RegisterInternal(categoryName, counterName,
					nvMemoryNames[i], () =>
					{
						NvMemoryInfo memoryInfo = new NvMemoryInfo
						{
							Version = NVAPI.GPU_MEMORY_INFO_VER,
							Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetMemoryInfo != null &&
						  NVAPI.NvAPI_GPU_GetMemoryInfo(NvidiaGpus[0].Value, ref memoryInfo) ==
						  NvStatus.OK)
						{
							uint totalMemory = memoryInfo.Values[0];
							uint freeMemory = memoryInfo.Values[4];
							float usedMemory = Math.Max(totalMemory - freeMemory, 0);
							switch (indexCopy)
							{
								case 0:
									return freeMemory / 1024f;
								case 1:
									return usedMemory / 1024;
								case 2:
									return totalMemory / 1024f;
								case 3:
									return 100f * usedMemory / totalMemory;
							}
						}
						return 0;
					});
			}
		}

		private static void RegisterAtiFan(string categoryName, string counterName, int gpu)
		{
			PerformanceCounterManager.RegisterInternal(categoryName, counterName,
				"FanRpm", () =>
				{
					ADLFanSpeedValue adlf = new ADLFanSpeedValue { SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM };
					if (ADL.ADL_Overdrive5_FanSpeed_Get(gpu, 0, ref adlf)
						== ADL.ADL_OK)
					{
						return adlf.FanSpeed;
					}
					return 0;
				});

			PerformanceCounterManager.RegisterInternal(categoryName, counterName,
				"% FanUsage", () =>
				{
					ADLFanSpeedValue adlf = new ADLFanSpeedValue { SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT };
					if (ADL.ADL_Overdrive5_FanSpeed_Get(gpu, 0, ref adlf)
						== ADL.ADL_OK)
					{
						return adlf.FanSpeed;
					}
					return 0;
				});
		}

		private static void RegisterAtiTemperature(string categoryName, string counterName, int gpu)
		{
			PerformanceCounterManager.RegisterInternal(categoryName, counterName,
				"Temperature", () =>
				{
					ADLTemperature adlt = new ADLTemperature();
					if (ADL.ADL_Overdrive5_Temperature_Get(AtiGpus[0], 0, ref adlt)
					  == ADL.ADL_OK)
					{
						return 0.001f * adlt.Temperature;
					}
					return 0;
				});
		}

		private static void RegisterAtiActivity(string categoryName, string counterName, int gpu)
		{
			var nvMemoryNames = new[] { "Core Clock", "Memory Clock", "Core Voltage", "% Load Core" };
			for (var i = 0; i < nvMemoryNames.Length; i++)
			{
				var indexCopy = i;

				PerformanceCounterManager.RegisterInternal(categoryName, counterName,
					nvMemoryNames[i], () =>
					{
						ADLPMActivity adlp = new ADLPMActivity();
						if (ADL.ADL_Overdrive5_CurrentActivity_Get(AtiGpus[0], ref adlp)
						  == ADL.ADL_OK)
						{
							switch (indexCopy)
							{
								case 0:
									if (adlp.EngineClock > 0)
										return 0.01f * adlp.EngineClock;
									break;
								case 1:
									if (adlp.MemoryClock > 0)
										return 0.01f * adlp.MemoryClock;
									break;
								case 2:
									if (adlp.Vddc > 0)
										return 0.001f * adlp.Vddc;
									break;
								case 3:
									return Math.Min(adlp.ActivityPercent, 100);
							}
						}
						return 0;
					});
			}
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

					Global.logger.LogLine($"Number of adapters: {numberOfAdapters}");

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
								Global.logger.LogLine($"AdapterIndex: {i}");
								Global.logger.LogLine($"isActive: {isActive}");
								Global.logger.LogLine($"AdapterName: {adapterInfo[i].AdapterName}");
								Global.logger.LogLine($"UDID: {adapterInfo[i].UDID}");
								Global.logger.LogLine($"Present: {adapterInfo[i].Present}");
								Global.logger.LogLine($"VendorID: 0x{adapterInfo[i].VendorID}");
								Global.logger.LogLine($"BusNumber: {adapterInfo[i].BusNumber}");
								Global.logger.LogLine($"DeviceNumber: {adapterInfo[i].DeviceNumber}");
								Global.logger.LogLine($"FunctionNumber: {adapterInfo[i].FunctionNumber}");
								Global.logger.LogLine($"AdapterID: 0x{adapterID}");

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
				Global.logger.LogLine($"Error: {e}", Logging_Level.Error);
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
					Global.logger.LogLine($"Version: {version}");
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
						Global.logger.LogLine($"Status: {status}");
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

				Global.logger.LogLine($"Number of GPUs: {count}");

				for (int i = 0; i < count; i++)
				{
					NvDisplayHandle displayHandle;
					displayHandles.TryGetValue(handles[i], out displayHandle);
					ngpus.Add(new KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>(handles[i], displayHandle));
				}
			}
			return ngpus.ToArray();
		}

		public GpuPerformance()
		{
			if (NvidiaGpus.Length > 0)
			{
				nvidiaGpuClock = new NvidiaGpuClock();
				nvidiaGpuUsage = new NvidiaGpuUsage();
				nvidiaGpuMemory = new NvidiaGpuMemory();
			}
			else if (AtiGpus.Length > 0)
			{
				atiGpuActivity = new AtiGpuActivity();
			}

			if (NvidiaGpus.Length > 0 || AtiGpus.Length > 0)
			{
				gpuFanRpm = new GpuFanRpm();
				gpuFanUsage = new GpuFanUsage();
				gpuTemperature = new GpuTemperature();
			}
		}

		public float FanRpm(bool easing = true)
		{
			return gpuFanRpm?.GetValue(easing) ?? 0;
		}

		public float FanUsage(bool easing = true)
		{
			return gpuFanUsage?.GetValue(easing) ?? 0;
		}

		public float Temperature(bool easing = true)
		{
			return gpuTemperature?.GetValue(easing) ?? 0;
		}

		public float CoreClock(bool easing = true)
		{
			return nvidiaGpuClock?.GetValue(easing)[0] ??
				atiGpuActivity?.GetValue(easing)[0] ?? 0;
		}

		public float MemoryClock(bool easing = true)
		{
			return nvidiaGpuClock?.GetValue(easing)[1] ??
				atiGpuActivity?.GetValue(easing)[1] ?? 0;
		}

		public float ShaderClock(bool easing = true)
		{
			return nvidiaGpuClock?.GetValue(easing)[2] ?? 0;
		}

		public float CoreVoltage(bool easing = true)
		{
			return atiGpuActivity?.GetValue(easing)[2] ?? 0;
		}

		public float CoreUsage(bool easing = true)
		{
			return nvidiaGpuUsage?.GetValue(easing)[0] ??
				atiGpuActivity?.GetValue(easing)[3] ?? 0;
		}

		public float MemoryControllerUsage(bool easing = true)
		{
			return nvidiaGpuUsage?.GetValue(easing)[1] ?? 0;
		}

		public float VideoEngineUsage(bool easing = true)
		{
			return nvidiaGpuUsage?.GetValue(easing)[2] ?? 0;
		}

		public float MemoryUsage(bool easing = true)
		{
			return nvidiaGpuMemory?.GetValue(easing)[0] ?? 0;
		}

		public float MemoryFree(bool easing = true)
		{
			return nvidiaGpuMemory?.GetValue(easing)[1] ?? 0;
		}

		public float MemoryUsed(bool easing = true)
		{
			return nvidiaGpuMemory?.GetValue(easing)[2] ?? 0;
		}

		public float MemoryTotal(bool easing = true)
		{
			return nvidiaGpuMemory?.GetValue(easing)[3] ?? 0;
		}

		private readonly GpuFanRpm gpuFanRpm;
		private readonly GpuFanUsage gpuFanUsage;
		private readonly GpuTemperature gpuTemperature;

		private readonly NvidiaGpuClock nvidiaGpuClock;
		private readonly NvidiaGpuUsage nvidiaGpuUsage;
		private readonly NvidiaGpuMemory nvidiaGpuMemory;

		private readonly AtiGpuActivity atiGpuActivity;


		/// <summary>
		/// Returns array with clock for Nvidia GPU
		/// 0 is Core Clock
		/// 1 is Memory Clock
		/// 2 is Shader Clock
		/// </summary>
		public class NvidiaGpuClock : EasedPerformanceCounterMultiFloat
		{
			public NvidiaGpuClock() : base(new float[3]) { }

			protected override float[] UpdateValue()
			{
				NvClocks allClocks = new NvClocks
				{
					Version = NVAPI.GPU_CLOCKS_VER,
					Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetAllClocks == null ||
					NVAPI.NvAPI_GPU_GetAllClocks(NvidiaGpus[0].Key, ref allClocks) != NvStatus.OK)
					return null;

				var values = allClocks.Clock;
				var clocks = new float[3];
				clocks[1] = 0.001f * values[8];
				if (values[30] != 0)
				{
					clocks[0] = 0.0005f * values[30];
					clocks[2] = 0.001f * values[30];
				}
				else
				{
					clocks[0] = 0.001f * values[0];
					clocks[2] = 0.001f * values[14];
				}
				return clocks;
			}
		}

		/// <summary>
		/// Returns array with load for Nvidia GPU
		/// 0 is Core Load
		/// 1 is Memory Controller Load
		/// 2 is Video Engine Load
		/// </summary>
		public class NvidiaGpuUsage : EasedPerformanceCounterMultiFloat
		{
			public NvidiaGpuUsage() : base(new float[3]) { }

			protected override float[] UpdateValue()
			{
				NvPStates states = new NvPStates
				{
					Version = NVAPI.GPU_PSTATES_VER,
					PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetPStates != null &&
				  NVAPI.NvAPI_GPU_GetPStates(NvidiaGpus[0].Key, ref states) == NvStatus.OK)
				{
					return states.PStates.Select(x => x.Present ? (float)x.Percentage : 0).ToArray();
				}

				NvUsages usages = new NvUsages
				{
					Version = NVAPI.GPU_USAGES_VER,
					Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetUsages != null &&
					NVAPI.NvAPI_GPU_GetUsages(NvidiaGpus[0].Key, ref usages) == NvStatus.OK)
				{
					return new float[] { usages.Usage[2], usages.Usage[6], usages.Usage[10] };
				}
				return new float[3];
			}
		}

		/// <summary>
		/// Returns array with memory information for Nvidia GPU
		/// 0 is Memory Usage
		/// 1 is Memory Free
		/// 2 is Memory Used
		/// 3 is Memory Total
		/// </summary>
		public class NvidiaGpuMemory : EasedPerformanceCounterMultiFloat
		{
			public NvidiaGpuMemory() : base(new float[4]) { }

			protected override float[] UpdateValue()
			{
				NvMemoryInfo memoryInfo = new NvMemoryInfo
				{
					Version = NVAPI.GPU_MEMORY_INFO_VER,
					Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetMemoryInfo != null &&
				  NVAPI.NvAPI_GPU_GetMemoryInfo(NvidiaGpus[0].Value, ref memoryInfo) ==
				  NvStatus.OK)
				{
					uint totalMemory = memoryInfo.Values[0];
					uint freeMemory = memoryInfo.Values[4];
					float usedMemory = Math.Max(totalMemory - freeMemory, 0);
					return new[]
					{
						freeMemory / 1024f,
						totalMemory / 1024f,
						usedMemory / 1024,
						100f * usedMemory / totalMemory
					};
				}
				return new float[4];
			}
		}

		/// <summary>
		/// Returns array with ATI GPU Activity info
		/// 0 is Core Clock
		/// 1 is Memory Clock
		/// 2 is Core Voltage
		/// 3 is Core Load
		/// </summary>
		public class AtiGpuActivity : EasedPerformanceCounterMultiFloat
		{
			public AtiGpuActivity() : base(new float[4]) { }

			protected override float[] UpdateValue()
			{
				var activity = new float[4];
				ADLPMActivity adlp = new ADLPMActivity();
				if (ADL.ADL_Overdrive5_CurrentActivity_Get(AtiGpus[0], ref adlp)
				  == ADL.ADL_OK)
				{
					if (adlp.EngineClock > 0)
					{
						activity[0] = 0.01f * adlp.EngineClock;
					}

					if (adlp.MemoryClock > 0)
					{
						activity[1] = 0.01f * adlp.MemoryClock;
					}

					if (adlp.Vddc > 0)
					{
						activity[2] = 0.001f * adlp.Vddc;
					}

					activity[3] = Math.Min(adlp.ActivityPercent, 100);
				}
				return activity;
			}
		}

		public class GpuTemperature : EasedPerformanceCounterFloat
		{
			protected override float UpdateValue()
			{
				if (NvidiaGpus.Length > 0)
				{
					NvGPUThermalSettings settings = new NvGPUThermalSettings
					{
						Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
						Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
						Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
					};
					if (!(NVAPI.NvAPI_GPU_GetThermalSettings != null &&
						  NVAPI.NvAPI_GPU_GetThermalSettings(NvidiaGpus[0].Key, (int)NvThermalTarget.ALL,
							  ref settings) == NvStatus.OK))
					{
						return 0;
					}

					foreach (var sensor in settings.Sensor)
					{
						if (sensor.Target == NvThermalTarget.GPU)
							return sensor.CurrentTemp;
					}
					return settings.Sensor[0].CurrentTemp;
				}
				else if (AtiGpus.Length > 0)
				{
					ADLTemperature adlt = new ADLTemperature();
					if (ADL.ADL_Overdrive5_Temperature_Get(AtiGpus[0], 0, ref adlt)
					  == ADL.ADL_OK)
					{
						return 0.001f * adlt.Temperature;
					}
				}
				return 0;
			}
		}

		public class GpuFanRpm : EasedPerformanceCounterFloat
		{
			protected override float UpdateValue()
			{
				if (NvidiaGpus.Length > 0)
				{
					int value;
					NVAPI.NvAPI_GPU_GetTachReading(NvidiaGpus[0].Key, out value);
					return value;
				}
				else if (AtiGpus.Length > 0)
				{
					ADLFanSpeedValue adlf = new ADLFanSpeedValue { SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM };
					if (ADL.ADL_Overdrive5_FanSpeed_Get(AtiGpus[0], 0, ref adlf)
						== ADL.ADL_OK)
					{
						return adlf.FanSpeed;
					}
					return 0;
				}
				return 0;
			}
		}

		public class GpuFanUsage : EasedPerformanceCounterFloat
		{
			protected override float UpdateValue()
			{
				if (NvidiaGpus.Length > 0)
				{
					NvGPUCoolerSettings settings = new NvGPUCoolerSettings
					{
						Version = NVAPI.GPU_COOLER_SETTINGS_VER,
						Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
					};
					if (NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
						NVAPI.NvAPI_GPU_GetCoolerSettings(NvidiaGpus[0].Key, 0, ref settings) == NvStatus.OK)
						return settings.Cooler[0].CurrentLevel;

					return 0;
				}
				else if (AtiGpus.Length > 0)
				{
					ADLFanSpeedValue adlf = new ADLFanSpeedValue { SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT };
					if (ADL.ADL_Overdrive5_FanSpeed_Get(AtiGpus[0], 0, ref adlf)
						== ADL.ADL_OK)
					{
						return adlf.FanSpeed;
					}
					return 0;
				}
				return 0;
			}
		}

	}
}