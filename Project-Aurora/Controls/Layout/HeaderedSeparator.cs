using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Aurora.Controls.Layout
{
	public class HeaderedSeparator : Control
	{
		public static DependencyProperty HeaderProperty =
			DependencyProperty.Register(
			"Header",
			typeof(string),
			typeof(HeaderedSeparator));

		public string Header
		{
			get { return (string)GetValue(HeaderProperty); }
			set { SetValue(HeaderProperty, value); }
		}
	}
}
