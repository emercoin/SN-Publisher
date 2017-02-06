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

        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }

        private void PortNumberText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !portNumberTextAllowed(e.Text);
        }
    }
}
