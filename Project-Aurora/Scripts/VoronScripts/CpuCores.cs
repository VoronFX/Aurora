//
// Voron Scripts - CpuCores
// v1.0-beta.6
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
using Aurora.Utils;

namespace Aurora.Scripts.VoronScripts
{
	internal static class CpuCoresExtensionMethods
	{
		internal static void RegProp(this VariableRegistry registry,
			string name, object defaultValue, string remark = "", object min = null, object max = null)
		{
			registry.Register(name, defaultValue, name, max, min, remark);
		}
	}

	public class CpuCores : IEffectScript
	{
		public string ID
		{
			get { return "CpuCores"; }
		}

		public CpuCores()
		{
			var registry = new VariableRegistry();

			registry.RegProp("Core Keys",
				new KeySequence(new[] {DeviceKeys.G6, DeviceKeys.G7, DeviceKeys.G8, DeviceKeys.G9}
					.Take(Math.Min(Environment.ProcessorCount, 4)).ToArray()),
				"Each key will represent load of one core. Select only keys.");

			registry.RegProp("Enable Core Overload", true,
				"Core will be start blinking when load will reach certain threshold");

			registry.RegProp("Enable Full Overload", true, "EnableFullOverload",
				"Keyboard will be start fading out when overall load will reach certain threshold");

			registry.RegProp("Overload Threshold", 95, "Overload threshold in percent of load", 0, 100);

			registry.RegProp("Overload Blinking Speed", 1000, "Speed of CoreOverload blinking in ms", 10, 10000);

			registry.RegProp("Overload Color", new RealColor(Color.Black));
			registry.RegProp("Core Gradient (Advanced)", SpectrumToString(new ColorSpectrum(
					Color.FromArgb(0, Color.Lime),
					Color.Lime,
					Color.Orange,
					Color.Red)
				),
				"Gradient that is used for CoreKeys. One color per line. Position can be optionally set with @ symbol.");

			registry.RegProp("Circle Keys", new KeySequence(new[]
				{
					DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT,
					DeviceKeys.NUM_NINE, DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
				}),
				"Circle will be rotate faster with CPU load. Select only keys.");

			registry.RegProp("Circle Gradient (Advanced)", SpectrumToString(new ColorSpectrum(
					Color.FromArgb(255, 0, 0),
					Color.FromArgb(255, 127, 0),
					Color.FromArgb(255, 255, 0),
					Color.FromArgb(0, 255, 0),
					Color.FromArgb(0, 0, 255),
					Color.FromArgb(75, 0, 130),
					Color.FromArgb(139, 0, 255),
					Color.FromArgb(255, 0, 0)
				)),
				"Gradient that is used for CircleKeys. One color per line. Position can be optionally set with @ symbol.");

			Properties = registry;
		}

		public KeySequence DefaultKeys;

		private VariableRegistry properties;

		public VariableRegistry Properties
		{
			get { return properties; }
			private set
			{
				if (value == null) return;
				CircleGradient = StringToSpectrum(value.GetVariable<string>("Circle Gradient (Advanced)"));
				CoreGradient = StringToSpectrum(value.GetVariable<string>("Core Gradient (Advanced)"));
				CoreKeys = value.GetVariable<KeySequence>("Core Keys").keys.ToArray();
				CircleKeys = value.GetVariable<KeySequence>("Circle Keys").keys.ToArray();
				OverloadThreshold = value.GetVariable<int>("Overload Threshold") / 100f;
				OverloadColor = value.GetVariable<RealColor>("Overload Color").GetDrawingColor();
				BlinkingSpeed = value.GetVariable<int>("Overload Blinking Speed");
				EnableCoreOverload = value.GetVariable<bool>("Enable Core Overload");
				EnableFullOverload = value.GetVariable<bool>("Enable Full Overload");
				properties = value;
			}
		}

		private string SpectrumToString(ColorSpectrum spectrum)
		{
			return string.Join(Environment.NewLine,
				spectrum.GetSpectrumColors().Select(x => string.Format("#{0:X2}{1:X2}{2:X2}{3:X2} @ {4}", x.Value.A, x.Value.R,
					x.Value.G, x.Value.B, x.Key)));
		}

		private ColorSpectrum StringToSpectrum(string text)
		{
			var colors = text.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('@'))
				.ToArray();
			var spectrum = new ColorSpectrum();
			foreach (var colorset in colors.Select((x, i) => new
			{
				Color = ColorTranslator.FromHtml(x[0]),
				Position = x.Length > 1 ? float.Parse(x[1]) : (1f / colors.Length * i)
			}))
			{
				spectrum.SetColorAt(colorset.Position, colorset.Color);
			}
			return spectrum;
		}

		private ColorSpectrum CircleGradient { get; set; }
		private ColorSpectrum CoreGradient { get; set; }
		private float OverloadThreshold { get; set; }
		private Color OverloadColor { get; set; }
		private int BlinkingSpeed { get; set; }
		private DeviceKeys[] CoreKeys { get; set; }
		private DeviceKeys[] CircleKeys { get; set; }
		private bool EnableCoreOverload { get; set; }
		private bool EnableFullOverload { get; set; }

		public object UpdateLights(VariableRegistry properties, IGameState state = null)
		{
			//if (!ReferenceEquals(Properties, properties))
			//Properties = properties;

			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			var cpuLoad = Cpu.GetValue();

			var blinkingLevel = (cpuLoad[0] - OverloadThreshold) / (1 - OverloadThreshold);
			blinkingLevel = Math.Max(0, Math.Min(1, blinkingLevel));

			EffectLayer CPULayer = new EffectLayer(ID + " - CPULayer",
				Color.FromArgb((byte) (EnableFullOverload ? (255 * blinkingLevel) : 0), OverloadColor));
			EffectLayer CPULayerBlink = new EffectLayer(ID + " - CPULayerBlink");
			EffectLayer CPULayerCircle = new EffectLayer(ID + " - CPULayerCircle");

			for (int i = 0; i < Math.Min(CoreKeys.Length, cpuLoad.Length - 1); i++)
			{
				CPULayer.Set(CoreKeys[i], CoreGradient.GetColorAt(cpuLoad[i + 1]));

				blinkingLevel = (cpuLoad[i + 1] - OverloadThreshold) / (1 - OverloadThreshold);
				blinkingLevel = Math.Max(0, Math.Min(1, blinkingLevel))
				                * Math.Abs(1f - (Utils.Time.GetMillisecondsSinceEpoch() % BlinkingSpeed) / (BlinkingSpeed / 2f));

				CPULayer.Set(CoreKeys[i], (Color) EffectColor.BlendColors(
					new EffectColor(CPULayer.Get(CoreKeys[i])),
					new EffectColor(OverloadColor), EnableCoreOverload ? blinkingLevel : 0f));
			}

			CircleGradient.Shift((float) (-0.005 + -0.02 * cpuLoad[0]));

			for (int i = 0; i < CircleKeys.Length; i++)
			{
				CPULayerCircle.Set(CircleKeys[i],
					Color.FromArgb((byte) (255 * cpuLoad[0]),
						CircleGradient.GetColorAt(i / (float) CircleKeys.Length)));
			}

			layers.Enqueue(CPULayer);
			layers.Enqueue(CPULayerBlink);
			layers.Enqueue(CPULayerCircle);

			return layers.ToArray();
		}

		private static readonly CpuPerCoreCounter Cpu = new CpuPerCoreCounter();

		internal class CpuPerCoreCounter : EasedPerformanceCounter<float[]>
		{
			private PerformanceCounter[] counters;
			private readonly float[] defaultValues = new float[Environment.ProcessorCount + 1];

			protected override float[] GetEasedValue(CounterFrame<float[]> currentFrame)
			{
				var prev = currentFrame.PreviousValue ?? defaultValues;
				var curr = currentFrame.CurrentValue ?? defaultValues;

				return prev.Select((x, i) => x + (curr[i] - x) * Math.Min(Utils.Time.GetMillisecondsSinceEpoch()
				                                                          - currentFrame.Timestamp, UpdateInterval) /
				                             UpdateInterval).ToArray();
			}

			protected override float[] UpdateValue()
			{
				if (counters == null)
					counters = new PerformanceCounter[Environment.ProcessorCount + 1]
						.Select((x, i) =>
							new PerformanceCounter("Processor", "% Processor Time", i == 0 ? "_Total" : (i - 1).ToString())).ToArray();

				return counters.Select(x => x.NextValue() / 100f).ToArray();
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
}