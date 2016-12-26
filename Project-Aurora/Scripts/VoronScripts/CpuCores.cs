//
// Voron Scripts - CpuCores
// v1.0-beta.0
// https://github.com/VoronFX/Aurora
// Copyright (C) 2016 Voronin Igor <Voron.exe@gmail.com>
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Aurora.Devices;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{

	public class CpuCores
	{
		public string ID = "CpuCores";

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

		private static int BlinkingSpeed = 1000;

		// Each key displays load of one core
		private static readonly DeviceKeys[] CpuCoresKeys =
			{ DeviceKeys.G6, DeviceKeys.G7, DeviceKeys.G8, DeviceKeys.G9 };

		// Keys for rainbow that represents processor usage by speed
		static readonly DeviceKeys[] RainbowCircleKeys = {
			DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT,
			DeviceKeys.NUM_NINE, DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, GameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			var cpuLoad = CpuCoresPerformanceCounter.EasedCpuCoresLoad;
			var cpuOverload = (cpuLoad[0] - 95) / 5f;
			cpuOverload = Math.Max(0, Math.Min(1, cpuOverload));

			EffectLayer CPULayer = new EffectLayer(ID + " - CPULayer", Color.FromArgb((byte)(255 * cpuOverload), Color.Black));
			EffectLayer CPULayerBlink = new EffectLayer(ID + " - CPULayerBlink");
			EffectLayer CPULayerRainbowCircle = new EffectLayer(ID + " - CPULayerRainbowCircle");

			var blinkColor = BlinkingSpectrum.GetColorAt((Utils.Time.GetMillisecondsSinceEpoch() % BlinkingSpeed) / (float)BlinkingSpeed);

			for (int i = 0; i < CpuCoresKeys.Length; i++)
			{
				CPULayer.Set(CpuCoresKeys[i], LoadGradient.GetColorAt(cpuLoad[i + 1] / 100f));
				CPULayerBlink.Set(CpuCoresKeys[i], Color.FromArgb((byte)(blinkColor.A * Math.Max(0, Math.Min(1,
					(cpuLoad[i + 1] - 95) / 5))), blinkColor));
			}

			RainbowLoop.Shift((float)(-0.005 + -0.02 *
				cpuLoad[0] / 100f));

			for (int i = 0; i < RainbowCircleKeys.Length; i++)
			{
				CPULayerRainbowCircle.Set(RainbowCircleKeys[i],
					Color.FromArgb((byte)(255 * cpuLoad[0] / 100f),
					RainbowLoop.GetColorAt(i / (float)RainbowCircleKeys.Length)));
			}

			layers.Enqueue(CPULayer);
			layers.Enqueue(CPULayerBlink);
			layers.Enqueue(CPULayerRainbowCircle);

			return layers.ToArray();
		}

		private static class CpuCoresPerformanceCounter
		{
			public static int UpdateEvery { get; set; }

			private static float[] prevCpuCoresLoad;
			private static float[] easedCpuCoresLoad;
			private static float[] targetCpuCoresLoad;
			private static float[] newCpuCoresLoad;

			private static long updateTime = Utils.Time.GetMillisecondsSinceEpoch();

			private static int usage = 1;
			private static TaskCompletionSource<bool> sleeping;

			static CpuCoresPerformanceCounter()
			{
				UpdateEvery = 1000;
				prevCpuCoresLoad = new float[Environment.ProcessorCount + 1];
				easedCpuCoresLoad = new float[Environment.ProcessorCount + 1];
				targetCpuCoresLoad = new float[Environment.ProcessorCount + 1];
			}


			private static readonly Task Updater = Task.Run((Action)(async () =>
			{
				while (true)
				{
					try
					{
						var counters = new PerformanceCounter[Environment.ProcessorCount + 1];
						counters[0] = new PerformanceCounter("Processor", "% Processor Time", "_Total");
						for (int i = 0; i < counters.Length - 1; i++)
						{
							counters[i + 1] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
						}

						while (true)
						{
							usage--;
							if (usage <= 0)
							{
								sleeping = new TaskCompletionSource<bool>();
								await sleeping.Task;
								sleeping = null;
							}

							newCpuCoresLoad = counters.Select(x => x.NextValue()).ToArray();

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
			/// Returns eased load for each logical core. 0 is total load.
			/// </summary>
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
							Math.Min((Utils.Time.GetMillisecondsSinceEpoch() - updateTime) / (float)UpdateEvery, 1f);
					}
					return easedCpuCoresLoad;
				}
			}
		}

	}
}