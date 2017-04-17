using Aurora.Profiles;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aurora.Profiles.PerformanceCounters;
using Newtonsoft.Json.Linq;
using PerformanceCounter = Aurora.Profiles.PerformanceCounters.PerformanceCounterManager.IntervalPerformanceCounter;

namespace Aurora.Profiles
{
	public class GameStateIgnoreAttribute : Attribute
	{ }

	public class RangeAttribute : Attribute
	{
		public int Start { get; set; }

		public int End { get; set; }

		public RangeAttribute(int start, int end)
		{
			Start = start;
			End = end;
		}
	}

	/// <summary>
	/// A class representing various information retaining to the game.
	/// </summary>
	public interface IGameState
	{
		/// <summary>
		/// Information about the local system
		/// </summary>
		//LocalPCInformation LocalPCInfo { get; }

		JObject _ParsedData { get; set; }
		string json { get; set; }

		String GetNode(string name);
	}

	public class GameState<T> : StringProperty<T>, IGameState where T : GameState<T>
	{
		private static LocalPCInformation _localpcinfo;

		/// <summary>
		/// Information about the local system
		/// </summary>
		public LocalPCInformation LocalPCInfo
		{
			get
			{
				if (_localpcinfo == null)
					_localpcinfo = new LocalPCInformation();

				return _localpcinfo;
			}
		}

		public JObject _ParsedData { get; set; }
		public string json { get; set; }

		/// <summary>
		/// Creates a default GameState instance.
		/// </summary>
		public GameState() : base()
		{
			json = "{}";
			_ParsedData = JObject.Parse(json);
		}

		/// <summary>
		/// Creates a GameState instance based on the passed json data.
		/// </summary>
		/// <param name="json_data">The passed json data</param>
		public GameState(string json_data) : base()
		{
			if (String.IsNullOrWhiteSpace(json_data))
				json_data = "{}";

			json = json_data;
			_ParsedData = JObject.Parse(json_data);
		}

		/// <summary>
		/// A copy constructor, creates a GameState instance based on the data from the passed GameState instance.
		/// </summary>
		/// <param name="other_state">The passed GameState</param>
		public GameState(IGameState other_state) : base()
		{
			_ParsedData = other_state._ParsedData;
			json = other_state.json;
		}

		public String GetNode(string name)
		{
			JToken value;

			if (_ParsedData.TryGetValue(name, out value))
				return value.ToString();
			else
				return "";
		}

		/// <summary>
		/// Displays the JSON, representative of the GameState data
		/// </summary>
		/// <returns>JSON String</returns>
		public override string ToString()
		{
			return json;
		}
	}

	public class GameState : GameState<GameState>
	{
		public GameState() : base() { }
		public GameState(IGameState gs) : base(gs) { }
		public GameState(string json) : base(json) { }
	}

	/// <summary>
	/// Class representing local computer information
	/// </summary>
	public sealed class LocalPCInformation : Node<LocalPCInformation>
	{
		private const int DefaultUpdateInterval = 1000;
		private static readonly NvidiaGpuInfo nvidiaGpu = new NvidiaGpuInfo();
		private static readonly AtiGpuInfo atiGpu = new AtiGpuInfo();

		private static readonly PerformanceCounters.PerformanceCounterManager PerformanceCounterManager
			= new PerformanceCounters.PerformanceCounterManager();

		private static readonly GpuPerformance GpuPerformance = new GpuPerformance();

		public sealed class TimeInfo
		{
			public static TimeInfo Instance { get; } = new TimeInfo();

			/// <summary>
			/// The current hour
			/// </summary>
			public int CurrentHour => Utils.Time.GetHours();

			/// <summary>
			/// The current minute
			/// </summary>
			public int CurrentMinute => Utils.Time.GetMinutes();

			/// <summary>
			/// The current second
			/// </summary>
			public int CurrentSecond => Utils.Time.GetSeconds();

			/// <summary>
			/// The current millisecond
			/// </summary>
			public int CurrentMillisecond => Utils.Time.GetMilliSeconds();
		}

		public TimeInfo Time => TimeInfo.Instance;

		public sealed class MemoryInfo
		{
			public static MemoryInfo Instance { get; } = new MemoryInfo();

			private static Lazy<PerformanceCounter> GetCounter(string name) =>
				new Lazy<PerformanceCounter>(()=> PerformanceCounterManager.GetCounter("Aurora Internal",
					nameof(ComputerInfo), name, DefaultUpdateInterval), LazyThreadSafetyMode.PublicationOnly);

			private static readonly Lazy<PerformanceCounter> UsedPhysicalMemoryCounter
				= GetCounter("% PhysicalMemoryUsed");

			private static readonly Lazy<PerformanceCounter> UsedVirtualMemoryCounter
				= GetCounter("% VirtualMemoryUsed");

			private static readonly Lazy<PerformanceCounter> AvailablePhysicalMemoryInMiBCounter
				= GetCounter("AvailablePhysicalMemoryInMiB");

			private static readonly Lazy<PerformanceCounter> AvailableVirtualMemoryInMiBCounter
				= GetCounter("AvailableVirtualMemoryInMiB");

			private static readonly Lazy<PerformanceCounter> TotalPhysicalMemoryInMiBCounter
				= GetCounter("TotalPhysicalMemoryInMiB");

			private static readonly Lazy<PerformanceCounter> TotalVirtualMemoryInMiBCounter
				= GetCounter("TotalVirtualMemoryInMiB");

			/// <summary>
			/// Percent Used Physical Memory
			/// </summary>
			public float UsedPhysicalMemory => UsedPhysicalMemoryCounter.Value.GetValue();

			/// <summary>
			/// Percent Used Virtual Memory
			/// </summary>
			public float UsedVirtualMemory => UsedVirtualMemoryCounter.Value.GetValue();

			/// <summary>
			/// Available Physical Memory In MiB
			/// </summary>
			public float AvailablePhysicalMemoryInMiB => AvailablePhysicalMemoryInMiBCounter.Value.GetValue();

			/// <summary>
			/// Available Virtual Memory In MiB
			/// </summary>
			public float AvailableVirtualMemoryInMiB => AvailableVirtualMemoryInMiBCounter.Value.GetValue();

			/// <summary>
			/// Total Physical Memory In MiB
			/// </summary>
			public float TotalPhysicalMemoryInMiB => TotalPhysicalMemoryInMiBCounter.Value.GetValue();

			/// <summary>
			/// Total Virtual Memory In MiB
			/// </summary>
			public float TotalVirtualMemoryInMiB => TotalVirtualMemoryInMiBCounter.Value.GetValue();
			
		}

		public MemoryInfo Memory => MemoryInfo.Instance;

		public sealed class CpuInfo
		{
			public static CpuInfo Instance { get; } = new CpuInfo();

			private static readonly Lazy<PerformanceCounter> TotalUsageCounter
				= new Lazy<PerformanceCounter>(() => PerformanceCounterManager.GetCounter("Processor",
					"% Processor Time", "_Total", DefaultUpdateInterval), LazyThreadSafetyMode.PublicationOnly);

			/// <summary>
			/// Percent Total CPU Usage
			/// </summary>
			public float TotalUsage => TotalUsageCounter.Value.GetValue();

		}

		public CpuInfo CPU => CpuInfo.Instance;

		public sealed class DiskInfo
		{
			public static DiskInfo Instance { get; } = new DiskInfo();

			private static readonly Lazy<PerformanceCounter> TotalPhysicalDiskUsageCounter
				= new Lazy<PerformanceCounter>(() => PerformanceCounterManager.GetCounter("PhysicalDisk",
					"% Disk Time", "_Total", DefaultUpdateInterval), LazyThreadSafetyMode.PublicationOnly);

			private static readonly Lazy<PerformanceCounter> TotalLogicalDiskUsageCounter
				= new Lazy<PerformanceCounter>(() => PerformanceCounterManager.GetCounter("LogicalDisk",
					"% Disk Time", "_Total", DefaultUpdateInterval), LazyThreadSafetyMode.PublicationOnly);

			private static readonly Lazy<PerformanceCounter> SystemDiskUsageCounter
				= new Lazy<PerformanceCounter>(() => PerformanceCounterManager.GetCounter("LogicalDisk", 
					"% Disk Time", Path.GetPathRoot(Environment.GetFolderPath(
						Environment.SpecialFolder.System)).Substring(0, 2), DefaultUpdateInterval), 
					LazyThreadSafetyMode.PublicationOnly);

			/// <summary>
			/// Percent Total Physical Disk Usage
			/// </summary>
			public float TotalPhysicalDiskUsage => TotalPhysicalDiskUsageCounter.Value.GetValue();

			/// <summary>
			/// Percent Total Logical Disk Usage
			/// </summary>
			public float TotalLogicalDiskUsage => TotalLogicalDiskUsageCounter.Value.GetValue();

			/// <summary>
			/// Percent System Disk Usage
			/// </summary>
			public float SystemDiskUsage => SystemDiskUsageCounter.Value.GetValue();

		}

		public DiskInfo Disk => DiskInfo.Instance;

		public sealed class GpuInfo
		{
			public static GpuInfo Instance { get; } = new GpuInfo();

			/// <summary>
			/// Percent Total Physical Disk Usage
			/// </summary>
			public float TotalPhysicalDiskUsage =>
				PerformanceCounterManager.GetCounter("PhysicalDisk", "% Disk Time",
					"_Total", DefaultUpdateInterval).GetValue();

			/// <summary>
			/// Percent Total Logical Disk Usage
			/// </summary>
			public float TotalLogicalDiskUsage =>
				PerformanceCounterManager.GetCounter("LogicalDisk", "% Disk Time",
					"_Total", DefaultUpdateInterval).GetValue();

			/// <summary>
			/// Percent System Disk Usage
			/// </summary>
			public float SystemDiskUsage =>

				PerformanceCounterManager.GetCounter("LogicalDisk", "% Disk Time",
					SystemDriveLetter, DefaultUpdateInterval).GetValue();

		}

		public GpuInfo GPU => GpuInfo.Instance;

		public sealed class NetworkInfo
		{
			public static NetworkInfo Instance { get; } = new NetworkInfo();

			private static Lazy<PerformanceCounter> GetCounter(string name) =>
				new Lazy<PerformanceCounter>(() => PerformanceCounterManager.GetCounter("Aurora Internal",
					"Default Network", name, DefaultUpdateInterval), LazyThreadSafetyMode.PublicationOnly);

			private static readonly Lazy<PerformanceCounter> BytesReceivedPerSecCounter
				= GetCounter("Bytes Received/sec");

			private static readonly Lazy<PerformanceCounter> BytesSentPerSecCounter
				= GetCounter("Bytes Sent/sec");

			private static readonly Lazy<PerformanceCounter> BytesTotalPerSecCounter
				= GetCounter("Bytes Total/sec");

			private static readonly Lazy<PerformanceCounter> CurrentBandwidthUsageCounter
				= GetCounter("Current Bandwidth");

			private static readonly Lazy<PerformanceCounter> NetworkTotalUsageCounter
				= GetCounter("% Network Total Usage");

			/// <summary>
			/// Default adapter download speed in Bytes/sec
			/// </summary>
			public float BytesReceivedPerSec => BytesReceivedPerSecCounter.Value.GetValue();

			/// <summary>
			/// Default adapter upload speed in Bytes/sec
			/// </summary>
			public float BytesSentPerSec => BytesSentPerSecCounter.Value.GetValue();

			/// <summary>
			/// Default adapter total speed in Bytes/sec
			/// </summary>
			public float BytesTotalPerSec => BytesTotalPerSecCounter.Value.GetValue();

			/// <summary>
			/// Default adapter current bandwidth
			/// </summary>
			public float CurrentBandwidth => CurrentBandwidthUsageCounter.Value.GetValue();

			/// <summary>
			/// Default adapter total usage percent
			/// </summary>
			public float NetworkTotalUsage => NetworkTotalUsageCounter.Value.GetValue();

		}

		public NetworkInfo Network => NetworkInfo.Instance;

		public class NvidiaGpuInfo
		{
			/// <summary>
			/// Percent Total CPU Usage
			/// </summary>
			public float TotalUsage(int updateInterval) =>
				PerformanceCounterManager.GetCounter("Processor", "% Processor Time",
					"_Total", updateInterval).GetValue();

			/// <summary>
			/// Percent Per Core CPU Usage
			/// </summary>
			public float PerCoreUsage(int core, int updateInterval) =>
				PerformanceCounterManager.GetCounter("Processor", "% Processor Time",
					core.ToString(), updateInterval).GetValue();
		}

		public NvidiaGpuInfo NvidiaGpu => nvidiaGpu;

		public class AtiGpuInfo
		{
			/// <summary>
			/// Percent Total CPU Usage
			/// </summary>
			public float TotalUsage(int updateInterval) =>
				PerformanceCounterManager.GetCounter("Processor", "% Processor Time",
					"_Total", updateInterval).GetValue();

			/// <summary>
			/// Percent Per Core CPU Usage
			/// </summary>
			public float PerCoreUsage(int core, int updateInterval) =>
				PerformanceCounterManager.GetCounter("Processor", "% Processor Time",
					core.ToString(), updateInterval).GetValue();
		}

		public AtiGpuInfo AtiGpu => atiGpu;

		/// <summary>
		/// Current GPU Fan speed in rpm
		/// </summary>
		public float GpuFanRpm => GpuPerformance.FanRpm();

		/// <summary>
		/// Current GPU Fan speed in percents
		/// </summary>
		public float GpuFanUsage => GpuPerformance.FanUsage();

		/// <summary>
		/// Current GPU Core clock
		/// </summary>
		public float GpuCoreClock => GpuPerformance.CoreClock();

		/// <summary>
		/// Current GPU Memory clock
		/// </summary>
		public float GpuMemoryClock => GpuPerformance.MemoryClock();

		/// <summary>
		/// Current GPU Shader clock. NVidia only.
		/// </summary>
		public float GpuShaderClock => GpuPerformance.ShaderClock();

		/// <summary>
		/// Current GPU Core voltage. ATI only.
		/// </summary>
		public float GpuCoreVoltage => GpuPerformance.CoreVoltage();

		private static ExpandoObject gpu = new ExpandoObject();

		public ExpandoObject GPU => gpu;


		static LocalPCInformation()
		{
			ss = new ExpandoObject();
			((dynamic)ss).Name = 66f;
			((dynamic)gpu).Name = (Func<string, float>)((x) => 34f);

		}

		private static ExpandoObject ss;
		public ExpandoObject aaa => ss;

		public class Test : Node<Test>
		{
			float r = 70f;
			public Test()
			{
				//if (!PropertyLookup.ContainsKey("Core0"))
				//PropertyLookup.Add("Core0", new Tuple<Func<Test, object>, Action<Test, object>>(
				//	test => test.r, (test, o) => test.r = (float)o));
			}

			public float Shit1(bool rr)
			{
				return 2f;
			}
			public float Shit2(bool rr, bool r2r)
			{
				return 2f;
			}
			public float Get => 56f;
			public float Get2()
			{
				return 56f;
			}
			public float Get3(bool rr)
			{
				return 56f;
			}
			public float Gdet = 56f;

			[Range(0, 1)]

			public static readonly float[] cores = new[] { 4f, 5f };

			public class Test4
			{
				public float Val() => 55f;
			}
		}
		public class Test22 : IStringProperty
		{
			private int a = 33;
			public object GetValueFromString(string name, object input = null)
			{
				return a;
			}

			public void SetValueFromString(string name, object value)
			{
				a = (int)value;
			}

			public IStringProperty Clone()
			{
				throw new NotImplementedException();
			}
		}

		public IStringProperty TEst2
		{
			get { return new Test22(); }
		}

		public dynamic Test1
		{
			get
			{
				dynamic obj = new ExpandoObject();
				obj.Name = 22f;
				obj.Get2 = new Func<float>(() => 34f);
				return obj;
			}
		}

		public Test Test3
		{
			get { return new Test(); }
		}

		/// <summary>
		/// Current GPU Core usage
		/// </summary>
		public float GpuCoreUsage => GpuPerformance.CoreUsage();

		/// <summary>
		/// Current GPU Memory Controller usage. NVidia only.
		/// </summary>
		public float GpuMemoryControllerUsage => GpuPerformance.MemoryControllerUsage();

		/// <summary>
		/// Current GPU Video Engine usage. NVidia only.
		/// </summary>
		public float GpuVideoEngineUsage => GpuPerformance.VideoEngineUsage();

		/// <summary>
		/// Current GPU Memory usage in percents. NVidia only.
		/// </summary>
		public float GpuMemoryUsage => GpuPerformance.MemoryUsage();

		/// <summary>
		/// Current GPU Free memory. NVidia only.
		/// </summary>
		public float GpuMemoryFree => GpuPerformance.MemoryFree();

		/// <summary>
		/// Current GPU Used memory. NVidia only.
		/// </summary>
		public float GpuMemoryUsed => GpuPerformance.MemoryUsed();

		/// <summary>
		/// Current GPU Total memory. NVidia only.
		/// </summary>
		public float GpuMemoryTotal => GpuPerformance.MemoryTotal();

	}


}
