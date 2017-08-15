using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{
	public class CaterpillarDemo : IEffectScript
	{
		public string ID
		{
			get { return "CaterpillarDemo"; }
		}

		public KeySequence DefaultKeys;

		public VariableRegistry Properties
		{
			get
			{
				var prop = new VariableRegistry();
				prop.Register("Smooth mode", 0L, "0 - Normal (no caterpillar)\r\n 1 - Caterpillar\r\n 2 - Hybrid (Caterpillar inside only)", 2L, 0L);
				prop.Register("Freeform", new KeySequence(new FreeFormObject() { Height = 300, Width = 1000, X = 0, Y = 0 }));
				prop.Register("Speed", 20000L, "", 50000L, 1000L);
				return prop;
			}
		}


		public object UpdateLights(VariableRegistry properties, IGameState state = null)
		{
			var speed = properties.GetVariable<long>("Speed");
			var blinkingLevel = Math.Abs(1f - (Utils.Time.GetMillisecondsSinceEpoch() % speed) / (speed / 2f));

			var layer = new EffectLayer();
			var freeform = properties.GetVariable<KeySequence>("Freeform").freeform;
			var gradient = ColorSpectrum.RainbowLoop;
			using (Graphics g = layer.GetGraphics())
			{
				g.Clear(Color.Black);
				switch (properties.GetVariable<long>("Smooth mode"))
				{
					case 0L:
						{
							float width = (float)(freeform.Width * Effects.editor_to_canvas_width) / 3;
							float height = (float)(freeform.Height * Effects.editor_to_canvas_height);
							float x_pos = (float)Math.Round((freeform.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width + blinkingLevel * width);
							float y_pos = (float)Math.Round((freeform.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height);

							if (width < 3) width = 3;
							if (height < 3) height = 3;

							Rectangle rect = new Rectangle((int)x_pos, (int)y_pos, (int)width, (int)height);

							PointF rotatePoint = new PointF(x_pos + (width / 2.0f), y_pos + (height / 2.0f));

							Matrix myMatrix = new Matrix();
							myMatrix.RotateAt(freeform.Angle, rotatePoint, MatrixOrder.Append);

							g.Transform = myMatrix;
							LinearGradientBrush brush = gradient.ToLinearGradient(width, 0, x_pos, 0);
							brush.WrapMode = WrapMode.TileFlipX;

							g.FillRectangle(brush, rect);
						}
						break;
					case 1L:
						{
							float width = (float)(freeform.Width * Effects.editor_to_canvas_width) / 3;
							float height = (float)(freeform.Height * Effects.editor_to_canvas_height);
							float x_pos = (float)((freeform.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width + blinkingLevel * width);
							float y_pos = (float)((freeform.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height);

							RectangleF rect = new RectangleF(x_pos, y_pos, width, height);

							PointF rotatePoint = new PointF(x_pos + (width / 2.0f), y_pos + (height / 2.0f));

							Matrix myMatrix = new Matrix();
							myMatrix.RotateAt(freeform.Angle, rotatePoint, MatrixOrder.Append);

							g.Transform = myMatrix;
							LinearGradientBrush brush = gradient.ToLinearGradient(width, 0, x_pos, 0);
							brush.WrapMode = WrapMode.TileFlipX;
							g.FillRectangle(brush, rect);
						}
						break;

					case 2L:
						{
							float width = (float)(freeform.Width * Effects.editor_to_canvas_width) / 3;
							float height = (float)(freeform.Height * Effects.editor_to_canvas_height);
							float x_pos = (float)((freeform.X + Effects.grid_baseline_x) * Effects.editor_to_canvas_width + blinkingLevel * width);
							float y_pos = (float)((freeform.Y + Effects.grid_baseline_y) * Effects.editor_to_canvas_height);

							if (width < 3) width = 3;
							if (height < 3) height = 3;

							Rectangle rect = new Rectangle((int)x_pos, (int)y_pos, (int)width, (int)height);


							PointF rotatePoint = new PointF(x_pos + (width / 2.0f), y_pos + (height / 2.0f));

							Matrix myMatrix = new Matrix();
							myMatrix.RotateAt(freeform.Angle, rotatePoint, MatrixOrder.Append);

							g.Transform = myMatrix;
							LinearGradientBrush brush = gradient.ToLinearGradient(width, 0, x_pos, 0);
							brush.WrapMode = WrapMode.TileFlipX;

							g.FillRectangle(brush, rect);
						}
						break;
				}
			}
			return layer;
		}

	}

}