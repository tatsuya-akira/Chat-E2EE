using System.Configuration;
using System.Data;
using System.Windows;
using DotNetEnv;

namespace Hermes
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Env.TraversePath().Load();
        }
    }
}
