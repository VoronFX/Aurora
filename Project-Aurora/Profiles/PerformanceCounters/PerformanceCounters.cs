using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Aurora.Devices;
using Microsoft.VisualBasic.Devices;
using OpenHardwareMonitor.Hardware.ATI;
using OpenHardwareMonitor.Hardware.Nvidia;

namespace Aurora.Profiles.PerformanceCounters
{
	class AuroraInternal
	{
		public const string CategoryName = "Aurora Internal";

		public class Disk
		{
			public static void Register()
			{
				var performanceCounter = new Lazy<Func<float>>(() =>
				PerformanceCounterManager.GetSystemPerformanceCounter("LogicalDisk", "% Disk Time",
						Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)).Substring(0, 2)),
						LazyThreadSafetyMode.PublicationOnly);

				PerformanceCounterManager.RegisterInternal(CategoryName, "System Disk",
					"% Usage", () => performanceCounter.Value());
			}
		}

		public class Memory
		{
			public static void Register()
			{
				var propNames = new[] { "AvailablePhysicalMemoryInMiB", "AvailableVirtualMemoryInMiB",
				"TotalPhysicalMemoryInMiB", "TotalVirtualMemoryInMiB", "% PhysicalMemoryUsed", "% VirtualMemoryUsed" };
				for (var i = 0; i < propNames.Length; i++)
				{
					var indexCopy = i;

					PerformanceCounterManager.RegisterInternal(CategoryName, nameof(ComputerInfo),
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
									return (float) ((computerInfo.TotalPhysicalMemory - computerInfo.AvailablePhysicalMemory) * 100d / computerInfo.TotalPhysicalMemory);
								case 5:
									return (float) ((computerInfo.TotalVirtualMemory - computerInfo.AvailableVirtualMemory) * 100d / computerInfo.TotalVirtualMemory);
							}
							return 0;
						});
				}
			}
		}

		public class Network
		{
			public static void Register()
			{
				var propNames = new[] { "Bytes Received/sec", "Bytes Sent/sec",
				"Bytes Total/sec", "Current Bandwidth", "% Network Total Usage" };

				var defaultAdapterName = new Lazy<string>(() =>
				{
					UdpClient u = new UdpClient(Dns.GetHostName(), 1);
					string localAddr = ((IPEndPoint)u.Client.LocalEndPoint).Address.ToString();

					return NetworkInterface.GetAllNetworkInterfaces()
						.Where(netInt => netInt.OperationalStatus == OperationalStatus.Up)
						.FirstOrDefault(netInt =>
							netInt.GetIPProperties().UnicastAddresses.Any(
								uni => uni.Address.ToString() == localAddr))?.Name;

				}, LazyThreadSafetyMode.PublicationOnly);

				var counters = new[] { "Bytes Received/sec", "Bytes Sent/sec",
				"Bytes Total/sec", "Current Bandwidth" }.Select(x =>
				new Lazy<Func<float>>(() => PerformanceCounterManager.GetSystemPerformanceCounter(
					"Network Adapter", x, defaultAdapterName.Value))).ToArray();

				for (var i = 0; i < propNames.Length; i++)
				{
					var indexCopy = i;

					PerformanceCounterManager.RegisterInternal(CategoryName, "Default Network",
						propNames[i], () =>
						{
							if (indexCopy == counters.Length)
							{
								return counters[indexCopy - 2].Value() * 100 / counters[indexCopy - 1].Value();
							}

							return counters[indexCopy].Value();
						});
				}
			}
		}

		public class Gpu
		{
			private static readonly KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle>[] NvidiaGpus;
			private static readonly int[] AtiGpus;
			public static string InitLog;

			static Gpu()
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
				bool registerGeneral = true;

				for (var i = 0; i < NvidiaGpus.Length; i++)
				{
					RegisterNvidiaGpuCounters(NvidiaGpus[i], $"NvidiaGpu #{i}", false);

					if (registerGeneral)
					{
						RegisterNvidiaGpuCounters(NvidiaGpus[i], "GPU", true);
						registerGeneral = false;
					}
				}

				for (var i = 0; i < AtiGpus.Length; i++)
				{
					RegisterAtiGpuCounters(AtiGpus[i], $"AtiGpu #{i}", false);

					if (registerGeneral)
					{
						RegisterAtiGpuCounters(AtiGpus[i], "GPU", true);
						registerGeneral = false;
					}
				}
			}

			private static void RegisterAtiGpuCounters(int gpu, string counterName, bool general)
			{
				Action<string, Func<float>> register = (name, newSample) =>
				PerformanceCounterManager.RegisterInternal(CategoryName, counterName, name, newSample);

				register("FanRpm", () => GetAtiFanSpeed(gpu, FanSpeedType.Rpm));
				register("% FanUsage", () => GetAtiFanSpeed(gpu, FanSpeedType.Percent));

				register("Temperature", () => GetAtiTemperature(gpu));

				register("Core Clock", () => GetAtiActivity(gpu, AtiActivityType.CoreClock));
				register("Memory Clock", () => GetAtiActivity(gpu, AtiActivityType.MemoryClock));

				if (general)
				{
					register("% Load", () => GetAtiActivity(gpu, AtiActivityType.LoadCorePercent));
				}
				else
				{
					register("Core Voltage", () => GetAtiActivity(gpu, AtiActivityType.CoreVoltage));
					register("% Load Core", () => GetAtiActivity(gpu, AtiActivityType.LoadCorePercent));
				}
			}

			private static void RegisterNvidiaGpuCounters(KeyValuePair<NvPhysicalGpuHandle, NvDisplayHandle> gpu,
				string counterName, bool general)
			{
				Action<string, Func<float>> register = (name, newSample) =>
				PerformanceCounterManager.RegisterInternal(CategoryName, counterName, name, newSample);

				register("FanRpm", () => GetNvidiaFanSpeed(gpu.Key, FanSpeedType.Rpm));
				register("% FanUsage", () => GetNvidiaFanSpeed(gpu.Key, FanSpeedType.Percent));

				if (general)
				{
					register("Temperature", () => GetNvidiaTemperature(gpu.Key, NvThermalTarget.GPU));

					register("Core Clock", () => GetNvidiaClock(gpu.Key, NvidiaClockType.Core));
					register("Memory Clock", () => GetNvidiaClock(gpu.Key, NvidiaClockType.Memory));

					register("% Load", () => GetNvidiaUsage(gpu.Key, NvidiaUsageType.Core));
				}
				else
				{
					register("Temperature Board", () => GetNvidiaTemperature(gpu.Key, NvThermalTarget.BOARD));
					register("Temperature GPU", () => GetNvidiaTemperature(gpu.Key, NvThermalTarget.GPU));
					register("Temperature Memory", () => GetNvidiaTemperature(gpu.Key, NvThermalTarget.MEMORY));
					register("Temperature Power Supply", () => GetNvidiaTemperature(gpu.Key, NvThermalTarget.POWER_SUPPLY));

					register("Clock Core", () => GetNvidiaClock(gpu.Key, NvidiaClockType.Core));
					register("Clock Memory", () => GetNvidiaClock(gpu.Key, NvidiaClockType.Memory));
					register("Clock Shader", () => GetNvidiaClock(gpu.Key, NvidiaClockType.Shader));

					register("% Load Core", () => GetNvidiaUsage(gpu.Key, NvidiaUsageType.Core));
					register("% Load Memory Controller", () => GetNvidiaUsage(gpu.Key, NvidiaUsageType.MemoryController));
					register("% Load Video Engine", () => GetNvidiaUsage(gpu.Key, NvidiaUsageType.VideoEngine));

					register("Memory Free", () => GetNvidiaMemory(gpu.Value, NvidiaMemory.Free));
					register("Memory Used", () => GetNvidiaMemory(gpu.Value, NvidiaMemory.Used));
					register("Memory Total", () => GetNvidiaMemory(gpu.Value, NvidiaMemory.Total));
					register("% Memory Usage", () => GetNvidiaMemory(gpu.Value, NvidiaMemory.Usage));
				}
			}

			private enum FanSpeedType { Rpm, Percent }

			private static float GetNvidiaFanSpeed(NvPhysicalGpuHandle gpu, FanSpeedType speedType)
			{
				switch (speedType)
				{
					case FanSpeedType.Rpm:
						int value;
						NVAPI.NvAPI_GPU_GetTachReading(gpu, out value);
						return value;
					case FanSpeedType.Percent:
						NvGPUCoolerSettings settings = new NvGPUCoolerSettings
						{
							Version = NVAPI.GPU_COOLER_SETTINGS_VER,
							Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
						};
						if (NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
								NVAPI.NvAPI_GPU_GetCoolerSettings(gpu, 0, ref settings) == NvStatus.OK)
							return settings.Cooler[0].CurrentLevel;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(speedType), speedType, null);
				}
				return 0;
			}

			private static float GetNvidiaTemperature(NvPhysicalGpuHandle gpu, NvThermalTarget sensorTarget)
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
					return settings.Sensor.FirstOrDefault(s => s.Target == sensorTarget).CurrentTemp;
				}
				return 0;
			}

			private enum NvidiaUsageType
			{
				Core = 0,
				MemoryController = 1,
				VideoEngine = 2
			}

			private static float GetNvidiaUsage(NvPhysicalGpuHandle gpu, NvidiaUsageType usageType)
			{
				NvPStates states = new NvPStates
				{
					Version = NVAPI.GPU_PSTATES_VER,
					PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetPStates != null &&
					NVAPI.NvAPI_GPU_GetPStates(gpu, ref states) == NvStatus.OK)
				{
					return states.PStates[(int)usageType].Present ? (float)states.PStates[(int)usageType].Percentage : 0;
				}

				NvUsages usages = new NvUsages
				{
					Version = NVAPI.GPU_USAGES_VER,
					Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetUsages != null &&
					NVAPI.NvAPI_GPU_GetUsages(gpu, ref usages) == NvStatus.OK)
				{
					switch (usageType)
					{
						case NvidiaUsageType.Core:
							return usages.Usage[2];
						case NvidiaUsageType.MemoryController:
							return usages.Usage[6];
						case NvidiaUsageType.VideoEngine:
							return usages.Usage[10];
					}
				}
				return 0;
			}

			private enum NvidiaClockType
			{
				Core = 0,
				Memory = 1,
				Shader = 2
			}

			private static float GetNvidiaClock(NvPhysicalGpuHandle gpu, NvidiaClockType clockType)
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
				return clocks[(int)clockType];
			}

			private enum NvidiaMemory { Free, Used, Total, Usage }

			private static float GetNvidiaMemory(NvDisplayHandle gpu, NvidiaMemory type)
			{
				NvMemoryInfo memoryInfo = new NvMemoryInfo
				{
					Version = NVAPI.GPU_MEMORY_INFO_VER,
					Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
				};
				if (NVAPI.NvAPI_GPU_GetMemoryInfo != null &&
				  NVAPI.NvAPI_GPU_GetMemoryInfo(gpu, ref memoryInfo) ==
				  NvStatus.OK)
				{
					uint totalMemory = memoryInfo.Values[0];
					uint freeMemory = memoryInfo.Values[4];
					float usedMemory = Math.Max(totalMemory - freeMemory, 0);
					switch (type)
					{
						case NvidiaMemory.Free:
							return freeMemory / 1024f;
						case NvidiaMemory.Used:
							return usedMemory / 1024;
						case NvidiaMemory.Total:
							return totalMemory / 1024f;
						case NvidiaMemory.Usage:
							return 100f * usedMemory / totalMemory;
					}
				}
				return 0;
			}

			private static float GetAtiFanSpeed(int gpu, FanSpeedType speedType)
			{
				ADLFanSpeedValue adlf = new ADLFanSpeedValue
				{
					SpeedType = speedType == FanSpeedType.Rpm ?
					ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM : ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT
				};
				if (ADL.ADL_Overdrive5_FanSpeed_Get(gpu, 0, ref adlf)
					== ADL.ADL_OK)
				{
					return adlf.FanSpeed;
				}
				return 0;
			}

			private static float GetAtiTemperature(int gpu)
			{
				ADLTemperature adlt = new ADLTemperature();
				if (ADL.ADL_Overdrive5_Temperature_Get(gpu, 0, ref adlt)
				  == ADL.ADL_OK)
				{
					return 0.001f * adlt.Temperature;
				}
				return 0;
			}

			private enum AtiActivityType { CoreClock, MemoryClock, CoreVoltage, LoadCorePercent }

			private static float GetAtiActivity(int gpu, AtiActivityType activityType)
			{
				ADLPMActivity adlp = new ADLPMActivity();
				if (ADL.ADL_Overdrive5_CurrentActivity_Get(gpu, ref adlp)
				  == ADL.ADL_OK)
				{
					switch (activityType)
					{
						case AtiActivityType.CoreClock:
							if (adlp.EngineClock > 0)
								return 0.01f * adlp.EngineClock;
							break;
						case AtiActivityType.MemoryClock:
							if (adlp.MemoryClock > 0)
								return 0.01f * adlp.MemoryClock;
							break;
						case AtiActivityType.CoreVoltage:
							if (adlp.Vddc > 0)
								return 0.001f * adlp.Vddc;
							break;
						case AtiActivityType.LoadCorePercent:
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

		}
	}
}