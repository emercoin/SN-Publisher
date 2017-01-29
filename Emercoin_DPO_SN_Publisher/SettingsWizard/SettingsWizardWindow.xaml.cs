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
        private DefineSettingsPage remoteSettingsPage;
        private LocalModePage localModePage;

        private Brush defaultColor = new SolidColorBrush(Colors.Black);
        private Brush errorColor = new SolidColorBrush(Colors.Red);
        private bool success = false;
        private bool closeDisabled;

        public SettingsWizardWindow()
        {
            InitializeComponent();

            this.success = false;
            this.conModePage = new ConnectionModePage();
            this.conModePage.localWalletBtn.IsChecked = true;
            this.frame1.Content = this.conModePage;

            this.remoteSettingsPage = new DefineSettingsPage();
            this.localModePage = new LocalModePage();
        }

        public bool Success 
        {
            get 
            {
                return this.success;
            }
        }

        private void connectionModeLogic() 
        {
            var settings = Settings.Instance;
            conModePage = (ConnectionModePage)this.frame1.Content;
            if (conModePage.localWalletBtn.IsChecked.Value)
            {
                this.frame1.Content = this.localModePage;
                this.nextBtn.Content = "Finish";

                this.localModePage.RootDPONameTextLocal.Text = settings.RootDPOName;
                this.localModePage.WalletPassphraseLocal.Password = settings.WalletPassphrase;
            }
            else if (conModePage.RemoteWalletBtn.IsChecked.Value)
            {
                if (settings != null)
                {
                    this.remoteSettingsPage.HostText.Text = settings.Host;
                    this.remoteSettingsPage.PortNumberText.Text = settings.Port;
                    this.remoteSettingsPage.UsernameText.Text = settings.Username;
                    this.remoteSettingsPage.RpcPassword.Password = settings.RpcPassword;
                    this.remoteSettingsPage.RootDPONameText.Text = settings.RootDPOName;
                    this.remoteSettingsPage.WalletPassphrase.Password = settings.WalletPassphrase;
                    this.nextBtn.Content = "Finish";
                }

                this.frame1.Content = this.remoteSettingsPage;
            }
        }

        private async Task remoteSettingsLogic() 
        {
            // Check settings
            bool connectionOk = false;
            var wc = new WalletClient();
            this.OperationProgress.IsIndeterminate = true;
            try
            {
                connectionOk = await wc.checkConnection(
                    this.remoteSettingsPage.HostText.Text,
                    this.remoteSettingsPage.PortNumberText.Text,
                    this.remoteSettingsPage.UsernameText.Text,
                    this.remoteSettingsPage.RpcPassword.Password,
                    this.remoteSettingsPage.RootDPONameText.Text);

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
                this.StatusTextBlock.Text = "Connection failed";
                this.StatusTextBlock.Foreground = this.errorColor;
            }

            bool walletLocked = true;
            bool pwdChecked = false;
            try
            {
                var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
                var walletInfo = await Task.Run(() => wallet.GetWalletInfo());

                walletLocked = ((walletInfo != null && walletInfo.locked) || !string.IsNullOrEmpty(this.remoteSettingsPage.WalletPassphrase.Password));
                if (walletLocked)
                {
                    pwdChecked = await wallet.CheckWalletPassphrase(this.remoteSettingsPage.WalletPassphrase.Password);
                }
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
            }

            if (walletLocked && !pwdChecked)
            {
                this.StatusTextBlock.Text = "Wallet passphrase check failed";
                this.StatusTextBlock.Foreground = this.errorColor;
            }

            this.OperationProgress.IsIndeterminate = false;

            this.closeDisabled = false;
            this.success = connectionOk && (!walletLocked || pwdChecked);

            if (this.success)
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
            var walletApp = walletApps.OrderBy(i => i.Version).ThenBy(i => i.Bitness).Last();

            if (walletApp == null)
            {
                Process.Start("https://sourceforge.net/projects/emercoin/files/");
                throw new Exception("There'are no Emercoin Core applications installed on the local machine");
            }

            var walletConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "Emercoin" + "\\emercoin.conf";
            var confManager = new EmercoinConfigManager(walletConfigPath);
            var conf = confManager.ReadConfig();

            // check emercoin config if correspondes to publisher settings
            bool configValid = conf.ValidateParameters(Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
            bool settingsValid = validateSettings(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.RootDPOName, Settings.Instance.Username, Settings.Instance.RpcPassword);
            bool dpoNameChanged = !string.Equals(this.localModePage.RootDPONameTextLocal.Text, Settings.Instance.RootDPOName, StringComparison.InvariantCultureIgnoreCase);

            if (!configValid || !settingsValid)
            {
                confManager.FixToActive(conf);
                confManager.WriteConfig(conf, walletConfigPath);

                Settings.Instance.Host = "localhost";
                Settings.Instance.Port = conf.GetParameterValue(EmercoinConfig.portParam) ?? string.Empty;
                Settings.Instance.Username = conf.GetParameterValue(EmercoinConfig.userParam) ?? string.Empty;
                Settings.Instance.RpcPassword = conf.GetParameterValue(EmercoinConfig.rpcPasswordParam) ?? string.Empty;
                Settings.Instance.RootDPOName = this.localModePage.RootDPONameTextLocal.Text;
                Settings.Instance.WalletPassphrase = this.localModePage.WalletPassphraseLocal.Password;
                Settings.WriteSettings();
            }
            else if (dpoNameChanged)
            {
                Settings.Instance.RootDPOName = this.localModePage.RootDPONameTextLocal.Text;
                Settings.WriteSettings();
            }

            // restart wallet in order to apply new settings
            bool connectionOk = false;
            if (walletApp.IsExecuting())
            {
                // test connection
                var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
                
                try
                {
                    connectionOk = await wallet.CheckConnection(this.localModePage.RootDPONameTextLocal.Text);
                }
                catch (EmercoinWalletException ex) {}

                if (!connectionOk)
                {
                    walletClose(walletApp);
                    
                    // wait and start wallet
                    Action<Task> startNewWallet = (t) =>
                    {
                        if (walletApp.IsExecuting())
                        {
                            throw new Exception("Emercoin wallet wasn't able to close in time");
                        }
                        else
                        {
                            Process.Start(walletApp.FilePath);
                        }
                    };

                    await Task.Delay(15000).ContinueWith(startNewWallet);
                    await Task.Delay(15000);
                }
            }
            else
            {
                var proc = await Task.Run(() => Process.Start(walletApp.FilePath));
                await Task.Delay(15000);
            }

            // test connection
            try
            {
                var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
                connectionOk = await wallet.CheckConnection(Settings.Instance.RootDPOName);

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                this.StatusTextBlock.Text = "Connection failed";
                this.StatusTextBlock.Foreground = this.errorColor;
                throw;
            }

            bool walletLocked = true;
            bool pwdChecked = false;
            try
            {
                var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
                var walletInfo = await Task.Run(() => wallet.GetWalletInfo());

                walletLocked = ((walletInfo != null && walletInfo.locked) || !string.IsNullOrEmpty(this.localModePage.WalletPassphraseLocal.Password));
                if (walletLocked) 
                {
                    pwdChecked = await wallet.CheckWalletPassphrase(this.localModePage.WalletPassphraseLocal.Password);

                    if (pwdChecked)
                    {
                        Settings.Instance.WalletPassphrase = this.localModePage.WalletPassphraseLocal.Password;
                        Settings.WriteSettings();
                    }
                    else
                    {
                        throw new EmercoinWalletException("Wallet passphrase check failed");
                    }
                }
            }
            catch (EmercoinWalletException ex) 
            {
                this.StatusTextBlock.Text = "Wallet passphrase check failed";
                this.StatusTextBlock.Foreground = this.errorColor;
                throw;
            }

            this.success = connectionOk && (!walletLocked || pwdChecked);
        }

        private async void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                Settings.ReadSettings();
                closeDisabled = true;
                this.success = false;
                this.nextBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;
                bool controlsValid = false;

                // check type of the current page on which logic depends
                if (frame1.Content is ConnectionModePage)
                {
                    connectionModeLogic();
                    this.closeDisabled = false;
                }
                else if (frame1.Content is DefineSettingsPage)
                {
                    controlsValid = this.validateRemoteSettingsPage();

                    if (controlsValid) 
                    {
                        await remoteSettingsLogic();
                    }
                }
                else if (frame1.Content is LocalModePage)
                {
                    controlsValid = validateLocalSettingsPage();

                    if (controlsValid) 
                    {
                        await localModeLogic();
                        this.Activate();
                        this.OperationProgress.IsIndeterminate = false;
                        this.closeDisabled = false;
                        this.DialogResult = true;
                    }
                }
            }
            catch (Exception ex)
            {
                AppUtils.ShowException(ex, this);
                this.DialogResult = false;
            }
            finally 
            {
                closeDisabled = false;
                this.OperationProgress.IsIndeterminate = false;
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
            if (this.remoteSettingsPage == null) 
            {
                return;
            }

            Settings.Instance.Host = this.remoteSettingsPage.HostText.Text;
            Settings.Instance.Port = this.remoteSettingsPage.PortNumberText.Text;
            Settings.Instance.Username = this.remoteSettingsPage.UsernameText.Text;
            Settings.Instance.RpcPassword = this.remoteSettingsPage.RpcPassword.Password;
            Settings.Instance.RootDPOName = this.remoteSettingsPage.RootDPONameText.Text;
            Settings.Instance.WalletPassphrase = this.remoteSettingsPage.WalletPassphrase.Password;
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

        private bool validateLocalSettingsPage()
        {
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");

            if (string.IsNullOrWhiteSpace(this.localModePage.RootDPONameTextLocal.Text))
            {
                this.localModePage.RootDPONameTextLocal.Focus();
                this.StatusTextBlock.Text = "Root DPO name is not configured";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.StatusTextBlock.Text = string.Empty;
            return true;
        }

        private bool validateRemoteSettingsPage()
        {
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");

            string host = this.remoteSettingsPage.HostText.Text;
            if (!validHostnameRegex.IsMatch(host) && !validIpAddressRegex.IsMatch(host))
            {
                this.remoteSettingsPage.HostText.Focus();
                this.StatusTextBlock.Text = "Host is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!portNumberTextAllowed(this.remoteSettingsPage.PortNumberText.Text))
            {
                this.remoteSettingsPage.PortNumberText.Focus();
                this.StatusTextBlock.Text = "Port number is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (string.IsNullOrWhiteSpace(this.remoteSettingsPage.RootDPONameText.Text))
            {
                this.remoteSettingsPage.RootDPONameText.Focus();
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
