using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace OpenKh.Tools.Common.Wpf.Dialogs
{
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window, INotifyPropertyChanged
    {
        public class ViewModel
        {
            public string ToolName { get; set; }
            public string Version { get; set; }
            public string Website { get; set; }
            public string Author { get; set; }
            public string PoweredBy { get; set; }
            public string AuthorWebsite { get; set; }
        }

        public AboutDialog(Assembly assembly = null)
        {
            InitializeComponent();

            assembly = assembly ?? Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            DataContext = new ViewModel
            {
                ToolName = fvi.ProductName,
                Website = fvi.Comments,
                Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Author = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright,
                PoweredBy = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description,
                AuthorWebsite = "https://openkh.dev",
            };

            MouseDown += (o, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation()
            {
                From = 0.0,
                To = 1.0,
                EasingFunction = new CubicEase(),
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            });
        }
    }
}
