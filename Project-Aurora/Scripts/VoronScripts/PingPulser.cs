// 
// Voron Scripts - PingPulser
// v1.0-beta.4
// for Aurora v0.6.0
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

	public class PingPulser
	{
		public string ID = "PingPulser";

		public KeySequence DefaultKeys = new KeySequence();

		private static readonly PingAnimation PingAnimation1 = new PingAnimation()
		{
			DefaultHost = "google.com",
			HostsPerApplication = new[]
			{
				new KeyValuePair<string[], string>(
					new [] { "League of Legends", "league_of_legends", "league of legends.exe", "LolClient.exe", "LoLLauncher.exe", "lolpatcherux.exe" }, "185.40.64.69")
			},

			// Use Keys or Region to set area. 
			// Keys do much more fluid transitions than Region.
			Keys = new []
			{
				DeviceKeys.F1, DeviceKeys.F2, DeviceKeys.F3, DeviceKeys.F4, DeviceKeys.F5,  DeviceKeys.F6,
				DeviceKeys.F7,  DeviceKeys.F8,  DeviceKeys.F9,  DeviceKeys.F10, DeviceKeys.F11, DeviceKeys.F12
			},
			Region = new Rectangle(60, -3, 495, 35), // This is for F1-12 on Logitech G910

			MaxPing = 400, // End of region will be considered as MaxPing
			SuccessDuration = 500,
			FailDuration = 1000,
			TimeBetweenPings = 1500,

			PingSignalWidth = 2f / 12,
			PingShadowWidth = 2f / 12,
			PingAdvance = 200, // A reserve time between real ping request and it's animation (ms)
			BarSpectrum = new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
			ErrorSpectrum = new ColorSpectrum(Color.FromArgb(0, Color.Red),
					Color.Red, Color.Black, Color.Red, Color.Black)
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, IGameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			EffectLayer pingPulser1 = new EffectLayer(ID + " - PingPulser");
			PingAnimation1.Render(pingPulser1, Utils.Time.GetMillisecondsSinceEpoch());
			layers.Enqueue(pingPulser1);

			return layers.ToArray();
		}

		private class PingAnimation : Pinger
		{
			public int MaxPing { get; set; }

			public long PingAdvance { get; set; }

			public long FailDuration { get; set; }

			public long SuccessDuration { get; set; }

			public float PingShadowWidth { get; set; }

			public float PingSignalWidth { get; set; }

			public long TimeBetweenPings { get; set; }

			public ColorSpectrum BarSpectrum { get; set; }

			public ColorSpectrum ErrorSpectrum { get; set; }

			private RectangleF region;

			public RectangleF Region
			{
				get
				{
					return new RectangleF(
						(float)Math.Round((region.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width),
						(float)Math.Round((region.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height),
						(float)Math.Round(region.Width * Effects.editor_to_canvas_width),
						(float)Math.Round(region.Height * Effects.editor_to_canvas_height));
				}
				set { region = value; }
			}

			public DeviceKeys[] Keys { get; set; }

			protected static PingReply OldReply;
			protected long FinalAnimationTime;

			public PingAnimation()
			{
				// Default settings

				DefaultHost = "google.com";
				Region = new Rectangle(60, -3, 495, 35); // Region 
				MaxPing = 400; // End of region will be considered as MaxPing
				SuccessDuration = 500;
				FailDuration = 1000;
				TimeBetweenPings = 1500;

				PingSignalWidth = 2f / 12;
				PingShadowWidth = 2f / 12;
				PingAdvance = 200; // A reserve time between real ping request and it's animation (ms)
				BarSpectrum = new ColorSpectrum(Color.Lime, Color.Orange, Color.Red);
				ErrorSpectrum = new ColorSpectrum(Color.FromArgb(0, Color.Red),
					Color.Red, Color.Black, Color.Red, Color.Black);
			}

			public void Render(EffectLayer effectLayer, long currentTime)
			{
				// layers							|					|
				// 0.OldReplyPingBar				|==========>		|
				// 1.PingShadowAnimation			|     >=-			|
				// 2.NewBar							|====>				|
				// 3.SignalAnimation				|  -=>				|

				float pingPos = -PingShadowWidth;
				float pingOldBarWidth = 0;
				float pingNewBarWidth = 0;

				if (OldReply != null && OldReply.Status == IPStatus.Success)
				{
					pingOldBarWidth = OldReply.RoundtripTime / (float)MaxPing;
					//(float)(Region.Width * (Math.Ceiling(OldReply.RoundtripTime * 12d / MaxPing) - 0.2) / 12);
				}

				if (Phase == PingPhase.WaitingForActivation)
				{
					if (currentTime - PingStartedTime > TimeBetweenPings)
						AllowNextPing();
				}
				else if (Phase != PingPhase.Activated)
				{
					pingPos = Math.Max(0, currentTime - PingStartedTime - PingAdvance)
							* (1f + (PingSignalWidth + PingShadowWidth)) / (MaxPing + PingAdvance)
							- PingShadowWidth;
					pingNewBarWidth = pingPos;
				}

				if (Phase == PingPhase.PingEnded)
				{
					if (Reply != null && Reply.Status == IPStatus.Success)
					{
						if (pingPos >= Reply.RoundtripTime / (float)MaxPing)
						{
							FinalAnimationTime = currentTime;
							Phase = PingPhase.SuccessCompleteAnimation;
						}
					}
					else if (pingPos >= 1 + PingSignalWidth)
					{
						FinalAnimationTime = currentTime;
						Phase = PingPhase.TimeoutErrorAnimation;
					}
				}

				if (Phase == PingPhase.SuccessCompleteAnimation
					|| Phase == PingPhase.TimeoutErrorAnimation)
				{
					if (currentTime - FinalAnimationTime >=
						(Phase == PingPhase.SuccessCompleteAnimation ?
						SuccessDuration : FailDuration))
					{
						OldReply = Reply;
						Reply = null;
						Phase = PingPhase.WaitingForActivation;
						Render(effectLayer, currentTime);
						return;
					}

					if (Phase == PingPhase.SuccessCompleteAnimation)
					{
						float pingNewBarFullWidth = Reply.RoundtripTime / (float)MaxPing;

						pingPos =
							(currentTime - FinalAnimationTime) *
							(1 - pingNewBarFullWidth + PingSignalWidth) / (SuccessDuration)
							+ pingNewBarFullWidth;

						pingNewBarWidth = Math.Min(pingPos, pingNewBarFullWidth);
					}
				}

				if (Keys != null)
				{
					// Animating by keys. Prefferable. 

					Func<float, float, float, float> getBlend2 = (l, pos, r) =>
					{
						var leftEdgePercent = (1 + pos) / Keys.Length - l;
						var rightEdgePercent = r - (pos / Keys.Length);
						leftEdgePercent /= 1f / Keys.Length;
						rightEdgePercent /= 1f / Keys.Length;
						leftEdgePercent = 1 - Math.Max(0, Math.Min(1, leftEdgePercent));
						rightEdgePercent = 1 - Math.Max(0, Math.Min(1, rightEdgePercent));
						return 1 - leftEdgePercent - rightEdgePercent;
					};

					for (int i = 0; i < Keys.Length; i++)
					{
						var keyColor = new EffectColor(Color.Black);
						float kL = i / (Keys.Length - 1f);


						keyColor.BlendColors(new EffectColor(BarSpectrum.GetColorAt(kL)), getBlend2(pingPos, i, pingOldBarWidth));
						keyColor.BlendColors(new EffectColor(Color.Black),
							getBlend2(pingPos, i, pingPos + PingShadowWidth)
							* (1 - ((i / (float)Keys.Length - pingPos) / PingShadowWidth)));
						keyColor.BlendColors(new EffectColor(BarSpectrum.GetColorAt(kL)),
							getBlend2(0, i, Math.Min(pingPos, pingNewBarWidth)));
						keyColor.BlendColors(new EffectColor(BarSpectrum.GetColorAt(Math.Min(1, pingNewBarWidth))),
							getBlend2(pingPos - PingSignalWidth, i, pingPos)
							* (((i + 1) / (float)Keys.Length - (pingPos - PingSignalWidth)) / PingSignalWidth));

						if (Phase == PingPhase.TimeoutErrorAnimation)
							keyColor += new EffectColor(ErrorSpectrum.GetColorAt(
								Math.Min(1, (currentTime - FinalAnimationTime) / (float)FailDuration)));

						effectLayer.Set(Keys[i], (Color)keyColor);
					}

				}
				else
				{
					// Animating by rectangle.
					pingPos *= Region.Width;
					pingOldBarWidth *= Region.Width;
					pingNewBarWidth *= Region.Width;

					var oldPingBarRect = RectangleF.FromLTRB(
						Region.Left + pingPos, Region.Top,
						Region.Left + pingOldBarWidth, Region.Bottom);

					oldPingBarRect.Intersect(Region);

					var newPingBarRect = RectangleF.FromLTRB(
						Region.Left, Region.Top,
						Region.Left + Math.Min(pingNewBarWidth, pingPos), Region.Bottom);

					newPingBarRect.Intersect(Region);

					var pingSignalRect = RectangleF.FromLTRB(
						Region.Left + pingPos - (PingSignalWidth * Region.Width), Region.Top,
						Region.Left + pingPos, Region.Bottom);

					var pingShadowRect = RectangleF.FromLTRB(
						Region.Left + pingPos, Region.Top,
						Region.Left + pingPos + (PingShadowWidth * Region.Width), Region.Bottom);

					var shadowBrush = new LinearGradientBrush(pingShadowRect,
						Color.Black, Color.FromArgb(0, Color.Black), LinearGradientMode.Horizontal);

					var signalBrush = new LinearGradientBrush(pingSignalRect,
						 Color.FromArgb(0, BarSpectrum.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width))),
						 BarSpectrum.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width)), LinearGradientMode.Horizontal);

					pingShadowRect.Intersect(Region);
					pingSignalRect.Intersect(Region);

					var pingBarBrush = BarSpectrum.ToLinearGradient(Region.Width, Region.Height, Region.Left, Region.Top);

					shadowBrush.WrapMode = WrapMode.TileFlipX;
					signalBrush.WrapMode = WrapMode.TileFlipX;
					pingBarBrush.WrapMode = WrapMode.TileFlipX;

					using (var g = effectLayer.GetGraphics())
					{

						if (oldPingBarRect.Width > 0)
							g.FillRectangle(pingBarBrush, oldPingBarRect);

						if (pingShadowRect.Width > 0)
							g.FillRectangle(shadowBrush, pingShadowRect);

						if (newPingBarRect.Width > 0)
							g.FillRectangle(pingBarBrush, newPingBarRect);

						if (pingSignalRect.Width > 0)
							g.FillRectangle(signalBrush, pingSignalRect);

						if (Phase == PingPhase.TimeoutErrorAnimation)
							g.FillRectangle(new SolidBrush(ErrorSpectrum.GetColorAt(
								Math.Min(1, (currentTime - FinalAnimationTime) / (float)FailDuration))), Region);

					}
				}
			}
		}

		internal class Pinger
		{
			protected long PingStartedTime;
			protected long PingEndedTime;
			protected PingReply Reply;
			protected PingPhase Phase = PingPhase.WaitingForActivation;

			public KeyValuePair<string[], string>[] HostsPerApplication { get; set; }
			public string DefaultHost { get; set; }

			protected enum PingPhase
			{
				WaitingForActivation, Activated, PingStarted, PingEnded, SuccessCompleteAnimation, TimeoutErrorAnimation
			}

			private readonly Task updater;

			public Pinger()
			{
				updater = Task.Run((Action)(async () =>
				{
					while (true)
					{
						try
						{
							var ping = new Ping();
							while (true)
							{
								var pingReplyTask = ping.SendPingAsync(ChooseHost());
								PingStartedTime = Utils.Time.GetMillisecondsSinceEpoch();
								Phase = PingPhase.PingStarted;
								try
								{
									Reply = await pingReplyTask;
								}
								catch (Exception e)
								{
									Reply = null;
								}
								PingEndedTime = Utils.Time.GetMillisecondsSinceEpoch();
								Phase = PingPhase.PingEnded;
								var newActivator = new TaskCompletionSource<bool>();
								pingActivator = newActivator;
								await newActivator.Task;
							}
						}
						catch (Exception exc)
						{
							Global.logger.LogLine("PingCounter exception: " + exc, Logging_Level.Error);
						}
						await Task.Delay(500);
					}
				}));
			}


			private TaskCompletionSource<bool> pingActivator;

			public void AllowNextPing()
			{
				var pingActivatorCopy = pingActivator;
				if (pingActivatorCopy != null)
				{
					if (Interlocked.CompareExchange(ref pingActivator, null, pingActivatorCopy) == pingActivatorCopy)
					{
						Task.Run(() => pingActivatorCopy.TrySetResult(true));
					}
				}
			}

			private string ChooseHost()
			{
				var host = DefaultHost;
				var currentApp = GetActiveWindowsProcessname();
				if (!string.IsNullOrWhiteSpace(currentApp))
					currentApp = System.IO.Path.GetFileName(currentApp).ToLowerInvariant();

				if (!string.IsNullOrWhiteSpace(currentApp) && HostsPerApplication != null)
				{
					foreach (var hostPerApplication in HostsPerApplication)
					{
						if (hostPerApplication.Key.Select(x => x.ToLowerInvariant()).Contains(currentApp))
						{
							host = hostPerApplication.Value;
						}
					}
				}
				return host;
			}

			[System.Runtime.InteropServices.DllImport("user32.dll")]
			private static extern IntPtr GetForegroundWindow();

			[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
			private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

			private static string GetActiveWindowsProcessname()
			{
				try
				{
					IntPtr handle = GetForegroundWindow();

					uint processId;
					return GetWindowThreadProcessId(handle, out processId) > 0 ?
						Process.GetProcessById((int)processId).MainModule.FileName : "";
				}
				catch (Exception exception)
				{
					Global.logger.LogLine(exception.ToString(), Logging_Level.Error);
					return "";
				}
			}

		}

	}
}