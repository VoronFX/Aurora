using Aurora.EffectsEngine;
using Aurora.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Corale.Colore.Razer.Keyboard;

namespace Aurora.Profiles.Desktop
{
	public class Event_Idle : LightEvent
	{
		private long previoustime = 0;
		private long currenttime = 0;

		private Random randomizer;

		private LayerEffectConfig effect_cfg = new LayerEffectConfig();

		private Devices.DeviceKeys[] allKeys = Enum.GetValues(typeof(Devices.DeviceKeys)).Cast<Devices.DeviceKeys>().ToArray();
		private Dictionary<Devices.DeviceKeys, float> stars = new Dictionary<Devices.DeviceKeys, float>();
		private Dictionary<Devices.DeviceKeys, double> raindrops = new Dictionary<Devices.DeviceKeys, double>();
		long nextstarset = 0L;

		private float getDeltaTime()
		{
			return (currenttime - previoustime) / 1000.0f;
		}

		public Event_Idle()
		{
			randomizer = new Random();

			previoustime = currenttime;
			currenttime = Utils.Time.GetMillisecondsSinceEpoch();
		}

		private int count;
		private long time;
		public override void UpdateLights(EffectFrame frame)
		{
			previoustime = currenttime;
			currenttime = Utils.Time.GetMillisecondsSinceEpoch();

			Queue<EffectLayer> layers = new Queue<EffectLayer>();
			EffectLayer layer;

			effect_cfg.speed = Global.Configuration.idle_speed;

			switch (Global.Configuration.idle_type)
			{
				case IdleEffects.Dim:
					layer = new EffectLayer("Idle - Dim");

					layer.Fill(Color.FromArgb(125, 0, 0, 0));

					layers.Enqueue(layer);
					break;
				case IdleEffects.ColorBreathing:
					layer = new EffectLayer("Idle - Color Breathing");

					Color breathe_bg_color = Global.Configuration.idle_effect_secondary_color;
					layer.Fill(breathe_bg_color);

					float sine = (float)Math.Pow(Math.Sin((double)((currenttime % 10000L) / 10000.0f) * 2 * Math.PI * Global.Configuration.idle_speed), 2);

					layer.Fill(Color.FromArgb((byte)(sine * 255), Global.Configuration.idle_effect_primary_color));

					layers.Enqueue(layer);
					break;
				case IdleEffects.RainbowShift_Horizontal:
					layer = new EffectLayer("Idle - Rainbow Shift (Horizontal)", LayerEffects.RainbowShift_Horizontal, effect_cfg);

					layers.Enqueue(layer);
					break;
				case IdleEffects.RainbowShift_Vertical:
					layer = new EffectLayer("Idle - Rainbow Shift (Vertical)", LayerEffects.RainbowShift_Vertical, effect_cfg);

					layers.Enqueue(layer);
					break;
				case IdleEffects.StarFall:
					layer = new EffectLayer("Idle - Starfall");

					if (nextstarset < currenttime)
					{
						for (int x = 0; x < Global.Configuration.idle_amount; x++)
						{
							Devices.DeviceKeys star = allKeys[randomizer.Next(allKeys.Length)];
							if (stars.ContainsKey(star))
								stars[star] = 1.0f;
							else
								stars.Add(star, 1.0f);
						}

						nextstarset = currenttime + (long)(1000L * Global.Configuration.idle_frequency);
					}

					layer.Fill(Global.Configuration.idle_effect_secondary_color);

					Devices.DeviceKeys[] stars_keys = stars.Keys.ToArray();

					foreach (Devices.DeviceKeys star in stars_keys)
					{
						layer.Set(star, Utils.ColorUtils.MultiplyColorByScalar(Global.Configuration.idle_effect_primary_color, stars[star]));
						stars[star] -= getDeltaTime() * 0.05f * Global.Configuration.idle_speed;
					}

					layers.Enqueue(layer);
					break;
				case IdleEffects.RainFall:
					layer = new EffectLayer("Idle - Rainfall");

					if (nextstarset < currenttime)
					{
						for (int x = 0; x < Global.Configuration.idle_amount; x++)
						{
							Devices.DeviceKeys star = allKeys[randomizer.Next(allKeys.Length)];
							if (raindrops.ContainsKey(star))
								raindrops[star] = 1.0f;
							else
								raindrops.Add(star, 1.0f);
						}

						nextstarset = currenttime + (long)(1000L * Global.Configuration.idle_frequency);
					}

					layer.Fill(Global.Configuration.idle_effect_secondary_color);

					Devices.DeviceKeys[] raindrops_keys = raindrops.Keys.ToArray();

					ColorSpectrum drop_spec = new ColorSpectrum(Global.Configuration.idle_effect_primary_color, Color.FromArgb(0, Global.Configuration.idle_effect_primary_color));

					foreach (Devices.DeviceKeys raindrop in raindrops_keys)
					{
						PointF pt = Effects.GetBitmappingFromDeviceKey(raindrop).Center;

						float transition_value = (float) (1.0f - raindrops[raindrop]);
						float radius = transition_value * Effects.canvas_biggest;

						layer.GetGraphics().DrawEllipse(new Pen(drop_spec.GetColorAt(transition_value), 2),
							pt.X - radius,
							pt.Y - radius,
							2 * radius,
							2 * radius);

						raindrops[raindrop] -= getDeltaTime() * 0.05f * Global.Configuration.idle_speed;
					}

					layers.Enqueue(layer);
					break;
				case IdleEffects.RainFallSmooth:
					layer = new EffectLayer("Idle - RainfallSmooth");

					if (nextstarset < currenttime)
					{
						for (int x = 0; x < Global.Configuration.idle_amount; x++)
						{
							Devices.DeviceKeys star = allKeys[randomizer.Next(allKeys.Length)];
							if (raindrops.ContainsKey(star))
								raindrops[star] = 1.0f;
							else
								raindrops.Add(star, 1.0f);
						}

						nextstarset = currenttime + (long)(1000L * Global.Configuration.idle_frequency);
					}
					layer.Fill(Global.Configuration.idle_effect_secondary_color);
					var b = new Bitmap(Effects.canvas_width * 10, Effects.canvas_height * 10);
					var g = Graphics.FromImage(b);
					ColorSpectrum drop_spec2 = new ColorSpectrum(
						Global.Configuration.idle_effect_primary_color, 
						Color.FromArgb(0, Global.Configuration.idle_effect_primary_color));
					try
					{


						var s = new System.Diagnostics.Stopwatch();
						s.Start();
						var drops = raindrops.Keys.ToArray().Select(d =>
						{
							PointF pt = Effects.GetBitmappingFromDeviceKey(d).Center;
							float transitionValue = (float)(1.0f - raindrops[d]);
							//float transitionValue = (float)(currenttime - raindrops[d]) / 
							//	((8f-Global.Configuration.idle_speed)*1000);
							float radius = transitionValue * Effects.canvas_biggest;
							raindrops[d] -= getDeltaTime() * 0.05f * Global.Configuration.idle_speed;
							return new Tuple<Devices.DeviceKeys, PointF, float, float>(d, pt, transitionValue, radius);

						}).Where(d=> d.Item3 <= 1.5).ToArray();
						

						//if (drops.Length > 0)
						//	Debug.WriteLine($"Radius {drops[0].Item4}");
						//Debug.WriteLine($"time {getDeltaTime()}");

						float circleHalfThickness = 1f;

						foreach (var key in allKeys)
						{
							var keyInfo = Effects.GetBitmappingFromDeviceKey(key);
							var btnRadius = ((keyInfo.Width + keyInfo.Height) / 4f);
							if (btnRadius <= 0) continue;

							foreach (var raindrop in drops)
							{

								float circleInEdge = (raindrop.Item4 - circleHalfThickness);
								float circleOutEdge = (raindrop.Item4 + circleHalfThickness);
								circleInEdge *= circleInEdge;
								circleOutEdge *= circleOutEdge;

								float xKey = Math.Abs(keyInfo.Center.X - raindrop.Item2.X);
								float yKey = Math.Abs(keyInfo.Center.Y - raindrop.Item2.Y);
								float xKeyInEdge = xKey - btnRadius;
								float xKeyOutEdge = xKey + btnRadius;
								float yKeyInEdge = yKey - btnRadius;
								float yKeyOutEdge = yKey + btnRadius;
								float keyInEdge = xKeyInEdge * xKeyInEdge + yKeyInEdge * yKeyInEdge;
								float keyOutEdge = xKeyOutEdge * xKeyOutEdge + yKeyOutEdge * yKeyOutEdge;

								var btnDiameter = keyOutEdge - keyInEdge;
								var inEdgePercent = (circleOutEdge - keyInEdge) / btnDiameter;
								var outEdgePercent = (keyOutEdge - circleInEdge) / btnDiameter;
								var percent = Math.Min(1, Math.Max(0, inEdgePercent))
									+ Math.Min(1, Math.Max(0, outEdgePercent)) - 1f;
								//	Debug.WriteLine($"Percent {inEdgePercent}");

								if (percent > 0)
								{
									layer.Set(key, (Color)EffectColor.BlendColors(
										new EffectColor(layer.Get(key)),
										new EffectColor(drop_spec2.GetColorAt(raindrop.Item3)), percent));
								}
								//g.FillEllipse(new SolidBrush(Color.FromArgb((byte)(255 * percent), Color.Red)),
								//	keyInfo.Center.X, keyInfo.Center.Y, keyInfo.Width, keyInfo.Height);
								//g.FillEllipse(new SolidBrush(Color.FromArgb((byte)(255), Color.Blue)),
								//	keyInfo.Center.X * 10, keyInfo.Center.Y * 10, btnDiameter * 10, btnDiameter * 10);
							}
						}
						s.Stop();
						time += s.ElapsedMilliseconds;
						count++;

							Debug.WriteLine($"Effect time {time / (float)count}");
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
						throw;
					}
					//var circleWidth = 20f;
					//Func<float, float, float, float, float> calcAxePercent = (circleCenter, radius, leftEdge, rightEdge) =>
					//{
					//	var le = (leftEdge - (circleWidth / 2f)) / circleWidth;
					//	var re = (rightEdge - (circleWidth / 2f)) / circleWidth;
					//	var cp = (circleCenter + radius) / circleWidth;

					//	var percentL = Math.Min(1, Math.Max(0, cp - le)) - (1 - Math.Min(1, Math.Max(0, re - cp)));
					//	percentL = Math.Min(1, Math.Max(0, percentL));
					//	cp = (circleCenter - radius) / circleWidth;
					//	var percentR = Math.Min(1, Math.Max(0, cp - le)) - (1 - Math.Min(1, Math.Max(0, re - cp)));
					//	percentR = Math.Min(1, Math.Max(0, percentR));
					//	return Math.Max(percentL, percentR);
					//};

					//var res = 5f;
					//var b = new Bitmap(Effects.canvas_width, Effects.canvas_height);
					//var g = Graphics.FromImage(b);
					//for (float x = 0; x < Effects.canvas_width; x += res)
					//{
					//	for (float y = 0; y < Effects.canvas_height; y += res)
					//	{
					//		var xLevel1 = calcAxePercent(15, 5, x, x + res);

					//		var yLevel1 = calcAxePercent(15, 5, y, y + res);

					//		g.FillRectangle(new SolidBrush(Color.FromArgb((byte)(255 * xLevel1 * yLevel1), Color.Red)), x, y, res, res);
					//	}
					//}
					g.Dispose();
					//					b.Save($"test{count}.png");
					//foreach (var key in Enum.GetValues(typeof(Devices.DeviceKeys)).Cast<Devices.DeviceKeys>())
					//{
					//	var keyInfo = Effects.GetBitmappingFromDeviceKey(key);

					//	foreach (Devices.DeviceKeys raindrop in raindrops_keys)
					//	{
					//		PointF pt = Effects.GetBitmappingFromDeviceKey(raindrop).Center;

					//		float transition_value = 1.0f - raindrops[raindrop];
					//		float radius = transition_value * Effects.canvas_biggest;

					//		var xLevel = calcAxePercent(pt.X, radius, keyInfo.Left, keyInfo.Right)
					//			* Math.Min(1, circleWidth / keyInfo.Width);

					//		var yLevel = calcAxePercent(pt.Y, radius, keyInfo.Top, keyInfo.Bottom)
					//			* Math.Min(1, circleWidth / keyInfo.Height);


					//		layer.Set(key, (Color)EffectColor.BlendColors(
					//			new EffectColor(layer.Get(key)),
					//			new EffectColor(drop_spec.GetColorAt(transition_value)), xLevel));
					//	}
					//}

					//foreach (Devices.DeviceKeys raindrop in raindrops_keys)
					//{
					//	raindrops[raindrop] -= getDeltaTime() * 0.05f * Global.Configuration.idle_speed;
					//}



					//foreach (Devices.DeviceKeys raindrop in raindrops_keys)
					//{
					//	PointF pt = Effects.GetBitmappingFromDeviceKey(raindrop).Center;

					//	float transition_value = 1.0f - raindrops[raindrop];
					//	float radius = transition_value * Effects.canvas_biggest;


					//	layer.GetGraphics().DrawEllipse(new Pen(drop_spec.GetColorAt(transition_value), 2),
					//		pt.X - radius,
					//		pt.Y - radius,
					//		2 * radius,
					//		2 * radius);

					//	raindrops[raindrop] -= getDeltaTime() * 0.05f * Global.Configuration.idle_speed;
					//}

					layers.Enqueue(layer);
					break;
				case IdleEffects.Blackout:
					layer = new EffectLayer("Idle - Blackout");

					layer.Fill(Color.Black);

					layers.Enqueue(layer);
					break;
				default:
					break;
			}

			frame.AddOverlayLayers(layers.ToArray());
		}

		public override void UpdateLights(EffectFrame frame, IGameState new_game_state)
		{
			//This event does not take a game state
			UpdateLights(frame);
		}

		public override bool IsEnabled()
		{
			return true;
		}
	}
}
