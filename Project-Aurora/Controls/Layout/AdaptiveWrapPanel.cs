using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Aurora.Controls.Layout
{
	[ContentProperty(nameof(Children))]
	class AdaptiveWrapPanel : ScrollViewer
	{
		public AdaptiveWrapPanel()
		{
			Content = new ColumnWrapPanel();
			Children = ((ColumnWrapPanel)Content).Children;
		}

		protected override Size MeasureOverride(Size constraint)
		{
			var content = Content as ColumnWrapPanel;
			if (content != null)
			{
				content.ParentScrollViewerConstraint = constraint;
				content.InvalidateMeasure();
			}
			return base.MeasureOverride(constraint);
		}

		public static readonly DependencyPropertyKey ChildrenProperty = DependencyProperty.RegisterReadOnly(
			nameof(Children),  // Prior to C# 6.0, replace nameof(Children) with "Children"
			typeof(UIElementCollection),
			typeof(AdaptiveWrapPanel),
			new PropertyMetadata());

		public UIElementCollection Children
		{
			get { return (UIElementCollection)GetValue(ChildrenProperty.DependencyProperty); }
			private set { SetValue(ChildrenProperty, value); }
		}

		//public MultiChildDemo()
		//{
		//	InitializeComponent();
		//	Children = PART_Host.Children;
		//}
	}
}
