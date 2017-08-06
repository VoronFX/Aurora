//
// Voron Scripts - PingEffect
// v1.0-beta.6
// https://github.com/VoronFX/Aurora
// Copyright (C) 2016 Voronin Igor <Voron.exe@gmail.com>
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
	public class PingEffect : IEffectScript
	{
		public string ID
		{
			get { return "Voron Scripts - PingEffect - v1.0-beta.6"; }
		}

		public KeySequence DefaultKeys;

		public VariableRegistry Properties { get; private set; }

		internal enum EffectTypes
		{
			[Description("Ping pulse")]
			PingPulse = 0,

			[Description("Ping graph")]
			PingGraph = 1,
		}

		public PingEffect()
		{
			Properties = new VariableRegistry();

			Properties.RegProp("Keys or Freestyle",
				new KeySequence(new[] {
					DeviceKeys.F1, DeviceKeys.F2, DeviceKeys.F3, DeviceKeys.F4, DeviceKeys.F5,  DeviceKeys.F6,
					DeviceKeys.F7,  DeviceKeys.F8,  DeviceKeys.F9,  DeviceKeys.F10, DeviceKeys.F11, DeviceKeys.F12
				}));

			Properties.RegProp("Effect type", (long)EffectTypes.PingPulse,
				String.Join(Environment.NewLine,
				Enum.GetValues(typeof(EffectTypes)).Cast<EffectTypes>().Select(x => string.Format("{0} - {1}", (int)x, x))),
				(long)Enum.GetValues(typeof(EffectTypes)).Cast<EffectTypes>().Min(),
				(long)Enum.GetValues(typeof(EffectTypes)).Cast<EffectTypes>().Max());

			Properties.RegProp("Default Host", "google.com", "Ping this host when no known apps in foreground.");
			Properties.RegProp("Per App Hosts", "185.40.64.69 \"LolClient.exe\"",
				"Ping special host (i.e. game server) when certain app is in foreground. Separate apps with \"|\".");

			Properties.RegProp("Max Ping", 400L, "Such pings or higher will fill full bar.", 50L, 5000L);
			Properties.RegProp("AimationReserveDelay", 200L, String.Join(Environment.NewLine,
				"Reserve delay between actual ping response and animation in (ms).",
				"The lower the value the more recent information you see, the higher the value the less chance of animation glitches."), 0L, 1000L);

			Properties.RegProp("Ping Signal Width", 17L, "Width of ping signal", 0L, 100L);
			Properties.RegProp("Ping Shadow Width", 17L, "Width of shadow preceding ping signal", 0L, 100L);

			Properties.RegProp("Fail Animation Duration", 1000L, "Duration of failed ping animation in (ms)", 200L, 5000L);
			Properties.RegProp("Success Animation Duration", 500L, "Duration of succeeded ping animation in (ms)", 200L, 5000L);
			Properties.RegProp("Time Between Requests", 1500L, "Width of shadow preceding ping signal in (ms)", 200L, 10000L);

			Properties.RegProp("Gradient", "#FF00FF00 | #FFFFA500 | #FFFF0000",
				String.Join(Environment.NewLine,
				"Gradient that is used for effect. Separate color points with \"|\".",
				"Optionally set point position with \"@\" symbol."));

			Properties.RegProp("Fail Animation Gradient", "#00FF0000 | #FFFF0000 | #FF000000 | #FFFF0000 | #FF000000",
				String.Join(Environment.NewLine,
					"Gradient that is used for animation failed ping. Separate color points with \"|\".",
					"Optionally set point position with \"@\" symbol."));
		}

		private static readonly ConcurrentDictionary<Tuple<KeySequence, string, string>, Pinger> pingAnimations
			= new ConcurrentDictionary<Tuple<KeySequence, string, string>, Pinger>();

		private static readonly ConcurrentDictionary<string, ColorSpectrum> gradients
			= new ConcurrentDictionary<string, ColorSpectrum>();

		private KeySequence Keys { get; set; }
		private EffectTypes EffectType { get; set; }

		private string DefaultHost { get; set; }
		private string PerAppHosts { get; set; }

		private long MaxPing { get; set; }
		private long AimationReserveDelay { get; set; }

		private float PingSignalWidth { get; set; }
		private float PingShadowWidth { get; set; }

		private long FailAnimationDuration { get; set; }
		private long SuccessAnimationDuration { get; set; }
		private long TimeBetweenRequests { get; set; }
		private ColorSpectrum Gradient { get; set; }
		private ColorSpectrum FailAnimationGradient { get; set; }

		private Pinger Pinger { get; set; }

		private void ReadProperties(VariableRegistry properties)
		{
			Keys = properties.GetVariable<KeySequence>("Keys or Freestyle");
			EffectType = (EffectTypes)properties.GetVariable<long>("Effect type");

			PingSignalWidth = properties.GetVariable<long>("Ping Signal Width") / 100f;
			PingShadowWidth = properties.GetVariable<long>("Ping Shadow Width") / 100f;

			FailAnimationDuration = properties.GetVariable<long>("Fail Animation Duration");
			SuccessAnimationDuration = properties.GetVariable<long>("Success Animation Duration");
			TimeBetweenRequests = properties.GetVariable<long>("Time Between Requests");

			Gradient = gradients.GetOrAdd(properties.GetVariable<string>("Gradient"), ScriptHelper.StringToSpectrum);
			FailAnimationGradient = gradients.GetOrAdd(properties.GetVariable<string>("Fail Animation Gradient"), ScriptHelper.StringToSpectrum);

			DefaultHost = properties.GetVariable<string>("Default Host");
			PerAppHosts = properties.GetVariable<string>("Per App Hosts");

			MaxPing = properties.GetVariable<long>("Max Ping");
			AimationReserveDelay = properties.GetVariable<long>("Aimation Reserve Delay");

			Pinger = pingAnimations.GetOrAdd(new Tuple<KeySequence, string, string>(Keys, DefaultHost, PerAppHosts),
				key => new Pinger(DefaultHost, PerAppHosts.Split('|')
					.Select(x => x.Trim().Split(' ').Select(x2 => x2.Trim().ToLower()))
					.ToDictionary(s => s.First(), s => s.Last())));
		}

		public object UpdateLights(VariableRegistry properties, IGameState state = null)
		{
			ReadProperties(properties);

			var layer = new EffectLayer(ID);
			Render(layer, Time.GetMillisecondsSinceEpoch());
			return layer;
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

			if (Pinger.OldReply != null && Pinger.OldReply.Status == IPStatus.Success)
			{
				pingOldBarWidth = Pinger.OldReply.RoundtripTime / (float)MaxPing;
				//(float)(Region.Width * (Math.Ceiling(OldReply.RoundtripTime * 12d / MaxPing) - 0.2) / 12);
			}

			if (Pinger.Phase == Pinger.PingPhase.WaitingForActivation)
			{
				if (currentTime - Pinger.PingStartedTime > TimeBetweenRequests)
					Pinger.AllowNextPing();
			}
			else if (Pinger.Phase != Pinger.PingPhase.Activated)
			{
				pingPos = Math.Max(0, currentTime - Pinger.PingStartedTime - AimationReserveDelay)
						  * (1f + (PingSignalWidth + PingShadowWidth)) / (MaxPing + AimationReserveDelay)
						  - PingShadowWidth;
				pingNewBarWidth = pingPos;
			}

			if (Pinger.Phase == Pinger.PingPhase.PingEnded)
			{
				if (Pinger.Reply != null && Pinger.Reply.Status == IPStatus.Success)
				{
					if (pingPos >= Pinger.Reply.RoundtripTime / (float)MaxPing)
					{
						Pinger.FinalAnimationTime = currentTime;
						Pinger.Phase = Pinger.PingPhase.SuccessCompleteAnimation;
					}
				}
				else if (pingPos >= 1 + PingSignalWidth)
				{
					Pinger.FinalAnimationTime = currentTime;
					Pinger.Phase = Pinger.PingPhase.TimeoutErrorAnimation;
				}
			}

			if (Pinger.Phase == Pinger.PingPhase.SuccessCompleteAnimation
				|| Pinger.Phase == Pinger.PingPhase.TimeoutErrorAnimation)
			{
				if (currentTime - Pinger.FinalAnimationTime >=
					(Pinger.Phase == Pinger.PingPhase.SuccessCompleteAnimation ?
						SuccessAnimationDuration : FailAnimationDuration))
				{
					Pinger.OldReply = Pinger.Reply;
					Pinger.Reply = null;
					Pinger.Phase = Pinger.PingPhase.WaitingForActivation;
					Render(effectLayer, currentTime);
					return;
				}

				if (Pinger.Phase == Pinger.PingPhase.SuccessCompleteAnimation)
				{
					float pingNewBarFullWidth = Pinger.Reply.RoundtripTime / (float)MaxPing;

					pingPos =
						(currentTime - Pinger.FinalAnimationTime) *
						(1 - pingNewBarFullWidth + PingSignalWidth) / (SuccessAnimationDuration)
						+ pingNewBarFullWidth;

					pingNewBarWidth = Math.Min(pingPos, pingNewBarFullWidth);
				}
			}

			if (Keys != null)
			{
				// Animating by keys. Prefferable. 

				Func<float, float, float, float> getBlend2 = (l, pos, r) =>
				{
					var leftEdgePercent = (1 + pos) / Keys.keys.Count - l;
					var rightEdgePercent = r - (pos / Keys.keys.Count);
					leftEdgePercent /= 1f / Keys.keys.Count;
					rightEdgePercent /= 1f / Keys.keys.Count;
					leftEdgePercent = 1 - Math.Max(0, Math.Min(1, leftEdgePercent));
					rightEdgePercent = 1 - Math.Max(0, Math.Min(1, rightEdgePercent));
					return 1 - leftEdgePercent - rightEdgePercent;
				};

				for (int i = 0; i < Keys.keys.Count; i++)
				{
					var keyColor = new EffectColor(Color.Black);
					float kL = i / (Keys.keys.Count - 1f);


					keyColor.BlendColors(new EffectColor(Gradient.GetColorAt(kL)), getBlend2(pingPos, i, pingOldBarWidth));
					keyColor.BlendColors(new EffectColor(Color.Black),
						getBlend2(pingPos, i, pingPos + PingShadowWidth)
						* (1 - ((i / (float)Keys.keys.Count - pingPos) / PingShadowWidth)));
					keyColor.BlendColors(new EffectColor(Gradient.GetColorAt(kL)),
						getBlend2(0, i, Math.Min(pingPos, pingNewBarWidth)));
					keyColor.BlendColors(new EffectColor(Gradient.GetColorAt(Math.Min(1, pingNewBarWidth))),
						getBlend2(pingPos - PingSignalWidth, i, pingPos)
						* (((i + 1) / (float)Keys.keys.Count - (pingPos - PingSignalWidth)) / PingSignalWidth));

					if (Pinger.Phase == Pinger.PingPhase.TimeoutErrorAnimation)
						keyColor += new EffectColor(FailAnimationGradient.GetColorAt(
							Math.Min(1, (currentTime - Pinger.FinalAnimationTime) / (float)FailAnimationDuration)));

					effectLayer.Set(Keys.keys[i], (Color)keyColor);
				}

			}
			else
			{
				//// Animating by rectangle.
				//pingPos *= Region.Width;
				//pingOldBarWidth *= Region.Width;
				//pingNewBarWidth *= Region.Width;

				//var oldPingBarRect = RectangleF.FromLTRB(
				//	Region.Left + pingPos, Region.Top,
				//	Region.Left + pingOldBarWidth, Region.Bottom);

				//oldPingBarRect.Intersect(Region);

				//var newPingBarRect = RectangleF.FromLTRB(
				//	Region.Left, Region.Top,
				//	Region.Left + Math.Min(pingNewBarWidth, pingPos), Region.Bottom);

				//newPingBarRect.Intersect(Region);

				//var pingSignalRect = RectangleF.FromLTRB(
				//	Region.Left + pingPos - (PingSignalWidth * Region.Width), Region.Top,
				//	Region.Left + pingPos, Region.Bottom);

				//var pingShadowRect = RectangleF.FromLTRB(
				//	Region.Left + pingPos, Region.Top,
				//	Region.Left + pingPos + (PingShadowWidth * Region.Width), Region.Bottom);

				//var shadowBrush = new LinearGradientBrush(pingShadowRect,
				//	Color.Black, Color.FromArgb(0, Color.Black), LinearGradientMode.Horizontal);

				//var signalBrush = new LinearGradientBrush(pingSignalRect,
				//	Color.FromArgb(0, Gradient.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width))),
				//	Gradient.GetColorAt(Math.Min(1, pingNewBarWidth / Region.Width)), LinearGradientMode.Horizontal);

				//pingShadowRect.Intersect(Region);
				//pingSignalRect.Intersect(Region);

				//var pingBarBrush = Gradient.ToLinearGradient(Region.Width, Region.Height, Region.Left, Region.Top);

				//shadowBrush.WrapMode = WrapMode.TileFlipX;
				//signalBrush.WrapMode = WrapMode.TileFlipX;
				//pingBarBrush.WrapMode = WrapMode.TileFlipX;

				//using (var g = effectLayer.GetGraphics())
				//{

				//	if (oldPingBarRect.Width > 0)
				//		g.FillRectangle(pingBarBrush, oldPingBarRect);

				//	if (pingShadowRect.Width > 0)
				//		g.FillRectangle(shadowBrush, pingShadowRect);

				//	if (newPingBarRect.Width > 0)
				//		g.FillRectangle(pingBarBrush, newPingBarRect);

				//	if (pingSignalRect.Width > 0)
				//		g.FillRectangle(signalBrush, pingSignalRect);

				//	if (Pinger.Phase == Pinger.PingPhase.TimeoutErrorAnimation)
				//		g.FillRectangle(new SolidBrush(FailAnimationGradient.GetColorAt(
				//			Math.Min(1, (currentTime - Pinger.FinalAnimationTime) / (float)FailAnimationDuration))), Region);

				//}
			}
		}

	}

	internal static class ScriptHelper
	{
		public static void RegProp(this VariableRegistry registry,
			string name, object defaultValue, string remark = "", object min = null, object max = null)
		{
			registry.Register(name, defaultValue, name, max, min, remark);
		}

		public static string SpectrumToString(ColorSpectrum spectrum)
		{
			return string.Join(" | ",
				spectrum.GetSpectrumColors().Select(x => string.Format("#{0:X2}{1:X2}{2:X2}{3:X2} @ {4}", x.Value.A, x.Value.R,
					x.Value.G, x.Value.B, x.Key)));
		}

		public static ColorSpectrum StringToSpectrum(string text)
		{
			var colors = text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Trim().Split('@').Select(x2 => x2.Trim()).ToArray())
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

		public static KeyValuePair<string, ColorSpectrum> UpdateSpectrumProperty(KeyValuePair<string, ColorSpectrum> current,
			string newValue)
		{
			return newValue == current.Key ? current :
				new KeyValuePair<string, ColorSpectrum>(newValue, StringToSpectrum(newValue));
		}
	}

	internal struct PingSettings
	{

	}

	//internal sealed class PingAnimation : Pinger
	//{
	//	private RectangleF region;

	//	public RectangleF Region
	//	{
	//		get
	//		{
	//			return new RectangleF(
	//				(float)Math.Round((region.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width),
	//				(float)Math.Round((region.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height),
	//				(float)Math.Round(region.Width * Effects.editor_to_canvas_width),
	//				(float)Math.Round(region.Height * Effects.editor_to_canvas_height));
	//		}
	//		set { region = value; }
	//	}

	//	public DeviceKeys[] Keys { get; set; }

	//	protected static PingReply OldReply;
	//	protected long FinalAnimationTime;

	//	public PingAnimation()
	//	{
	//		Region = new Rectangle(60, -3, 495, 35); // Region 

	//	}

	//}

	internal class Pinger
	{
		public static PingReply OldReply;
		public long FinalAnimationTime;

		public long PingStartedTime;
		protected long PingEndedTime;
		public PingReply Reply;
		public PingPhase Phase = PingPhase.WaitingForActivation;

		private Dictionary<string, string> HostsPerApplication { get; set; }
		private string DefaultHost { get; set; }

		public enum PingPhase
		{
			WaitingForActivation, Activated, PingStarted, PingEnded, SuccessCompleteAnimation, TimeoutErrorAnimation
		}

		private readonly Task updater;

		public Pinger(string defaultHost, Dictionary<string, string> hostsPerApplication)
		{
			DefaultHost = defaultHost;
			HostsPerApplication = hostsPerApplication;
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
			var currentApp = GetActiveWindowsProcessname();
			if (!string.IsNullOrWhiteSpace(currentApp))
				currentApp = Path.GetFileName(currentApp).ToLowerInvariant();

			if (!string.IsNullOrWhiteSpace(currentApp) && HostsPerApplication != null)
			{
				string host;
				if (HostsPerApplication.TryGetValue(currentApp, out host))
				{
					return host;
				}
			}
			return DefaultHost;
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