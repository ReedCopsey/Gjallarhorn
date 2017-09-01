using System.Windows;
using ElmInspiredOne;

namespace WpfElmInspiredOneCSharp
{
    /// <inheritdoc />
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            Gjallarhorn.Wpf.Framework.RunApplication(() => new MainWindow(), Program.applicationCore);
        }
    }
}
