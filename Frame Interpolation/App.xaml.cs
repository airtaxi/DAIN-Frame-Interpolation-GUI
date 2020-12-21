using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Frame_Interpolation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            foreach (var process in Frame_Interpolation.MainWindow.childs)
            {
                if (process.HasExited != true) { process.Kill(); }
            }
        }
    }
}
