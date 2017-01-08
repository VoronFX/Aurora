using System;
using System.Collections.Generic;
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

namespace Aurora.Controls.Layout
{
	/// <summary>
	/// Interaction logic for ColumnProperty3.xaml
	/// </summary>
	public partial class ColumnProperty3 : UserControl
	{
		public ColumnProperty3()
		{
			InitializeComponent();
			MyProperty = ContentPresenter2;
		}

		public new object Content
		{
			get { return ContentPresenter.Content; }
			set { ContentPresenter.Content = value; }
		}

		public ContentPresenter ContentPresenter2 => ContentPresenter;



		public ContentPresenter MyProperty
		{
			get { return (ContentPresenter)GetValue(MyPropertyProperty); }
			set { SetValue(MyPropertyProperty, value); }
		}

		// Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MyPropertyProperty =
			DependencyProperty.Register("MyProperty", typeof(ContentPresenter), typeof(ColumnProperty3), new PropertyMetadata(null));



	}
}
