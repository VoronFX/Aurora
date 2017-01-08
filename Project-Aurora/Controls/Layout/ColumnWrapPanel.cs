using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Mono.CSharp;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Aurora.Controls.Layout
{
	/// <summary>
	/// A column based layout panel, that automatically
	/// wraps to new column when required. The user
	/// may also create a new column before an element
	/// using the 
	/// </summary>
	public class ColumnWrapPanel : Panel
	{
		public Size? ParentScrollViewerConstraint { get; set; }

		#region Ctor
		static ColumnWrapPanel()
		{
			//tell DP sub system, this DP, will affect
			//Arrange and Measure phases
			ForceNewColumnProperty =
				DependencyProperty.RegisterAttached("ForceNewColumn",
				typeof(bool), typeof(ColumnWrapPanel), 
				new FrameworkPropertyMetadata
				{
					AffectsArrange = true,
					AffectsMeasure = true
				});

			FillColumnHeightProperty =
				DependencyProperty.RegisterAttached("FillColumnHeight",
				typeof(bool), typeof(ColumnWrapPanel),
				new FrameworkPropertyMetadata
				{
					AffectsArrange = true,
					AffectsMeasure = true
				});
		}
		#endregion

		#region DPs

		/// <summary>
		/// Can be used to create a new column with the ColumnedPanel
		/// just before an element
		/// </summary>
		public static DependencyProperty ForceNewColumnProperty;

		public static void SetForceNewColumn(UIElement element, Boolean value)
		{
			element.SetValue(ForceNewColumnProperty, value);
		}
		public static Boolean GetForceNewColumn(UIElement element)
		{
			return (bool)element.GetValue(ForceNewColumnProperty);
		}

		public static DependencyProperty FillColumnHeightProperty;

		public static void SetFillColumnHeight(UIElement element, Boolean value)
		{
			element.SetValue(FillColumnHeightProperty, value);
		}
		public static Boolean GetFillColumnHeight(UIElement element)
		{
			return (bool)element.GetValue(FillColumnHeightProperty);
		}
		#endregion

		private Rect[] rects = new Rect[0];

		private Size MeasureArrange(Size constraint, bool arrange)
		{
			UIElementCollection elements = base.InternalChildren;
			if (rects.Length < elements.Count)
				rects = new Rect[elements.Count];

			var visibleConstraint = ParentScrollViewerConstraint ?? constraint;
			var panelSize = new Size();

			for (int ignoreOverflow = 0; ignoreOverflow < elements.Count; ignoreOverflow++)
			{
				for (int ignoreFill = 0; ignoreFill < elements.Count; ignoreFill++)
				{
					int overflowIgnored = 0;
					int fillIgnored = 0;
					int firstInLine = 0;

					var currentColumnSize = new Size();
					panelSize = new Size();

					for (int i = 0; i < elements.Count; i++)
					{
						elements[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
						//visibleConstraint.Width - panelSize.Width,
						//visibleConstraint.Height));
						
						bool filled = GetFillColumnHeight(elements[i]);
						bool overflow = currentColumnSize.Height 
							+ elements[i].DesiredSize.Height > visibleConstraint.Height;

						//need to switch to another column
						if (GetForceNewColumn(elements[i]) || 
							(filled && fillIgnored >= ignoreFill) ||
						    (overflow && overflowIgnored >= ignoreOverflow))
						{
							for (int j = firstInLine; j < i; j++)
							{
								rects[j].Width = currentColumnSize.Width;
							}

							if (i > 0 && GetFillColumnHeight(elements[i - 1]))
							{
								rects[i - 1].Height = Math.Max(rects[i - 1].Height,
									visibleConstraint.Height - rects[i - 1].Y);
							}

							panelSize.Height = Math.Max(currentColumnSize.Height, panelSize.Height);
							panelSize.Width += currentColumnSize.Width;

							currentColumnSize = elements[i].DesiredSize;

							rects[i].Height = currentColumnSize.Height;
							if (filled && !overflow)
							{
								rects[i].Height = Math.Max(rects[i].Height, visibleConstraint.Height);
								currentColumnSize.Height = rects[i].Height;
							}

							rects[i].X = panelSize.Width;
							rects[i].Y = 0;

							//the element is higher then the constraint - 
							//give it a separate column 
							if (currentColumnSize.Height >= visibleConstraint.Height)
							{
								rects[i].Width = currentColumnSize.Width;
								
								panelSize.Height = Math.Max(currentColumnSize.Height, panelSize.Height);
								panelSize.Width += currentColumnSize.Width;

								currentColumnSize = new Size();
							}
							firstInLine = i;
						}
						else //continue to accumulate a column
						{
							if (filled && fillIgnored < ignoreFill)
							{
								fillIgnored++;
							}
							else if (overflow && overflowIgnored < ignoreOverflow)
							{
								overflowIgnored++;
							}

							rects[i].Height = elements[i].DesiredSize.Height;
							rects[i].X = panelSize.Width;
							rects[i].Y = currentColumnSize.Height;

							currentColumnSize.Height += elements[i].DesiredSize.Height;
							currentColumnSize.Width = Math.Max(elements[i].DesiredSize.Width, currentColumnSize.Width);
						}
					}

					var lastIndex = elements.Count - 1;
					if (lastIndex > 0 && GetFillColumnHeight(elements[lastIndex]))
					{
						rects[lastIndex].Height = Math.Max(rects[lastIndex].Height,
							visibleConstraint.Height - rects[lastIndex].Y);
					}

					if (arrange && firstInLine < elements.Count)
					{
						for (int j = firstInLine; j < elements.Count; j++)
						{
							rects[j].Width = currentColumnSize.Width;
						}
					}

					panelSize.Height = Math.Max(currentColumnSize.Height, panelSize.Height);
					panelSize.Width += currentColumnSize.Width;

					if (panelSize.Width <= visibleConstraint.Width)
					{
						break;
					}
				}
				if (panelSize.Width <= visibleConstraint.Width)
				{
					break;
				}
			}

			panelSize.Height = Math.Max(arrange ? constraint.Height : visibleConstraint.Height, panelSize.Height);
			panelSize.Width = Math.Max(arrange ? constraint.Width : visibleConstraint.Width, panelSize.Width);

			if (arrange)
			{
				for (var i = 0; i < elements.Count; i++)
				{
					double xOffset = 0;
					if (elements[i].DesiredSize.Width < rects[i].Width)
					{
						xOffset = ((rects[i].Width - elements[i].DesiredSize.Width) / 2);
					}

					elements[i].Arrange(new Rect(rects[i].X + xOffset, rects[i].Y, 
						elements[i].DesiredSize.Width, rects[i].Height));
				}
			}

			return panelSize;
		}

		#region Measure Override
		// From MSDN : When overridden in a derived class, measures the 
		// size in layout required for child elements and determines a
		// size for the FrameworkElement-derived class
		protected override Size MeasureOverride(Size constraint)
		{
			Debug.WriteLine($"Measure {constraint} {ParentScrollViewerConstraint}");
			return MeasureArrange(constraint, false);
		}
		#endregion

		#region Arrange Override
		//From MSDN : When overridden in a derived class, positions child
		//elements and determines a size for a FrameworkElement derived
		//class.
		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			Debug.WriteLine($"Arrange {arrangeBounds} {ParentScrollViewerConstraint}");
			return MeasureArrange(arrangeBounds, true);
		}
		#endregion

	}


}
