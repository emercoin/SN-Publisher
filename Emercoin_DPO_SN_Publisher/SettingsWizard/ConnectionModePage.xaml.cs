namespace EmercoinDPOSNP.SettingsWizard
{
    using System.Windows.Controls;
    using System.Windows.Input;
    using EmercoinDPOSNP.AppSettings;

    /// <summary>
    /// Interaction logic for ConnectionMode.xaml
    /// </summary>
    public partial class ConnectionModePage : Page
    {
        public ConnectionModePage()
        {
            this.InitializeComponent();
        }

        private void LifetimeText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Checks.PortNumberValid(e.Text);
        }
    }
}
