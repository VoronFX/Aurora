using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Aurora.Devices;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{

	public class CpuCoresUsage
	{
		public string ID = "CpuCoresUsage";

		public KeySequence DefaultKeys = new KeySequence();

		static readonly ColorSpectrum loadGradient = new ColorSpectrum(Color.FromArgb(0, Color.Lime), Color.Lime, Color.Red);
		static readonly ColorSpectrum blinking = new ColorSpectrum(Color.Black, Color.FromArgb(0, Color.Black), Color.Black);
		static readonly DeviceKeys[] RainbowCircleKeys = {
			DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT,
			DeviceKeys.NUM_NINE, DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, GameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			//	EffectLayer layer = new EffectLayer(this.ID);
			//	layer.PercentEffect(Color.Purple, Color.Green,
			//		new KeySequence(new[]
			//		{
			//			DeviceKeys.F12, DeviceKeys.F11, DeviceKeys.F10, DeviceKeys.F9, DeviceKeys.F8, DeviceKeys.F7, DeviceKeys.F6,
			//			DeviceKeys.F5, DeviceKeys.F4, DeviceKeys.F3, DeviceKeys.F2, DeviceKeys.F1
			//		}), DateTime.Now.Second % 20D, 20D);
			//	layers.Enqueue(layer);

			//	EffectLayer layer_swirl = new EffectLayer(this.ID + " - Swirl");
			//	layer_swirl.PercentEffect(Color.Blue, Color.Black,
			//		new KeySequence(new[]
			//		{
			//			DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT, DeviceKeys.NUM_NINE,
			//			DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
			//		}), DateTime.Now.Millisecond % 500D, 500D,
			//		PercentEffectType.Progressive_Gradual);
			//	//layers.Enqueue(layer_swirl);

			//	EffectLayer layer_blinking = new EffectLayer(this.ID + " - Blinking Light");

			//	ColorSpectrum blink_spec = new ColorSpectrum(Color.Red, Color.Black, Color.Red);
			//	var blink_color2 = new EffectColor(blink_spec.GetColorAt(DateTime.Now.Millisecond/1f / 1000.0f));
			//	//blink_color.Alpha = (byte)(blink_color.Alpha * 0.3);

			//	layer_blinking.Set(DeviceKeys.NUM_FIVE, (Color)blink_color2);
			//	layer_blinking.Set(DeviceKeys.NUM_SIX, (Color)new EffectColor(blink_spec.GetColorAt((Utils.Time.GetMillisecondsSinceEpoch() % 2000) / 2000.0f, 1F)));
			////	Global.logger.LogLine("====" + Utils.Time.GetMillisecondsSinceEpoch()% 1000, Logging_Level.Error);
			//	layers.Enqueue(layer_blinking);

			//	//	ColorSpectrum.RainbowLoop.GetColorAt()

			//	//layer.Set();

			var totalLoad = (CpuCoresPerformanceCounter.EasedCpuCoresLoad[0] +
							CpuCoresPerformanceCounter.EasedCpuCoresLoad[1] +
							CpuCoresPerformanceCounter.EasedCpuCoresLoad[2] +
							CpuCoresPerformanceCounter.EasedCpuCoresLoad[3]) / 4f;

			var per = (totalLoad - 95) / 5f;
			per = Math.Max(0, Math.Min(1, per));

			var cx = Color.FromArgb((byte)(255 * per), Color.Black);

			EffectLayer CPULayer = new EffectLayer(this.ID + " - CPULayer", cx);
			EffectLayer CPULayerBlink = new EffectLayer(this.ID + " - CPULayerBlink");
			EffectLayer CPULayerRainbowCircle = new EffectLayer(this.ID + " - CPULayerRainbowCircle");

			var blink_color = blinking.GetColorAt((Utils.Time.GetMillisecondsSinceEpoch() % 1000) / 1000.0f);

			var setLoad = new Action<DeviceKeys, float>((keys, load) =>
			{
				CPULayer.Set(keys, loadGradient.GetColorAt(load, 100f));
				CPULayerBlink.Set(keys, Color.FromArgb((byte)(blink_color.A * Math.Max(0, Math.Min(1, (load - 95) / 5))), blink_color));
			});
			//Global.logger.LogLine("CPU LOAD: "+ CpuCoresPerformanceCounter.EasedCpuCoresLoad[0], Logging_Level.Error);

			setLoad(DeviceKeys.G6, CpuCoresPerformanceCounter.EasedCpuCoresLoad[0]);
			setLoad(DeviceKeys.G7, CpuCoresPerformanceCounter.EasedCpuCoresLoad[1]);
			setLoad(DeviceKeys.G8, CpuCoresPerformanceCounter.EasedCpuCoresLoad[2]);
			setLoad(DeviceKeys.G9, CpuCoresPerformanceCounter.EasedCpuCoresLoad[3]);

			ColorSpectrum.RainbowLoop.Shift((float)(-0.005 + -0.02 * totalLoad / 100f));
			for (int i = 0; i < RainbowCircleKeys.Length; i++)
			{
				CPULayerRainbowCircle.Set(RainbowCircleKeys[i], Color.FromArgb((byte)(255* totalLoad / 100f), ColorSpectrum.RainbowLoop.GetColorAt(i, RainbowCircleKeys.Length)));
			}

			layers.Enqueue(CPULayer);
			layers.Enqueue(CPULayerBlink);
			layers.Enqueue(CPULayerRainbowCircle);

			return layers.ToArray();
		}

		private static class CpuCoresPerformanceCounter
		{
			private static float[] prevCpuCoresLoad = { 0f, 0f, 0f, 0f };
			private static float[] easedCpuCoresLoad = { 0f, 0f, 0f, 0f };
			private static float[] targetCpuCoresLoad = { 0f, 0f, 0f, 0f };
			private static float[] newCpuCoresLoad = null;

			private static long updateTime = Utils.Time.GetMillisecondsSinceEpoch();

			//public struct CpuCoresLoadFrame
			//{
			//	public readonly float[] CpuCoresLoad;
			//	public readonly long Time;

			//	public CpuCoresLoadFrame(long time, float core1Load, float core2Load, float core3Load, float core4Load)
			//	{
			//		CpuCoresLoad = new[] { core1Load, core2Load, core3Load, core4Load };
			//		Time = time;
			//	}
			//}

			private static int usage = 5;
			private static TaskCompletionSource<bool> sleeping;

			private static readonly Task Updater = Task.Run((Action)(async () =>
		   {
			   while (true)
			   {
				   try
				   {
					   var counters = new[] {
							new PerformanceCounter("Processor", "% Processor Time", "0"),
							new PerformanceCounter("Processor", "% Processor Time", "1"),
							new PerformanceCounter("Processor", "% Processor Time", "2"),
							new PerformanceCounter("Processor", "% Processor Time", "3")
					   };

					   while (true)
					   {
						   usage--;
						   if (usage <= 0)
						   {
							   sleeping = new TaskCompletionSource<bool>();
							   await sleeping.Task;
							   sleeping = null;
						   }

						   //cpuCoresLoadPrevious = cpuCoresLoadLast;
						   //cpuCoresLoadLast = new CpuCoresLoadFrame(Utils.Time.GetMillisecondsSinceEpoch(),
						   //	counters[1].NextValue(), counters[2].NextValue(), counters[3].NextValue(), counters[4].NextValue());
						   //newTargetValues = true;

						   newCpuCoresLoad = new[]
							  {counters[0].NextValue(), counters[1].NextValue(), counters[2].NextValue(), counters[3].NextValue()};

						   await Task.Delay(1000);
					   }
				   }
				   catch (Exception exc)
				   {
					   Global.logger.LogLine("PerformanceCounter exception: " + exc, Logging_Level.Error);
				   }
				   await Task.Delay(500);
			   }
		   }));

			//private static CpuCoresLoadFrame cpuCoresLoadPrevious;
			//private static CpuCoresLoadFrame cpuCoresLoadLast;

			//public static CpuCoresLoadFrame CpuCoresLoadPrevious
			//{
			//	get
			//	{
			//		usage = 5;
			//		sleeping?.SetResult(true);
			//		return cpuCoresLoadPrevious;
			//	}
			//}

			//public static CpuCoresLoadFrame CpuCoresLoadLast
			//{
			//	get
			//	{
			//		usage = 5;
			//		sleeping?.SetResult(true);
			//		return cpuCoresLoadLast;
			//	}
			//}

			public static float[] EasedCpuCoresLoad
			{
				get
				{
					usage = 5;

					var sleepingcopy = sleeping;
					if (sleepingcopy != null)
						sleepingcopy.SetResult(true);

					if (newCpuCoresLoad != null)
					{
						prevCpuCoresLoad = (float[])easedCpuCoresLoad.Clone();
						targetCpuCoresLoad = newCpuCoresLoad;
						newCpuCoresLoad = null;
						updateTime = Utils.Time.GetMillisecondsSinceEpoch();
					}

					for (int i = 0; i < easedCpuCoresLoad.Length; i++)
					{
						easedCpuCoresLoad[i] = prevCpuCoresLoad[i] + (targetCpuCoresLoad[i] - prevCpuCoresLoad[i]) *
							Math.Min((Utils.Time.GetMillisecondsSinceEpoch() - updateTime) / 1000f, 1f);
					}
					return easedCpuCoresLoad;
				}
			}
		}

	}
}