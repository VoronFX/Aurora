using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Aurora.Controls.Layout
{
	public class ColumnProperty : ContentControl
	{

		public string PropertyHeader
		{
			get { return (string)GetValue(PropertyHeaderProperty); }
			set { SetValue(PropertyHeaderProperty, value); }
		}

		public static readonly DependencyProperty PropertyHeaderProperty =
			DependencyProperty<ColumnProperty>.Register(x => x.PropertyHeader);

		public string AfterText
		{
			get { return (string) GetValue(AfterTextProperty); }
			set { SetValue(AfterTextProperty, value); }
		}

		public static readonly DependencyProperty AfterTextProperty =
			DependencyProperty<ColumnProperty>.Register(x => x.AfterText);


		public bool DualLine
		{
			get { return (bool) GetValue(DualLineProperty); }
			set { SetValue(DualLineProperty, value); }
		}

		public static readonly DependencyProperty DualLineProperty =
			DependencyProperty<ColumnProperty>.Register(x => x.DualLine);

	}
}
