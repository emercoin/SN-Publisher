using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace EmercoinDPOSNP.SettingsWizard
{
    /// <summary>
    /// Interaction logic for DefineSettingsPage.xaml
    /// </summary>
    public partial class DefineSettingsPage : Page
    {
        public DefineSettingsPage()
        {
            InitializeComponent();
        }

        private void PortNumberText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !portNumberTextAllowed(e.Text);
        }

        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }
    }
}
