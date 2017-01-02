//
// Voron Scripts - PerformanceBars
// v1.0-beta.5
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
using Aurora.Settings;
using Microsoft.Scripting.Runtime;

namespace Aurora.Scripts.VoronScripts
{
	public class PerformanceBars
	{
		public string ID = "PerformanceBars";

		public KeySequence DefaultKeys = new KeySequence();

		internal PerformanceBar[] Bars = new[]
		{
			// You can create as many bars as you like with different parameters.
			// You can use any valid performance counter in system.
			// List of them you can find in Computer Managment->Performance->Monitoring Tools->Performance Monitor
			// Below if some common examples. Comment/uncomment what need/don't need.

			// Total processor load on F1-F12 keys
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "Processor",
						counterName: "% Processor Time",
						instanceName: "_Total"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new [] {
					 DeviceKeys.F1, DeviceKeys.F2, DeviceKeys.F3, DeviceKeys.F4, DeviceKeys.F5, DeviceKeys.F6,
					 DeviceKeys.F7, DeviceKeys.F8, DeviceKeys.F9, DeviceKeys.F10, DeviceKeys.F11, DeviceKeys.F12}),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of Core 1 on keys 1-0
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "Processor",
						counterName: "% Processor Time",
						instanceName: "0"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new []
				 {
					 DeviceKeys.ONE, DeviceKeys.TWO, DeviceKeys.THREE, DeviceKeys.FOUR, DeviceKeys.FIVE,
					 DeviceKeys.SIX, DeviceKeys.SEVEN, DeviceKeys.EIGHT, DeviceKeys.NINE, DeviceKeys.ZERO
				 }),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of Core 2 on keys Q-P
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "Processor",
						counterName: "% Processor Time",
						instanceName: "1"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new []
				 {
					 DeviceKeys.Q, DeviceKeys.W, DeviceKeys.E, DeviceKeys.R, DeviceKeys.T,
					 DeviceKeys.Y, DeviceKeys.U, DeviceKeys.I, DeviceKeys.O, DeviceKeys.P
				 }),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of Core 3 on keys A-;
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "Processor",
						counterName: "% Processor Time",
						instanceName: "2"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new []
				 {
					 DeviceKeys.A, DeviceKeys.S, DeviceKeys.D, DeviceKeys.F, DeviceKeys.G,
					 DeviceKeys.H, DeviceKeys.J, DeviceKeys.K, DeviceKeys.L, DeviceKeys.PERIOD, DeviceKeys.SEMICOLON
				 }),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of Core 4 on keys Z-/
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "Processor",
						counterName: "% Processor Time",
						instanceName: "3"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new []
				 {
					 DeviceKeys.Z, DeviceKeys.X, DeviceKeys.C, DeviceKeys.V, DeviceKeys.B,
					 DeviceKeys.N, DeviceKeys.M, DeviceKeys.COMMA, DeviceKeys.FORWARD_SLASH
				 }),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of disk C: on Num1-Num8
			new PerformanceBar(
				valueProvider: new EasedPerformanceCounter(
					performanceCounter: new PerformanceCounter(
						categoryName: "LogicalDisk",
						counterName: "% Disk Time",
						instanceName: "C:"),
					minValue: 0f,
					maxValue: 100f,
					updateInterval: 1000,
					easing: true),
				 deviceKeys: new KeySequence(new []
				 {
					DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN
				 }),
				 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
				 blinkingThreshold: 0.95f,
				 blinkingSpeed: 1000
				),

			// Load of all disks using FreeForm 
			//new PerformanceBar(
			//	valueProvider: new EasedPerformanceCounter(
			//		performanceCounter: new PerformanceCounter(
			//			categoryName: "LogicalDisk",
			//			counterName: "% Disk Time",
			//			instanceName: "_Total"),
			//		minValue: 0f,
			//		maxValue: 100f,
			//		updateInterval: 1000,
			//		easing: true),
			//	 deviceKeys: new KeySequence(new FreeFormObject(0, 0, 300, 200, 90)),
			//	 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
			//	 blinkingThreshold: 0.95f,
			//	 blinkingSpeed: 1000
			//	),
			
			// Network Load on Num2-Num8
			// You need to specify right name for your network adapter and your theoretical max speed
			//new PerformanceBar(
			//	valueProvider: new EasedPerformanceCounter(
			//		performanceCounter: new PerformanceCounter(
			//			categoryName: "Network Interface",
			//			counterName: "Bytes Total/sec",
			//			instanceName: "Realtek PCIe GBE Family Controller"),
			//		minValue: 0f,
			//		maxValue: 100 * 1000 * 1000, // 100 Mbit
			//		updateInterval: 1000,
			//		easing: true),
			//	 deviceKeys: new KeySequence(new []
			//	 {
			//		DeviceKeys.NUM_TWO, DeviceKeys.NUM_FIVE, DeviceKeys.NUM_EIGHT, 
			//	 }),
			//	 colorSpectrum: new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
			//	 blinkingThreshold: 0.95f,
			//	 blinkingSpeed: 1000
			//	),
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, object state = null)
		{
			var currentTime = Utils.Time.GetMillisecondsSinceEpoch();

			return Bars.Select((x, i) =>
			{
				var effectLayer = new EffectLayer(ID + " - PerformanceBar " + i);
				x.Render(effectLayer, currentTime);
				return effectLayer;

			}).ToArray();
		}

		internal class PerformanceBar
		{
			private readonly IValueProvider valueProvider;
			private readonly KeySequence deviceKeys;
			private readonly ColorSpectrum colorSpectrum;
			private readonly float blinkingThreshold;
			private readonly int blinkingSpeed;

			public PerformanceBar([NotNull]IValueProvider valueProvider,
				[NotNull]KeySequence deviceKeys, [NotNull]ColorSpectrum colorSpectrum, float blinkingThreshold, int blinkingSpeed)
			{
				this.valueProvider = valueProvider;
				this.deviceKeys = deviceKeys;
				this.colorSpectrum = colorSpectrum;
				this.blinkingThreshold = blinkingThreshold;
				this.blinkingSpeed = blinkingSpeed;
			}

			public void Render(EffectLayer effectLayer, long currentTime)
			{
				var value = valueProvider.GetValue();
				//(currentTime % 3000) / 30.0f / 100f;

				var blinkingLevel = (value - blinkingThreshold) / (1 - blinkingThreshold);

				blinkingLevel = Math.Max(0, Math.Min(1, blinkingLevel))
					* Math.Abs(1f - (currentTime % blinkingSpeed) / (blinkingSpeed / 2f));

				if (deviceKeys.type == KeySequenceType.Sequence)
				{
					// Animating by key sequence manually cause of bug in PercentEffect in Aurora v0.5.1d

					for (int i = 0; i < deviceKeys.keys.Count; i++)
					{
						var blendLevel = Math.Min(1, Math.Max(0,
							(value - (i / (float)deviceKeys.keys.Count)) / (1f / deviceKeys.keys.Count)));

						effectLayer.Set(deviceKeys.keys[i], Color.FromArgb((byte)(blendLevel * 255),
							colorSpectrum.GetColorAt(i / (deviceKeys.keys.Count - 1f))));

						if (blinkingThreshold <= 1)
						{
							effectLayer.Set(deviceKeys.keys[i], (Color)EffectColor.BlendColors(
								new EffectColor(effectLayer.Get(deviceKeys.keys[i])),
								new EffectColor(Color.Black),
									blendLevel * i / (deviceKeys.keys.Count - 1f) * blinkingLevel));
						}
					}
				}
				else
				{
					effectLayer.PercentEffect(colorSpectrum, deviceKeys, value, 1, PercentEffectType.Progressive_Gradual);
					effectLayer.PercentEffect(
						new ColorSpectrum(
							Color.FromArgb(0, Color.Black),
							Color.FromArgb((byte)(255 * blinkingLevel), Color.Black)),
						deviceKeys, value, 1, PercentEffectType.Progressive_Gradual);
				}
			}

		}

		internal interface IValueProvider
		{
			float GetValue();
		}

		internal class EasedPerformanceCounter : EasedPerformanceCounter<float>, IValueProvider
		{
			private readonly PerformanceCounter performanceCounter;
			private readonly float minValue;
			private readonly float maxValue;
			private bool easing;

			public EasedPerformanceCounter([NotNull] PerformanceCounter performanceCounter, float minValue, float maxValue,
				int updateInterval, bool easing)
			{
				this.performanceCounter = performanceCounter;
				this.minValue = minValue;
				this.maxValue = maxValue;
				UpdateInterval = updateInterval;
				this.easing = easing;
			}

			protected override float GetEasedValue(CounterFrame<float> currentFrame)
			{
				return currentFrame.PreviousValue + (currentFrame.CurrentValue - currentFrame.PreviousValue) *
					   Math.Min(Utils.Time.GetMillisecondsSinceEpoch() - currentFrame.Timestamp, UpdateInterval) / UpdateInterval;
			}

			protected override float UpdateValue()
			{
				return (performanceCounter.NextValue() - minValue) / maxValue;
			}

			public float GetValue()
			{
				return GetValue(easing);
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