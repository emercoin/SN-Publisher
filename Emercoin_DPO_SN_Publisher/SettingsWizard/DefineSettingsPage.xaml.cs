namespace EmercoinDPOSNP.SettingsWizard
{
    using System.Text.RegularExpressions;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for DefineSettingsPage.xaml
    /// </summary>
    public partial class DefineSettingsPage : Page
    {
        public DefineSettingsPage()
        {
            this.InitializeComponent();
        }

        private void PortNumberText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !AppSettings.Checks.PortNumberValid(e.Text);
        }
    }
}
