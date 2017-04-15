using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Aurora.Settings.Layers
{
	/// <summary>
	/// Interaction logic for Control_PercentLayer.xaml
	/// </summary>
	public partial class Control_PercentLayer : UserControl
	{
		private bool settingsset = false;
		private bool profileset = false;

		public Control_PercentLayer()
		{
			InitializeComponent();
		}

		public Control_PercentLayer(PercentLayerHandler datacontext)
		{
			InitializeComponent();

			this.DataContext = datacontext;
		}

		public PercentLayerHandler CurrentDataContext => DataContext as PercentLayerHandler;
		public string Current { get; set; }

		public void SetSettings()
		{
			var percentLayerHandler = DataContext as PercentLayerHandler;
			if (percentLayerHandler != null && !settingsset)
			{
				// this.ComboBoxVariable.Text = percentLayerHandler.Properties._VariablePath;
				//this.ComboBoxMaxVariable.Text = percentLayerHandler.Properties._MaxVariablePath;
				//this.ColorPickerProgressColor.SelectedColor = Utils.ColorUtils.DrawingColorToMediaColor(percentLayerHandler.Properties._PrimaryColor ?? System.Drawing.Color.Empty);
				//this.ColorPicker_backgroundColor.SelectedColor = Utils.ColorUtils.DrawingColorToMediaColor(percentLayerHandler.Properties._SecondaryColor ?? System.Drawing.Color.Empty);
				this.ComboBox_effect_type.SelectedIndex = (int)percentLayerHandler.Properties._PercentType;
				// this.updown_blink_value.Value = (int)(percentLayerHandler.Properties._BlinkThresholdStart * 100);
				//this.CheckBoxThresholdReverse.IsChecked = percentLayerHandler.Properties._BlinkDirection;
				this.KeySequence_keys.Sequence = percentLayerHandler.Properties._Sequence;
				settingsset = true;
			}

			//if (this.DataContext is PercentLayerHandler && !settingsset)
			//{
			//	//this.ComboBoxVariable.Text = (this.DataContext as PercentLayerHandler).Properties._VariablePath;
			//	this.ComboBoxMaxVariable.Text = (this.DataContext as PercentLayerHandler).Properties._MaxVariablePath;
			//	this.ColorPicker_progressColor.SelectedColor = Utils.ColorUtils.DrawingColorToMediaColor((this.DataContext as PercentLayerHandler).Properties._PrimaryColor ?? System.Drawing.Color.Empty);
			//	this.ColorPicker_backgroundColor.SelectedColor = Utils.ColorUtils.DrawingColorToMediaColor((this.DataContext as PercentLayerHandler).Properties._SecondaryColor ?? System.Drawing.Color.Empty);
			//	this.ComboBox_effect_type.SelectedIndex = (int)(this.DataContext as PercentLayerHandler).Properties._PercentType;
			//	// this.updown_blink_value.Value = (int)((this.DataContext as PercentLayerHandler).Properties._BlinkThresholdStart * 100);
			//	this.CheckBoxThresholdReverse.IsChecked = (this.DataContext as PercentLayerHandler).Properties._BlinkDirection;
			//	this.KeySequence_keys.Sequence = (this.DataContext as PercentLayerHandler).Properties._Sequence;
			//	settingsset = true;
			//}
		}

		internal void SetProfile(ProfileManager profile)
		{
			if (profile != null && !profileset)
			{
				var var_types_numerical = profile.ParameterLookup?.Where(kvp => Utils.TypeUtils.IsNumericType(kvp.Value.Item1));

				this.ComboBoxVariable.Items.Clear();
				foreach (var item in var_types_numerical)
					this.ComboBoxVariable.Items.Add(item.Key);

				this.ComboBoxMaxVariable.Items.Clear();
				foreach (var item in var_types_numerical)
					this.ComboBoxMaxVariable.Items.Add(item.Key);

				profileset = true;
			}
			settingsset = false;
			this.SetSettings();
		}

		private void KeySequence_keys_SequenceUpdated(object sender, EventArgs e)
		{
			if (IsLoaded && settingsset && this.DataContext is PercentLayerHandler && sender is Aurora.Controls.KeySequence)
				(this.DataContext as PercentLayerHandler).Properties._Sequence = (sender as Aurora.Controls.KeySequence).Sequence;
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			SetSettings();

			this.Loaded -= UserControl_Loaded;
		}

		private void ComboBox_effect_type_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (IsLoaded && settingsset && this.DataContext is PercentLayerHandler && sender is ComboBox)
			{
				(this.DataContext as PercentLayerHandler).Properties._PercentType = (PercentEffectType)Enum.Parse(typeof(PercentEffectType), (sender as ComboBox).SelectedIndex.ToString());
			}
		}

	}

	public class MediaColorToDrawingColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? (object)null : Utils.ColorUtils.MediaColorToDrawingColor((System.Windows.Media.Color)value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? (object)null : Utils.ColorUtils.DrawingColorToMediaColor((System.Drawing.Color)value);
		}
	}

	public class DrawingColorToMediaColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? (object)null : Utils.ColorUtils.DrawingColorToMediaColor((System.Drawing.Color)value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? (object)null : Utils.ColorUtils.MediaColorToDrawingColor((System.Windows.Media.Color)value);
		}
	}

	//public class BlinkingEffect
	//{
	//	private long lastTime;
	//	private int lastSpeed;
	//	private int lastPhase;
	//	private double lastLevel;

	//	public double GetLevel(long currentTime, int blinkSpeed)
	//	{
	//		var currentPhase = currentTime % (double) blinkSpeed;

	//		Math.Abs(1f - (currentTime % blinkSpeed) / (blinkSpeed / 2f));

	//		var lastLevelWithNewSpeed = Math.Sin(lastTime % (double)blinkSpeed) / (double)blinkSpeed * Math.PI;
	//		var currentLevel = Math.Sin(currentTime % (double)blinkSpeed) / (double)blinkSpeed * Math.PI;
	//		var shift = lastLevel - lastLevelWithNewSpeed;


	//	}
	//}
}
