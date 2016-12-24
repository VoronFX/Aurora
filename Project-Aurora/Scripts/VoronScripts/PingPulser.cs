using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
			Host = "google.com",
			Region = new Rectangle(60, -3, 495, 35), // This is for F1-12 on Logitech G910
			MaxPing = 400, // End of region will be considered as MaxPing
			SuccessDuration = 500,
			FailDuration = 1000,
			TimeBetweenPings = 1500,

			PingSignalWidth = 495 / 12 * 2,
			PingShadowWidth = 495 / 12 * 2,
			PingAdvance = 200, // A reserve time between real ping request and it's animation (ms)
			BarSpectrum = new ColorSpectrum(Color.Lime, Color.Orange, Color.Red),
			ErrorSpectrum = new ColorSpectrum(Color.FromArgb(0, Color.Red),
					Color.Red, Color.Black, Color.Red, Color.Black)
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, GameState state = null)
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

			protected static PingReply OldReply;
			protected long FinalAnimationTime;

			public PingAnimation()
			{
				// Default settings

				Host = "google.com";
				Region = new Rectangle(60, -3, 495, 35); // Region 
				MaxPing = 400; // End of region will be considered as MaxPing
				SuccessDuration = 500;
				FailDuration = 1000;
				TimeBetweenPings = 1500;

				PingSignalWidth = Region.Width / 12 * 2;
				PingShadowWidth = Region.Width / 12 * 2;
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
					pingOldBarWidth = Region.Width * OldReply.RoundtripTime / MaxPing;
					//(float)(Region.Width * (Math.Ceiling(OldReply.RoundtripTime * 12d / MaxPing) - 0.2) / 12);
				}

				if (Phase == PingPhase.Delay)
				{
					var pingNextCopy = PingNext;
					if (pingNextCopy != null && (currentTime - PingStartedTime > TimeBetweenPings))
					{
						PingNext = null;
						pingNextCopy.SetResult(true);
					}
				}
				else
				{
					pingPos = Math.Max(0, currentTime - PingStartedTime - PingAdvance)
							* (Region.Width + (PingSignalWidth + PingShadowWidth)) / (MaxPing + PingAdvance)
							- PingShadowWidth;
					pingNewBarWidth = pingPos;
				}

				if (Phase == PingPhase.PingEnded)
				{
					if (Reply != null && Reply.Status == IPStatus.Success && pingPos >= Region.Width * Reply.RoundtripTime / MaxPing)
					{
						FinalAnimationTime = currentTime;
						Phase = PingPhase.SuccessCompleteAnimation;
					}
					else if (pingPos >= Region.Width + PingSignalWidth)
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
						Phase = PingPhase.Delay;
						Render(effectLayer, currentTime);
						return;
					}

					if (Phase == PingPhase.SuccessCompleteAnimation)
					{
						float pingNewBarFullWidth = Region.Width * Reply.RoundtripTime / MaxPing;

						pingPos =
							(currentTime - FinalAnimationTime) *
							(Region.Width - pingNewBarFullWidth + PingSignalWidth) / (SuccessDuration)
							+ pingNewBarFullWidth;

						pingNewBarWidth = Math.Min(pingPos, pingNewBarFullWidth);
					}
				}

				var oldPingBarRect = RectangleF.FromLTRB(
					Region.Left + pingPos, Region.Top,
					Region.Left + pingOldBarWidth, Region.Bottom);

				oldPingBarRect.Intersect(Region);

				var newPingBarRect = RectangleF.FromLTRB(
					Region.Left, Region.Top,
					Region.Left + Math.Min(pingNewBarWidth, pingPos), Region.Bottom);

				newPingBarRect.Intersect(Region);

				var pingSignalRect = RectangleF.FromLTRB(
					Region.Left + pingPos - PingSignalWidth, Region.Top,
					Region.Left + pingPos, Region.Bottom);

				var pingShadowRect = RectangleF.FromLTRB(
					Region.Left + pingPos, Region.Top,
					Region.Left + pingPos + PingShadowWidth, Region.Bottom);

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

		private class Pinger
		{
			protected long PingStartedTime;
			protected long PingEndedTime;
			protected TaskCompletionSource<bool> PingNext;
			protected PingReply Reply;
			protected PingPhase Phase = PingPhase.Delay;

			public string Host { get; set; }

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
								var pingReplyTask = ping.SendPingAsync(Host);
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
								PingNext = new TaskCompletionSource<bool>();
								await PingNext.Task;
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

			protected enum PingPhase
			{
				Delay, PingStarted, PingEnded, SuccessCompleteAnimation, TimeoutErrorAnimation
			}
		}

	}
}