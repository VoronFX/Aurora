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

	public class CpuCoresUsage
	{
		public string ID = "CpuCoresUsage";

		public KeySequence DefaultKeys = new KeySequence();

		static readonly ColorSpectrum loadGradient = new ColorSpectrum(Color.FromArgb(0, Color.Lime), Color.Lime, Color.Orange, Color.Red);
		static readonly ColorSpectrum blinking = new ColorSpectrum(Color.Black, Color.FromArgb(0, Color.Black), Color.Black);
		static readonly DeviceKeys[] RainbowCircleKeys = {
			DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT,
			DeviceKeys.NUM_NINE, DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
		};
		static readonly PingAnimation Ping = new PingAnimation(5, 12, 1000, new ColorSpectrum(Color.Black, Color.Lime));
		static readonly DeviceKeys[] PingKeys = {
			DeviceKeys.F1, DeviceKeys.F2, DeviceKeys.F3, DeviceKeys.F4,
			DeviceKeys.F5, DeviceKeys.F6, DeviceKeys.F7, DeviceKeys.F8,
			DeviceKeys.F9, DeviceKeys.F10, DeviceKeys.F11, DeviceKeys.F12
		};

		private static readonly PingAnimation2 PingAnimation2Test = new PingAnimation2();
		private static readonly PingAnimation3 PingAnimation3Test = new PingAnimation3(new Rectangle(60, -3, 495, 35), 500);
		private static readonly PingAnimation4 PingAnimation4Test = new PingAnimation4(new Rectangle(60, -3, 495, 35), 500);


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
			var time = Utils.Time.GetMillisecondsSinceEpoch();
			var per = (totalLoad - 95) / 5f;
			per = Math.Max(0, Math.Min(1, per));

			var cx = Color.FromArgb((byte)(255 * per), Color.Black);

			EffectLayer CPULayer = new EffectLayer(ID + " - CPULayer", cx);
			EffectLayer CPULayerBlink = new EffectLayer(ID + " - CPULayerBlink");
			EffectLayer CPULayerRainbowCircle = new EffectLayer(ID + " - CPULayerRainbowCircle");
			EffectLayer PingAnimation = new EffectLayer(ID + " - PingAnimation");
			EffectLayer PingAnimation2 = new EffectLayer(ID + " - PingAnimation2");

			var blink_color = blinking.GetColorAt((time % 1000) / 1000.0f);

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
				CPULayerRainbowCircle.Set(RainbowCircleKeys[i], Color.FromArgb((byte)(255 * totalLoad / 100f), ColorSpectrum.RainbowLoop.GetColorAt(i, RainbowCircleKeys.Length)));
			}
			if (DateTime.Now.Second % 2 == 0)
			{
				Ping.Ping();
			}

			for (int i = 0; i < PingKeys.Length; i++)
			{
				//PingAnimation.Set(PingKeys[i], Ping.Render(i, time));
				//PingAnimation.Set(PingKeys[i], Color.Blue);
			}

			PingAnimation4Test.Render(PingAnimation2, time);
			//PingAnimation2Test.Render(PingAnimation2, PingKeys, 10);

			layers.Enqueue(CPULayer);
			layers.Enqueue(CPULayerBlink);
			layers.Enqueue(CPULayerRainbowCircle);
			layers.Enqueue(PingAnimation);
			layers.Enqueue(PingAnimation2);

			return layers.ToArray();
		}


		private class PingAnimation4 : PingAnimation3
		{

			public PingAnimation4(RectangleF region, int maxPing) : base(region, maxPing) { }
			protected static PingReply OldReply;
			protected long finalAnimationTime;

			public override void Render(EffectLayer effectLayer, long currentTime)
			{

				// layers							|											|
				// 0.OldReplyPingBar				|==========>								|
				// 1.PingShadowAnimation			|     >=-
				// 2.NewBar							|====>
				// 3.SignalAnimation				|  -=>

				float pingSignalWidth = Region.Width / 12 * 3;
				float pingShadowWidth = Region.Width / 12 * 3;
				long finalSuccessAnimationSpeed = 200;
				long finalFailAnimationSpeed = 1000;
				long pingAdvance = 200;
				var barSpectrum = new ColorSpectrum(Color.Lime, Color.Orange, Color.Red);
				var finalErrorSpectrum = new ColorSpectrum(Color.FromArgb(0, Color.Red), Color.Red,
					Color.Black, Color.Red, Color.Black);

				float pingPos = -pingShadowWidth;
				float pingOldBarWidth = 0;
				float pingNewBarWidth = 0;

				if (OldReply != null && OldReply.Status == IPStatus.Success)
				{
					pingOldBarWidth = Region.Width * OldReply.RoundtripTime / MaxPing;
					//(float)(Region.Width * (Math.Ceiling(OldReply.RoundtripTime * 12d / MaxPing) - 0.2) / 12);
				}

				if (Phase == PingPhase.Delay)
				{
					var pingNextCopy = pingNext;
					if (pingNextCopy != null && (currentTime - finalAnimationTime > 1500))
					{
						pingNext = null;
						pingNextCopy.SetResult(true);
					}
				}
				else
				{
					pingPos =
							Math.Max(0, currentTime - PingStartedTime - pingAdvance)
							* (Region.Width + (pingSignalWidth + pingShadowWidth)) / (MaxPing + pingAdvance)
							- pingShadowWidth;
					pingNewBarWidth = pingPos;
				}

				if (Phase == PingPhase.PingEnded)
				{
					if (Reply != null && Reply.Status == IPStatus.Success && pingPos >= Region.Width * Reply.RoundtripTime / MaxPing)
					{
						finalAnimationTime = currentTime;
						Phase = PingPhase.SuccessCompleteAnimation;
					}
					else if (pingPos >= Region.Width + pingSignalWidth)
					{
						finalAnimationTime = currentTime;
						Phase = PingPhase.TimeoutErrorAnimation;
					}
				}

				if (Phase == PingPhase.SuccessCompleteAnimation
					|| Phase == PingPhase.TimeoutErrorAnimation)
				{
					if (currentTime - finalAnimationTime >=
						(Phase == PingPhase.SuccessCompleteAnimation ?
						finalSuccessAnimationSpeed : finalFailAnimationSpeed))
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
							(currentTime - finalAnimationTime) *
							(Region.Width - pingNewBarFullWidth + pingSignalWidth) / (finalSuccessAnimationSpeed)
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
					Region.Left + pingPos - pingSignalWidth, Region.Top,
					Region.Left + pingPos, Region.Bottom);

				var pingShadowRect = RectangleF.FromLTRB(
					Region.Left + pingPos, Region.Top,
					Region.Left + pingPos + pingShadowWidth, Region.Bottom);

				var shadowBrush = new LinearGradientBrush(pingShadowRect,
					Color.Black, Color.FromArgb(0, Color.Black), LinearGradientMode.Horizontal);

				var signalBrush = new LinearGradientBrush(pingSignalRect,
					 Color.FromArgb(0, barSpectrum.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width))),
					 barSpectrum.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width)), LinearGradientMode.Horizontal);

				pingShadowRect.Intersect(Region);
				pingSignalRect.Intersect(Region);

				var pingBarBrush = barSpectrum.ToLinearGradient(Region.Width, Region.Height, Region.Left, Region.Top);

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
						g.FillRectangle(new SolidBrush(finalErrorSpectrum.GetColorAt(
							Math.Min(1, (currentTime - finalAnimationTime) / (float)finalFailAnimationSpeed))), Region);

				}
			}
		}

		private class PingAnimation3 : Pinger
		{
			private RectangleF region;

			public PingAnimation3(RectangleF region, int maxPing)
			{
				Region = region;
				MaxPing = maxPing;
			}

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
				set
				{
					region = value;
				}
			}

			protected int MaxPing { get; }

			public virtual void Render(EffectLayer effectLayer, long currentTime)
			{
				switch (Phase)
				{
					case PingPhase.Delay:
						var pingNextCopy = pingNext;
						if (pingNextCopy != null &&
						 (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 2000)
							)
						{
							pingNext = null;
							pingNextCopy.SetResult(true);
						}

						if (Reply != null)
							DrawBar(effectLayer, Math.Min(1, Reply.RoundtripTime / (float)MaxPing), 500, 0);
						break;
					case PingPhase.PingStarted:
						DrawBar(effectLayer, Math.Min(1, Reply.RoundtripTime / (float)MaxPing), currentTime - PingStartedTime, 0);
						break;
					case PingPhase.PingEnded:
						PingEndedTime = Utils.Time.GetMillisecondsSinceEpoch();
						DrawBar(effectLayer, Math.Min(1, Reply.RoundtripTime / (float)MaxPing), currentTime - PingStartedTime, 0);

						if (Reply == null || Reply.Status != IPStatus.Success)
						{
							Phase = PingPhase.TimeoutErrorAnimation;
						}
						else
						{
							Phase = PingPhase.SuccessCompleteAnimation;
						}
						break;
					case PingPhase.SuccessCompleteAnimation:

						DrawBar(effectLayer, Math.Min(1, Reply.RoundtripTime / (float)MaxPing), currentTime - PingStartedTime, Math.Max(0, currentTime - PingEndedTime));
						if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 1500)
							Phase = PingPhase.Delay;
						break;
					case PingPhase.TimeoutErrorAnimation:
						if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 1500)
							Phase = PingPhase.Delay;
						break;

				}

				//if (Phase == PingPhase.SuccessCompleteAnimation)
				//{
				//	DrawBar(effectLayer,
				//		(float)(Region.Width * Math.Ceiling((currentTime - PingStartedTime) * 12d / MaxPing) / 12),
				//		currentTime - PingStartedTime);

				//	DrawPing(effectLayer, deviceKeys, 0, (byte)(255 * Math.Max(0, 1 - (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) / 1000f)));

				//	//DrawPing(effectLayer, deviceKeys, (int)(((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * deviceKeys.Length) / 500));
				//	//DrawPing(effectLayer, deviceKeys, (int)(((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * deviceKeys.Length) / 500));
				//	if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 1500)
				//		Phase = PingPhase.Delay;
				//}
				//else if (Phase == PingPhase.TimeoutErrorAnimation)
				//{
				//	var timePassed = Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime;

				//	DrawPing(effectLayer, deviceKeys, 0,
				//		(byte)(255 * Math.Max(0, 1 - (timePassed % 750 / 750f) - timePassed / 1500f)), true);
				//	//for (int i = 0; i < deviceKeys.Length; i++)
				//	//{
				//	//	effectLayer.Set(deviceKeys[i], Color.FromArgb(255 - (byte)((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * 255 / 1000f), effectLayer.Get(deviceKeys[i])));
				//	//}
				//	if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 3000)
				//		Phase = PingPhase.Delay;
				//}



				////var gradient = new LinearGradientBrush(new PointF(Region.X, 0), new PointF(Region.Right, 0), Color.Lime, Color.Red);

				////ColorBlend colorBlend = new ColorBlend
				////{
				////	Colors = new[] { Color.Black, Color.Red },
				////	Positions = new float[] { 0, 1 }
				////};




				////ColorBlend colorBlend = new ColorBlend
				////{
				////	Colors = new[] { Color.Black, Color.Red },
				////	Positions = new float[] { 0, 1 }
				////};


			}

			private void DrawBar(EffectLayer effectLayer, float pingNormalized, long phase1Time, long phase2Time)
			{
				float pingPos = (float)(Region.Width * (Math.Ceiling(pingNormalized * 12d) - 0.2) / 12);
				using (var g = effectLayer.GetGraphics())
				{
					var pingRect = new RectangleF(Region.Left, Region.Top, pingPos, Region.Height);
					var pingShadow = new RectangleF(Region.Left + pingRect.Width, Region.Top, Region.Width, Region.Height);
					var afterPingRect =
						new RectangleF(Region.Left - pingPos + ((Region.Width + pingPos) * phase2Time / 1000f),
						Region.Top, pingPos, Region.Height);


					var pingShadowPhase1 = new RectangleF(pingShadow.Location, pingShadow.Size);
					pingShadowPhase1.Intersect(Region);

					var afterPingBlackRect = new RectangleF(afterPingRect.Location, afterPingRect.Size);
					afterPingBlackRect.Intersect(pingRect);
					afterPingBlackRect.Intersect(Region);
					var afterPingColorRect = new RectangleF(afterPingRect.Location, afterPingRect.Size);
					afterPingColorRect.Intersect(pingShadowPhase1);
					afterPingColorRect.Intersect(Region);

					var pingShadowPhase2 = new RectangleF(new PointF(Math.Max(afterPingRect.Right, pingRect.Right), Region.Top),
						new SizeF(pingShadowPhase1.Right - Math.Max(afterPingRect.Right, pingShadowPhase1.Left), Region.Height));

					var pingRectPhase2 = new RectangleF(new PointF(afterPingRect.Right, Region.Top),
						new SizeF(pingRect.Right - Math.Max(afterPingRect.Right, pingRect.Left), Region.Height));

					//pingRectPhase2.Offset(new PointF(0, Region.Height));
					//afterPingBlackRect.Offset(new PointF(0, 2 * Region.Height));
					//afterPingColorRect.Offset(new PointF(0, 3 * Region.Height));

					var shadowBrush = new LinearGradientBrush(pingShadow,
						Color.FromArgb((byte)(255 * Math.Min(1, phase1Time / 200f)), Color.Black),
						Color.FromArgb((byte)(255 * Math.Min(1, phase1Time / 400f)), Color.Black), LinearGradientMode.Horizontal);

					var pingBrush = new LinearGradientBrush(Region,
						new ColorSpectrum(Color.Lime, Color.Yellow, Color.Red).GetColorAt(Math.Min(1, phase1Time / 600f)), Color.Red, LinearGradientMode.Horizontal);

					var afterPingBlackBrush = new LinearGradientBrush(afterPingRect, Color.FromArgb(0, Color.Black), Color.Black,
						LinearGradientMode.Horizontal);

					shadowBrush.WrapMode = WrapMode.TileFlipX;
					pingBrush.WrapMode = WrapMode.TileFlipX;
					afterPingBlackBrush.WrapMode = WrapMode.TileFlipX;

					if (pingShadowPhase2.Width > 0)
						g.FillRectangle(shadowBrush, pingShadowPhase2);

					if (pingRectPhase2.Width > 0)
						g.FillRectangle(pingBrush, pingRectPhase2);

					if (afterPingBlackRect.Width > 0)
						g.FillRectangle(afterPingBlackBrush, afterPingBlackRect);

					if (afterPingColorRect.Width > 0)
						g.FillRectangle(new SolidBrush(new ColorSpectrum(Color.Lime, Color.Yellow, Color.Red).GetColorAt(pingNormalized)), afterPingColorRect);

					//if (afterPingColorRect.Width > 0)
					//	g.FillRectangle(new SolidBrush(new ColorSpectrum(Color.Lime, Color.Red).GetColorAt(pingNormalized)), pingRect);

				}
			}
		}

		private class PingAnimation
		{
			private readonly float pingWidth;
			private readonly float fieldWidth;
			private readonly ColorSpectrum spectrum;
			private long time;
			private long delay;

			public PingAnimation(float pingWidth, float fieldWidth, int delay, ColorSpectrum spectrum)
			{
				this.pingWidth = pingWidth;
				this.fieldWidth = fieldWidth;
				this.delay = delay;
				this.spectrum = spectrum;
				Ping();
			}

			public Color Render(float position, long nowtime)
			{
				var pingPos = (nowtime - time) * (2 * pingWidth + fieldWidth) / delay;
				var insidePingPos = position - pingPos + pingWidth + 1;
				if (insidePingPos < 0 || insidePingPos > pingWidth)
				{
					return Color.Transparent;
				}
				else
				{
					return spectrum.GetColorAt(insidePingPos, pingWidth);
				}
			}

			public void Ping()
			{
				time = Utils.Time.GetMillisecondsSinceEpoch();
			}
		}

		private class Pinger
		{
			protected static long PingStartedTime;
			protected static long PingEndedTime;
			protected static TaskCompletionSource<bool> pingNext;
			protected static PingReply Reply;
			protected static PingPhase Phase = PingPhase.Delay;

			private static readonly Task Updater = Task.Run((Action)(async () =>
			{
				while (true)
				{
					try
					{
						var ping = new Ping();
						while (true)
						{
							string host;
							switch (DateTime.Now.Millisecond % 4)
							{
								case 0:
									host = "yandex.ru";
									break;
								case 1:
									host = "google.com";
									break;
								case 2:
									host = "www.cyberforum.ru";
									break;
								case 3:
									host = "8.2.6.4";
									break;
								case 4:
									host = "1.2.5.4";
									break;
								case 5:
									host = "44.56.1.48";
									break;

								default:
									//host = "google.com";

									host = "ncsoft.kr";
									break;
							}
							//host = "yandex.ru";

							var pingReplyTask = ping.SendPingAsync(host);
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
							pingNext = new TaskCompletionSource<bool>();
							await pingNext.Task;
						}
					}
					catch (Exception exc)
					{
						Global.logger.LogLine("PingCounter exception: " + exc, Logging_Level.Error);
					}
					await Task.Delay(500);
				}
			}));

			protected enum PingPhase
			{
				Delay, PingStarted, PingEnded, SuccessCompleteAnimation, TimeoutErrorAnimation
			}
		}

		private class PingAnimation2 : Pinger
		{
			private static long PingWidth;
			private static ColorSpectrum ColorSpectrum = new ColorSpectrum(Color.Lime, Color.Red);
			private static float SpectrumWidth = 10;

			public void Render(EffectLayer effectLayer, DeviceKeys[] deviceKeys, int buttonTime)
			{
				switch (Phase)
				{
					case PingPhase.Delay:
						var pingNextCopy = pingNext;
						if (pingNextCopy != null)
						{
							pingNext = null;
							pingNextCopy.SetResult(true);
						}
						break;
					case PingPhase.PingStarted:
						PingWidth = (long)Math.Ceiling((Utils.Time.GetMillisecondsSinceEpoch() - PingStartedTime + 50) / (decimal)buttonTime);
						DrawPing(effectLayer, deviceKeys, 0);
						break;
					case PingPhase.PingEnded:
						PingEndedTime = Utils.Time.GetMillisecondsSinceEpoch();
						if (Reply == null || Reply.Status != IPStatus.Success)
						{
							Phase = PingPhase.TimeoutErrorAnimation;
							PingWidth = (long)Math.Ceiling((Utils.Time.GetMillisecondsSinceEpoch() - PingStartedTime) / (decimal)buttonTime);
						}
						else
						{
							Phase = PingPhase.SuccessCompleteAnimation;
							PingWidth = (long)Math.Ceiling(Reply.RoundtripTime / (decimal)buttonTime);
						}
						break;
				}

				if (Phase == PingPhase.SuccessCompleteAnimation)
				{
					DrawPing(effectLayer, deviceKeys, 0, (byte)(255 * Math.Max(0, 1 - (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) / 1000f)));

					//DrawPing(effectLayer, deviceKeys, (int)(((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * deviceKeys.Length) / 500));
					//DrawPing(effectLayer, deviceKeys, (int)(((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * deviceKeys.Length) / 500));
					if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 1500)
						Phase = PingPhase.Delay;
				}
				else if (Phase == PingPhase.TimeoutErrorAnimation)
				{
					var timePassed = Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime;

					DrawPing(effectLayer, deviceKeys, 0,
						(byte)(255 * Math.Max(0, 1 - (timePassed % 750 / 750f) - timePassed / 1500f)), true);
					//for (int i = 0; i < deviceKeys.Length; i++)
					//{
					//	effectLayer.Set(deviceKeys[i], Color.FromArgb(255 - (byte)((Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime) * 255 / 1000f), effectLayer.Get(deviceKeys[i])));
					//}
					if (Utils.Time.GetMillisecondsSinceEpoch() - PingEndedTime > 3000)
						Phase = PingPhase.Delay;
				}
			}

			private void DrawPing(EffectLayer effectLayer, DeviceKeys[] deviceKeys, int positionShift, byte alfa = 255, bool red = false)
			{
				for (int i = positionShift; i < deviceKeys.Length && i < PingWidth + positionShift; i++)
				{
					//effectLayer.Set(deviceKeys[i], Color.FromArgb(alfa, ColorSpectrum.GetColorAt(PingWidth, deviceKeys.Length)));
					if (red)
					{
						effectLayer.Set(deviceKeys[i], Color.FromArgb(alfa, Color.Red));
					}
					else if (i > SpectrumWidth + positionShift)
						effectLayer.Set(deviceKeys[i], Color.FromArgb(alfa, ColorSpectrum.GetColorAt(1)));
					else
						effectLayer.Set(deviceKeys[i], Color.FromArgb(alfa, ColorSpectrum.GetColorAt(i - positionShift, SpectrumWidth)));
				}
			}
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