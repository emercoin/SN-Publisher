using EmercoinDPOSNP.AppSettings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EmercoinDPOSNP.SettingsWizard
{
    /// <summary>
    /// Interaction logic for SettingsWizardWindow.xaml
    /// </summary>
    public partial class SettingsWizardWindow : Window
    {
        private ConnectionModePage conModePage;
        private DefineSettingsPage defineSettingsPage;
        private LocalModePage localModePage;

        private Brush defaultColor = new SolidColorBrush(Colors.Black);
        private Brush errorColor = new SolidColorBrush(Colors.Red);
        private bool connectionChecked = false;
        private bool closeDisabled;

        public SettingsWizardWindow()
        {
            InitializeComponent();

            this.conModePage = new ConnectionModePage();
            this.conModePage.localWalletBtn.IsChecked = true;
            this.frame1.Content = this.conModePage;

            this.defineSettingsPage = new DefineSettingsPage();
            this.localModePage = new LocalModePage();
        }

        private void connectionModeLogic() 
        {
            conModePage = (ConnectionModePage)this.frame1.Content;
            if (conModePage.localWalletBtn.IsChecked.Value)
            {
                var localModePage = new LocalModePage();
                this.frame1.Content = localModePage;
                this.nextBtn.Content = "Finish";
            }
            else if (conModePage.RemoteWalletBtn.IsChecked.Value)
            {
                var settings = Settings.Instance;
                if (settings != null)
                {
                    this.defineSettingsPage.HostText.Text = settings.Host;
                    this.defineSettingsPage.PortNumberText.Text = settings.Port;
                    this.defineSettingsPage.UsernameText.Text = settings.Username;
                    this.defineSettingsPage.RpcPassword.Password = settings.Password;
                    this.defineSettingsPage.RootDPONameText.Text = settings.RootDPOName;
                    this.nextBtn.Content = "Finish";
                }

                this.frame1.Content = this.defineSettingsPage;
            }
        }

        private async Task defineSettingsLogic() 
        {
            // Check settings
            var wc = new WalletClient();
            this.OperationProgress.IsIndeterminate = true;
            try
            {
                this.connectionChecked = await wc.checkConnection(
                    this.defineSettingsPage.HostText.Text,
                    this.defineSettingsPage.PortNumberText.Text,
                    this.defineSettingsPage.UsernameText.Text,
                    this.defineSettingsPage.RpcPassword.Password,
                    this.defineSettingsPage.RootDPONameText.Text);

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
                this.StatusTextBlock.Text = "Connection failed";
                this.StatusTextBlock.Foreground = this.errorColor;
            }
            this.OperationProgress.IsIndeterminate = false;

            this.closeDisabled = false;
            if (this.connectionChecked)
            {
                SaveSettingsFromUI();
                this.DialogResult = true;
            }
            else
            {
                var mbResult = MessageBox.Show(this, "Settings error. Save settings anyway?", "Save settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (mbResult == MessageBoxResult.Yes)
                {
                    SaveSettingsFromUI();
                    this.DialogResult = true;
                }
            }
        }

        private async Task localModeLogic() 
        {
            this.OperationProgress.IsIndeterminate = true;
            // get the best wallet instance among installed
            var walletApps = WalletInstallInfo.GetInfo();
            var wi = walletApps.OrderBy(i => i.Version).ThenBy(i => i.Bitness).Last();
            var wc = new WalletClient();

            if (wi == null)
            {
                Process.Start("https://sourceforge.net/projects/emercoin/files/");
                throw new Exception("There'are no Emercoin Core applications installed on the local machine");
            }

            var walletConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "Emercoin" + "\\emercoin.conf";
            var confManager = new EmercoinConfigManager(walletConfigPath);
            var conf = confManager.ReadConfig();

            string host = Settings.Instance.Host;
            string port = Settings.Instance.Port;
            string username = Settings.Instance.Username;
            string password = Settings.Instance.Password;
            string rootPDOName = Settings.Instance.RootDPOName;

            // check emercoin config if correspondes to publisher settings
            bool configValid = conf.ValidateParameters(Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.Password);
            bool settingsValid = validateSettings(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.RootDPOName, Settings.Instance.Username, Settings.Instance.Password);

            if (!configValid || !settingsValid)
            {
                confManager.FixToActive(conf);
                confManager.WriteConfig(conf, walletConfigPath);

                Settings.Instance.RootDPOName = "myname";
                Settings.Instance.Host = "localhost";
                Settings.Instance.Port = conf.GetParameterValue(EmercoinConfig.portParam) ?? string.Empty;
                Settings.Instance.Username = conf.GetParameterValue(EmercoinConfig.userParam) ?? string.Empty;
                Settings.Instance.Password = conf.GetParameterValue(EmercoinConfig.passwordParam) ?? string.Empty;
                Settings.WriteSettings();

                host = Settings.Instance.Host;
                port = Settings.Instance.Port;
                username = Settings.Instance.Username;
                password = Settings.Instance.Password;
                rootPDOName = Settings.Instance.RootDPOName;
            }

            // restart wallet in order to apply new settings
            if (wi.IsExecuting())
            {
                // test connection
                try
                {
                    this.connectionChecked = await wc.checkConnection(host, port, username, password, rootPDOName);
                    Settings.Instance.Validated = this.connectionChecked;

                    this.StatusTextBlock.Text = "Connected successfully";
                    this.StatusTextBlock.Foreground = this.defaultColor;
                }
                catch (EmercoinWalletException ex)
                {
                    this.StatusTextBlock.Text = "Connection failed";
                    this.StatusTextBlock.Foreground = this.errorColor;
                }

                if (!this.connectionChecked)
                {
                    walletClose(wi);
                    
                    // wait and start wallet
                    Action<Task> startNewWallet = (t) =>
                    {
                        if (wi.IsExecuting())
                        {
                            throw new Exception("Emercoin wallet wasn't able to close in time");
                        }
                        else
                        {
                            Process.Start(wi.FilePath);
                        }
                    };

                    await Task.Delay(15000).ContinueWith(startNewWallet);
                    await Task.Delay(15000);
                }
            }
            else
            {
                var proc = await Task.Run(() => Process.Start(wi.FilePath));
                await Task.Delay(15000);
            }

            // test connection
            try
            {
                this.connectionChecked = await wc.checkConnection(host, port, username, password, rootPDOName);
                Settings.Instance.Validated = this.connectionChecked;

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
                this.StatusTextBlock.Text = "Connection failed";
                this.StatusTextBlock.Foreground = this.errorColor;
            }
        }

        private async void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                closeDisabled = true;
                this.connectionChecked = false;
                this.nextBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;

                // check type of the current page on which logic depends
                if (frame1.Content is ConnectionModePage)
                {
                    connectionModeLogic();
                    this.closeDisabled = false;
                }
                else if (frame1.Content is DefineSettingsPage)
                {
                    await defineSettingsLogic();
                }
                else if (frame1.Content is LocalModePage)
                {
                    await localModeLogic();
                    this.Activate();
                    this.OperationProgress.IsIndeterminate = false;
                    this.closeDisabled = false;
                    this.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                AppUtils.ShowException(ex, this);
            }
            finally 
            {
                this.OperationProgress.IsIndeterminate = false;
                closeDisabled = false;
                this.nextBtn.IsEnabled = true;
                this.cancelBtn.IsEnabled = true;
            }
        }

        private bool walletClose(WalletInstallInfo walletInfo) 
        {
            var proc = walletInfo.ActiveProcess();
            bool closed = false;

            // wallet is executing
            if (proc != null)
            {
                try
                {
                    closed = proc.CloseMainWindow();
                }
                catch { }
            }
            return closed;
        }

        private void SaveSettingsFromUI() 
        {
            if (this.defineSettingsPage == null) 
            {
                return;
            }

            Settings.Instance.Validated = this.connectionChecked;
            Settings.Instance.Host = this.defineSettingsPage.HostText.Text;
            Settings.Instance.Port = this.defineSettingsPage.PortNumberText.Text;
            Settings.Instance.Username = this.defineSettingsPage.UsernameText.Text;
            Settings.Instance.Password = this.defineSettingsPage.RpcPassword.Password;
            Settings.Instance.RootDPOName = this.defineSettingsPage.RootDPONameText.Text;
            Settings.WriteSettings();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public async Task<bool> checkConnectionWithUI(string host, string port, string username, string password, string rootDPOName) 
        {
            this.OperationProgress.IsIndeterminate = true;
            bool success = false;
            try 
            {
                var wallet = new EmercoinWallet(host, port, username, password);
                string balance = await Task.Run(() => wallet.GetBalance());
                wallet.LoadRootDPO(rootDPOName);
                success = true;

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
                this.StatusTextBlock.Text = "Connection failed";
                this.StatusTextBlock.Foreground = this.errorColor;
            }
            this.OperationProgress.IsIndeterminate = false;
            return success;
        }

        private bool validateSettingsWithUI(string host, string port, string rootDPOName, string username, string password)
        {
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");
            if (!validHostnameRegex.IsMatch(host) && !validIpAddressRegex.IsMatch(host))
            {
                this.StatusTextBlock.Text = "Host is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!portNumberTextAllowed(port))
            {
                this.defineSettingsPage.PortNumberText.Focus();
                this.StatusTextBlock.Text = "Port number is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootDPOName))
            {
                this.defineSettingsPage.RootDPONameText.Focus();
                this.StatusTextBlock.Text = "Root DPO name is not configured";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.StatusTextBlock.Text = string.Empty;
            return true;
        }

        private bool validateSettings(string host, string port, string rootDPOName, string username, string password)
        {
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");
            if (!validHostnameRegex.IsMatch(host) && !validIpAddressRegex.IsMatch(host))
            {
                return false;
            }

            if (!portNumberTextAllowed(port))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootDPOName))
            {
                return false;
            }

            return true;
        }

        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = this.closeDisabled;
        }
    }
}
