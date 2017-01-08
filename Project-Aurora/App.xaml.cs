using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aurora
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		public App() : base()
		{
			// Setup Quick Converter.
			// Add the System namespace so we can use primitive types (i.e. int, etc.).
			QuickConverter.EquationTokenizer.AddNamespace(typeof(object));
			// Add the System.Windows namespace so we can use Visibility.Collapsed, etc.
			QuickConverter.EquationTokenizer.AddNamespace(typeof(Visibility));
			QuickConverter.EquationTokenizer.AddNamespace(typeof(Dock));
		}
	}
}
