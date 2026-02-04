using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScavKRInstaller
{
    /// <summary>
    /// Interaction logic for Log.xaml
    /// </summary>
    public partial class Log : Window
    {
        Logger logInstance;
        public Log(Logger log)
        {
            this.logInstance=log;
            InitializeComponent();
            this.TextBoxLog.Text=log.GetWholeLog();
            logInstance.Logged+=this.LogInstance_Logged;
        }

        private void LogInstance_Logged()
        {
            this.TextBoxLog.Text = this.logInstance.GetWholeLog();
        }
    }
}
