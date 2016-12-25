using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Aurora.Devices;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{

	public class GpuLoad
	{
		public string ID = "GpuLoad";

		public KeySequence DefaultKeys = new KeySequence();

		// Independant RainbowLoop
		public static ColorSpectrum RainbowLoop = new ColorSpectrum(
			Color.FromArgb(255, 0, 0),
			Color.FromArgb(255, 127, 0),
			Color.FromArgb(255, 255, 0),
			Color.FromArgb(0, 255, 0),
			Color.FromArgb(0, 0, 255),
			Color.FromArgb(75, 0, 130),
			Color.FromArgb(139, 0, 255),
			Color.FromArgb(255, 0, 0)
			);

		static readonly ColorSpectrum LoadGradient =
			new ColorSpectrum(Color.FromArgb(0, Color.Lime), Color.Lime, Color.Orange, Color.Red);

		static readonly ColorSpectrum BlinkingSpectrum =
			new ColorSpectrum(Color.Black, Color.FromArgb(0, Color.Black), Color.Black);

		private static readonly DeviceKeys[] GpuRainbowKeys =
			{ DeviceKeys.PRINT_SCREEN, DeviceKeys.SCROLL_LOCK, DeviceKeys.PAUSE_BREAK };

		public EffectLayer[] UpdateLights(ScriptSettings settings, GameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			EffectLayer GPULayer = new EffectLayer(ID + " - GpuLoad");
			EffectLayer GPUBlinkingLayer = new EffectLayer(ID + " - GpuBlinkingLoad");
			EffectLayer GPURainbowLayer = new EffectLayer(ID + " - GpuRainbowLoad");

			// G5-G1 buttons
			var freeFormRect = new Rectangle(-45, 35, 35, 181);

			var rectangle = new RectangleF(
						(float)Math.Round((freeFormRect.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width),
						(float)Math.Round((freeFormRect.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height),
						(float)Math.Round(freeFormRect.Width * Effects.editor_to_canvas_width),
						(float)Math.Round(freeFormRect.Height * Effects.editor_to_canvas_height));

			var gpuLoad = GpuLoadCounter.EasedGpuLoad[0];

			var gpuOverload = (gpuLoad - 95) / 5f;
			gpuOverload = Math.Max(0, Math.Min(1, gpuOverload));

			var loadGradient = new ColorSpectrum(Color.Red, Color.Orange, Color.Lime)
				.ToLinearGradient(rectangle.Width, rectangle.Height, rectangle.X, rectangle.Y);

			loadGradient.WrapMode = WrapMode.TileFlipXY;

			var blinkColor = BlinkingSpectrum.GetColorAt((Utils.Time.GetMillisecondsSinceEpoch() % 1000) / 1000.0f);

			var barheight = rectangle.Height * gpuLoad / 100;

			using (var g = GPULayer.GetGraphics())
			{
				g.FillRectangle(loadGradient,
					new RectangleF(rectangle.Left, rectangle.Top + rectangle.Height - barheight, rectangle.Width, barheight));
			}

			var blinkGradient = new ColorSpectrum(Color.FromArgb((byte)(blinkColor.A * gpuOverload), blinkColor), Color.FromArgb(0, Color.Black))
				.ToLinearGradient(rectangle.Width, rectangle.Height, rectangle.X, rectangle.Y);
			blinkGradient.WrapMode = WrapMode.TileFlipXY;

			using (var g = GPUBlinkingLayer.GetGraphics())
			{
				g.FillRectangle(blinkGradient,
					new RectangleF(rectangle.Left, rectangle.Top + rectangle.Height - barheight, rectangle.Width, barheight));
			}


			RainbowLoop.Shift((float)(-0.003 + -0.01 * gpuLoad / 100f));

			for (int i = 0; i < GpuRainbowKeys.Length; i++)
			{
				GPURainbowLayer.Set(GpuRainbowKeys[i],
					Color.FromArgb((byte)(255 * gpuLoad / 100f),
					RainbowLoop.GetColorAt(i, GpuRainbowKeys.Length)));
			}

			layers.Enqueue(GPULayer);
			layers.Enqueue(GPUBlinkingLayer);
			layers.Enqueue(GPURainbowLayer);

			return layers.ToArray();
		}

		private static class GpuLoadCounter
		{
			public static int UpdateEvery { get; set; }

			private static float[] prevGpuLoad = new float[3];
			private static float[] easedGpuLoad = new float[3];
			private static float[] targetGpuLoad = new float[3];
			private static int[] newGpuLoad;

			private static long updateTime = Utils.Time.GetMillisecondsSinceEpoch();

			private static int usage = 1;
			private static TaskCompletionSource<bool> sleeping;

			static GpuLoadCounter()
			{
				UpdateEvery = 1000;
			}


			private static readonly Task Updater = Task.Run((Action)(async () =>
			{
				while (true)
				{
					try
					{
						if (!NVAPI.IsAvailable)
						{
							Global.logger.LogLine("NVAPI is not available!", Logging_Level.Error);
							return;
						}

						int[] loads = new int[3];

						while (true)
						{
							usage--;
							if (usage <= 0)
							{
								sleeping = new TaskCompletionSource<bool>();
								await sleeping.Task;
								sleeping = null;
							}

							NVAPI.NvPStates states = new NVAPI.NvPStates();
							states.Version = NVAPI.GPU_PSTATES_VER;
							states.PStates = new NVAPI.NvPState[NVAPI.MAX_PSTATES_PER_GPU];
							if (NVAPI.NvAPI_GPU_GetPStates != null &&
								NVAPI.NvAPI_GPU_GetPStates(NVAPI.handles[0], ref states) == NVAPI.NvStatus.OK)
							{
								for (int i = 0; i < 3; i++)
									if (states.PStates[i].Present)
									{
										loads[i] = states.PStates[i].Percentage;
									}
							}
							else
							{
								NVAPI.NvUsages usages = new NVAPI.NvUsages();
								usages.Version = NVAPI.GPU_USAGES_VER;
								usages.Usage = new uint[NVAPI.MAX_USAGES_PER_GPU];
								if (NVAPI.NvAPI_GPU_GetUsages != null &&
									NVAPI.NvAPI_GPU_GetUsages(NVAPI.handles[0], ref usages) == NVAPI.NvStatus.OK)
								{
									loads[0] = (int)usages.Usage[2];
									loads[1] = (int)usages.Usage[6];
									loads[2] = (int)usages.Usage[10];
								}
							}

							newGpuLoad = loads;

							await Task.Delay(UpdateEvery);
						}
					}
					catch (Exception exc)
					{
						Global.logger.LogLine("PerformanceCounter exception: " + exc, Logging_Level.Error);
					}
					await Task.Delay(500);
				}
			}));

			/// <summary>
			/// Returns eased gpu load
			/// 0 is GPU Core
			/// 1 is GPU Memory Controller
			/// 2 is GPU GPU Video Engine
			/// </summary>
			public static float[] EasedGpuLoad
			{
				get
				{
					usage = 5;

					var sleepingcopy = sleeping;
					if (sleepingcopy != null)
						sleepingcopy.SetResult(true);

					if (newGpuLoad != null)
					{
						prevGpuLoad = (float[])easedGpuLoad.Clone();
						targetGpuLoad = new float[3];
						for (int i = 0; i < newGpuLoad.Length; i++)
						{
							targetGpuLoad[i] = newGpuLoad[i];
						}
						newGpuLoad = null;
						updateTime = Utils.Time.GetMillisecondsSinceEpoch();
					}

					for (int i = 0; i < easedGpuLoad.Length; i++)
					{
						easedGpuLoad[i] = prevGpuLoad[i] + (targetGpuLoad[i] - prevGpuLoad[i]) *
							Math.Min((Utils.Time.GetMillisecondsSinceEpoch() - updateTime) / (float)UpdateEvery, 1f);
					}
					return easedGpuLoad;
				}
			}
		}

	}

	internal class NVAPI
	{
		// These pieces of code are taken from OpenHardwareMonitor project
		// https://github.com/openhardwaremonitor/openhardwaremonitor

		public const int MAX_USAGES_PER_GPU = 33;
		public const int MAX_PHYSICAL_GPUS = 64;
		public static readonly uint GPU_USAGES_VER = (uint)Marshal.SizeOf(typeof(NvUsages)) | 0x10000;
		public const int MAX_PSTATES_PER_GPU = 8;
		public static readonly uint GPU_PSTATES_VER = (uint)Marshal.SizeOf(typeof(NvPStates)) | 0x10000;

		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		internal struct NvUsages
		{
			public uint Version;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_USAGES_PER_GPU)]
			public uint[] Usage;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct NvPhysicalGpuHandle
		{
			private readonly IntPtr ptr;
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
		internal struct NvPState
		{
			public bool Present;
			public int Percentage;
		}

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

		public static readonly NvAPI_GPU_GetPStatesDelegate NvAPI_GPU_GetPStates;
		public delegate NvStatus NvAPI_GPU_GetPStatesDelegate(
			NvPhysicalGpuHandle gpuHandle, ref NvPStates nvPStates);
		public delegate NvStatus NvAPI_GPU_GetUsagesDelegate(
			NvPhysicalGpuHandle gpuHandle, ref NvUsages nvUsages);
		private delegate NvStatus NvAPI_InitializeDelegate();
		private delegate IntPtr nvapi_QueryInterfaceDelegate(uint id);
		public delegate NvStatus NvAPI_EnumPhysicalGPUsDelegate(
			[Out] NvPhysicalGpuHandle[] gpuHandles, out int gpuCount);

		private static readonly nvapi_QueryInterfaceDelegate nvapi_QueryInterface;
		public static readonly NvAPI_GPU_GetUsagesDelegate NvAPI_GPU_GetUsages;
		private static readonly NvAPI_InitializeDelegate NvAPI_Initialize;
		public static readonly NvAPI_EnumPhysicalGPUsDelegate NvAPI_EnumPhysicalGPUs;

		private static readonly IDictionary<Pair<DllImportAttribute, Type>, Type> wrapperTypes =
			new Dictionary<Pair<DllImportAttribute, Type>, Type>();

		public struct Pair<F, S>
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

		private static readonly ModuleBuilder moduleBuilder =
		  AppDomain.CurrentDomain.DefineDynamicAssembly(
			new AssemblyName("PInvokeDelegateFactoryInternalAssembly"),
			AssemblyBuilderAccess.Run).DefineDynamicModule(
			"PInvokeDelegateFactoryInternalModule");

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

		public static NvPhysicalGpuHandle[] handles =
				new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];

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
				GetDelegate(0xE5AC921F, out NvAPI_EnumPhysicalGPUs);
				GetDelegate(0x189A1FDF, out NvAPI_GPU_GetUsages);
				available = true;
			}

			int count;
			if (NVAPI.NvAPI_EnumPhysicalGPUs == null)
			{
				Global.logger.LogLine("Error: NvAPI_EnumPhysicalGPUs not available");
				return;
			}
			else
			{
				NvStatus status = NVAPI.NvAPI_EnumPhysicalGPUs(handles, out count);
				if (status != NvStatus.OK)
				{
					Global.logger.LogLine("Status: " + status);
					return;
				}
			}
		}


		private static readonly bool available;

		public static bool IsAvailable
		{
			get { return available; }
		}
	}
}