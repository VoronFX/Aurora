using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Xml.Serialization;
using Aurora.Profiles;
using Newtonsoft.Json;

namespace Aurora.Controls
{
	/// <summary>
	/// Interaction logic for PerformanceCountersDebug.xaml
	/// </summary>
	public partial class PerformanceCountersDebug : UserControl
	{
		public PerformanceCountersDebug()
		{
			InitializeComponent();
		}

		private void PerformanceCountersDebug_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			string s = "";
			try
			{
				s = "LocalPCInformation\r\n" + JsonConvert.SerializeObject(localPcInformation, Formatting.Indented);
			}
			catch (Exception exception)
			{
				s = exception.ToString();
			}
			s += "\r\nInitLog:\r\n" + Aurora.Profiles.PerformanceCounters.AuroraInternal.Gpu.InitLog;

			Dispatcher.BeginInvoke((Action)(() =>
			{
				PcInfoTextBox.Text = s;
				LogTextBox.Text = log.ToString();
				LogTextBox.ScrollToEnd();
			}));
		}

		private StringWriter stringWriter;
		private TextWriterTraceListener listener;
		private StringBuilder log;
		private System.Timers.Timer timer;
		private LocalPCInformation localPcInformation;

		private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
		{
			int newInterval;
			if (timer != null && int.TryParse(IntervalTextBox.Text, out newInterval))
			{
				timer.Interval = newInterval;
			}
			else
			{
				IntervalTextBox.Text = "1000";
			}
		}

		private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			if ((string)StartStopButton.Content == "Stop")
			{
				StartStopButton.Content = "Start";
				timer.Stop();
				localPcInformation = null;
				log = null;
				Debug.Listeners.Remove(listener);
				listener.Close();
				listener = null;
				stringWriter.Close();
				stringWriter = null;
			}
			else
			{
				StartStopButton.Content = "Stop";
				LogTextBox.Clear();
				int newInterval;
				timer = int.TryParse(IntervalTextBox.Text, out newInterval) ?
					new System.Timers.Timer(newInterval) : new System.Timers.Timer(1000);
				timer.Elapsed += PerformanceCountersDebug_Elapsed;
				log = new StringBuilder();
				stringWriter = new StringWriter(log);
				listener = new TextWriterTraceListener(stringWriter);
				Debug.Listeners.Add(listener);
				localPcInformation = new LocalPCInformation();
				timer.Start();

			}
		}
	}
}
