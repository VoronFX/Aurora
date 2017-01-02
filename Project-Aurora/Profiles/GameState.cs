using Aurora.Profiles;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private static readonly CpuTotal CpuTotal = new CpuTotal();
		private static readonly CpuPerCore CpuPerCore = new CpuPerCore();
		private static readonly Memory Memory = new Memory();
		private static readonly GpuPerformance GpuPerformance = new GpuPerformance();

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

		/// <summary>
		/// Used RAM
		/// </summary>
		public long MemoryUsed => Memory.GetTotalMemoryInMiB() - Memory.GetPhysicalAvailableMemoryInMiB();

		/// <summary>
		/// Available RAM
		/// </summary>
		public long MemoryFree => Memory.GetPhysicalAvailableMemoryInMiB();

		/// <summary>
		/// Total RAM
		/// </summary>
		public long MemoryTotal => Memory.GetTotalMemoryInMiB();

		/// <summary>
		/// Current CPU Usage
		/// </summary>
		public float CPUUsage => CpuTotal.GetValue();

		/// <summary>
		/// Current CPU Usage per core
		/// </summary>
		public float[] CpuCoresUsage => CpuPerCore.GetValue();

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
