using Aurora.EffectsEngine;
using Aurora.Profiles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Aurora.Settings.Layers
{
	public class PercentLayerHandlerProperties<TProperty> : LayerHandlerProperties2Color<TProperty> where TProperty : PercentLayerHandlerProperties<TProperty>
	{
		public PercentEffectType? _PercentType { get; set; }

		[JsonIgnore]
		public PercentEffectType PercentType { get { return Logic._PercentType ?? _PercentType ?? PercentEffectType.Progressive_Gradual; } }

		public double? _BlinkThresholdStart { get; set; }

		[JsonIgnore]
		public double BlinkThresholdStart { get { return Logic._BlinkThresholdStart ?? _BlinkThresholdStart ?? 0.0; } }

		public double? _BlinkThresholdMaximum { get; set; }

		[JsonIgnore]
		public double BlinkThresholdMaximum { get { return Logic._BlinkThresholdMaximum ?? _BlinkThresholdMaximum ?? 0.0; } }

		public int? _BlinkSpeed { get; set; }

		[JsonIgnore]
		public int BlinkSpeed { get { return Logic._BlinkSpeed ?? _BlinkSpeed ?? 1000; } }

		public bool? _BlinkDirection { get; set; }

		[JsonIgnore]
		public bool BlinkDirection { get { return Logic._BlinkDirection ?? _BlinkDirection ?? false; } }

		public string _VariablePath { get; set; }

		[JsonIgnore]
		public string VariablePath { get { return Logic._VariablePath ?? _VariablePath ?? string.Empty; } }

		public string _MinVariablePath { get; set; }

		[JsonIgnore]
		public string MinVariablePath { get { return Logic._MinVariablePath ?? _MinVariablePath ?? string.Empty; } }

		public string _MaxVariablePath { get; set; }

		[JsonIgnore]
		public string MaxVariablePath { get { return Logic._MaxVariablePath ?? _MaxVariablePath ?? string.Empty; } }

		public PercentLayerHandlerProperties() : base() { }

		public PercentLayerHandlerProperties(bool assign_default = false) : base(assign_default) { }

		public override void Default()
		{
			base.Default();
			this._PrimaryColor = Utils.ColorUtils.GenerateRandomColor();
			this._SecondaryColor = Utils.ColorUtils.GenerateRandomColor();
			this._PercentType = PercentEffectType.Progressive_Gradual;
			this._BlinkThresholdStart = 0.0;
			this._BlinkThresholdMaximum = 0.0;
			this._BlinkDirection = false;
		}
	}

	public class PercentLayerHandlerProperties : PercentLayerHandlerProperties<PercentLayerHandlerProperties>
	{
		public PercentLayerHandlerProperties() : base() { }

		public PercentLayerHandlerProperties(bool empty = false) : base(empty) { }
	}

	public class PercentLayerHandler<TProperty> : LayerHandler<TProperty> where TProperty : PercentLayerHandlerProperties<TProperty>
	{
		private static double ParseVariablePath(IGameState state, string variable)
		{
			double value;
			if (!double.TryParse(variable, out value) && !string.IsNullOrWhiteSpace(variable))
			{
				try
				{
					value = Convert.ToDouble(Utils.GameStateUtils.RetrieveGameStateParameter(state, variable));
				}
				catch (Exception exc)
				{
					value = 0;
				}
			}
			return value;
		}

		public override EffectLayer Render(IGameState state)
		{
			double value = ParseVariablePath(state, Properties.VariablePath);
			double minvalue = ParseVariablePath(state, Properties.MinVariablePath);
			double maxvalue = ParseVariablePath(state, Properties.MaxVariablePath);

			value -= minvalue;

			return new EffectLayer().PercentEffect(Properties.PrimaryColor, Properties.SecondaryColor, Properties.Sequence, value, maxvalue, Properties.PercentType, Properties.BlinkThresholdStart, Properties.BlinkThresholdMaximum, Properties.BlinkSpeed);
		}

		public override void SetProfile(ProfileManager profile)
		{
			if (profile != null)
			{
				double value;
				if (!double.TryParse(Properties._VariablePath, out value) && !string.IsNullOrWhiteSpace(Properties._VariablePath) && !profile.ParameterLookup.ContainsKey(Properties._VariablePath))
					Properties._VariablePath = string.Empty;

				if (!double.TryParse(Properties._MinVariablePath, out value) && !string.IsNullOrWhiteSpace(Properties._MinVariablePath) && !profile.ParameterLookup.ContainsKey(Properties._MinVariablePath))
					Properties._MaxVariablePath = string.Empty;

				if (!double.TryParse(Properties._MaxVariablePath, out value) && !string.IsNullOrWhiteSpace(Properties._MaxVariablePath) && !profile.ParameterLookup.ContainsKey(Properties._MaxVariablePath))
					Properties._MaxVariablePath = string.Empty;
			}
			(Control as Control_PercentLayer).SetProfile(profile);
		}
	}

	public class PercentLayerHandler : PercentLayerHandler<PercentLayerHandlerProperties>
	{
		public PercentLayerHandler() : base()
		{
			_Type = LayerType.Percent;
		}

		protected override UserControl CreateControl()
		{
			return new Control_PercentLayer(this);
		}
	}
}
