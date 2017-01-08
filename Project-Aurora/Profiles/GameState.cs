using Aurora.Profiles;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Aurora.Profiles.PerformanceCounters;
using Newtonsoft.Json.Linq;

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
	public class LocalPCInformation : Node<LocalPCInformation>
	{
		private static readonly TimeInfo time = new TimeInfo();
		private static readonly MemoryInfo memory = new MemoryInfo();
		private static readonly CpuInfo cpu = new CpuInfo();
		private static readonly NvidiaGpuInfo nvidiaGpu = new NvidiaGpuInfo();
		private static readonly AtiGpuInfo atiGpu = new AtiGpuInfo();
		private static readonly PhysicalDiskInfo physicalDisk = new PhysicalDiskInfo();

		private static readonly PerformanceCounters.PerformanceCounterManager PerformanceCounterManager 
			= new PerformanceCounters.PerformanceCounterManager();

		private static readonly CpuTotal CpuTotal = new CpuTotal();
		private static readonly CpuPerCore CpuPerCore = new CpuPerCore();
		private static readonly GpuPerformance GpuPerformance = new GpuPerformance();

		public class TimeInfo
		{
			protected static readonly TimeInfo Instance = new TimeInfo();

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

		public TimeInfo Time => time;

		public class MemoryInfo
		{
			protected static readonly MemoryInfo Instance = new MemoryInfo();

			/// <summary>
			/// Percent Used Physical Memory
			/// </summary>
			public float UsedPhysicalMemory(int updateInterval) => 
				PerformanceCounterManager.GetCounter("Aurora Internal", 
					nameof(ComputerInfo), "% PhysicalMemoryUsed", updateInterval).GetValue();

			/// <summary>
			/// Percent Used Virtual Memory
			/// </summary>
			public float UsedVirtualMemory(int updateInterval) =>
				PerformanceCounterManager.GetCounter("Aurora Internal",
					nameof(ComputerInfo), "% VirtualMemoryUsed", updateInterval).GetValue();
		}

		public MemoryInfo Memory => memory;

		public class CpuInfo
		{
			protected static readonly CpuInfo Instance = new CpuInfo();

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

		public CpuInfo CPU => cpu;

		public class PhysicalDiskInfo
		{
			protected static readonly PhysicalDiskInfo Instance = new PhysicalDiskInfo();

			/// <summary>
			/// Percent Total Physical Disk Usage
			/// </summary>
			public float TotalPhysicalDiskUsage(int updateInterval) =>
				PerformanceCounterManager.GetCounter("PhysicalDisk", "% Disk Time",
					"_Total", updateInterval).GetValue();

			/// <summary>
			/// Percent Per Core CPU Usage
			/// </summary>
			public float PerCoreUsage(int core, int updateInterval) =>
				PerformanceCounterManager.GetCounter("Processor", "% Processor Time",
					core.ToString(), updateInterval).GetValue();
		}

	//	public PhysicalDiskInfo PhysicalDisk => disk;

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
