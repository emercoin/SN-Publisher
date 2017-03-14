namespace EmercoinDPOSNP.SettingsWizard
{
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
    using EmercoinDPOSNP.AppSettings;

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
        private bool success;
        private bool closeDisabled;

        public SettingsWizardWindow()
        {
            this.InitializeComponent();

            this.StatusTextBlock.Text = string.Empty;
            this.success = false;
            this.conModePage = new ConnectionModePage();
            this.conModePage.localWalletBtn.IsChecked = true;
            this.frame1.Content = this.conModePage;

            Settings.ReadSettings();
            this.conModePage.RootDPONameText.Text = Settings.Instance.RootDPOName;
            this.conModePage.LifetimeText.Text = Settings.Instance.DpoLifetime.ToString();
            this.conModePage.WalletPassphrase.Password = Settings.Instance.WalletPassphrase;

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
            if (!this.validateConnectionModePage()) {
                return;
            }

            var settings = Settings.Instance;
            this.conModePage = (ConnectionModePage)this.frame1.Content;

            settings.RootDPOName = this.conModePage.RootDPONameText.Text;
            settings.DpoLifetime = int.Parse(this.conModePage.LifetimeText.Text);
            settings.WalletPassphrase = this.conModePage.WalletPassphrase.Password;
            
            if (this.conModePage.localWalletBtn.IsChecked.Value)
            {
                this.frame1.Content = this.localModePage;
                this.nextBtn.Content = "Finish";
            }
            else if (this.conModePage.RemoteWalletBtn.IsChecked.Value)
            {
                if (settings != null)
                {
                    this.remoteSettingsPage.HostText.Text = settings.Host;
                    this.remoteSettingsPage.PortNumberText.Text = settings.Port;
                    this.remoteSettingsPage.UsernameText.Text = settings.Username;
                    this.remoteSettingsPage.RpcPassword.Password = settings.RpcPassword;
                    this.nextBtn.Content = "Finish";
                }

                this.frame1.Content = this.remoteSettingsPage;
            }
        }

        private async Task remoteSettingsLogic() 
        {
            this.OperationProgress.IsIndeterminate = true;

            // Check settings
            bool connectionOk = false;
            try
            {
                connectionOk = await EmercoinWallet.CheckConnection(
                    this.remoteSettingsPage.HostText.Text,
                    this.remoteSettingsPage.PortNumberText.Text,
                    this.remoteSettingsPage.UsernameText.Text,
                    this.remoteSettingsPage.RpcPassword.Password,
                    Settings.Instance.RootDPOName);

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex)
            {
                this.StatusTextBlock.Text = ex.Message;
                this.StatusTextBlock.Foreground = this.errorColor;
            }

            bool walletLocked = true;
            bool pwdChecked = false;
            if (connectionOk) {
                try {
                    var wallet = new EmercoinWallet(
                        this.remoteSettingsPage.HostText.Text, 
                        this.remoteSettingsPage.PortNumberText.Text, 
                        this.remoteSettingsPage.UsernameText.Text,
                        this.remoteSettingsPage.RpcPassword.Password);
                    var walletInfo = await Task.Run(() => wallet.GetWalletInfo());

                    walletLocked = (walletInfo != null && walletInfo.locked) || !string.IsNullOrEmpty(Settings.Instance.WalletPassphrase);
                    if (walletLocked) {
                        pwdChecked = await wallet.CheckWalletPassphrase(Settings.Instance.WalletPassphrase);
                    }
                }
                catch (EmercoinWalletException ex) {
                }

                if (walletLocked && !pwdChecked) {
                    this.StatusTextBlock.Text = "Wallet passphrase check failed";
                    this.StatusTextBlock.Foreground = this.errorColor;
                }
            }

            this.OperationProgress.IsIndeterminate = false;

            this.closeDisabled = false;
            this.success = connectionOk && (!walletLocked || pwdChecked);

            if (this.success)
            {
                this.saveSettingsFromUI();
                this.DialogResult = true;
            }
            else
            {
                var promptResult = MessageBox.Show(this, "Configuration check error. Save settings anyway?", "Save settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (promptResult == MessageBoxResult.Yes)
                {
                    this.saveSettingsFromUI();
                    this.DialogResult = true;
                }
            }
        }

        private async Task localModeLogic()
        {
            // get the best wallet instance among installed
            var walletApps = WalletInstallInfo.GetInfo();
            var walletApp = walletApps.Count() > 0 ? walletApps.OrderBy(i => i.Version).ThenBy(i => i.Bitness).Last() : null;

            if (walletApp == null) {
                Process.Start("https://sourceforge.net/projects/emercoin/files/");
                throw new SettingsWizardException("No Emercoin Core applications installed on this computer");
            }

            var walletConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "Emercoin" + "\\emercoin.conf";
            var confManager = new EmercoinConfigManager(walletConfigPath);
            var conf = confManager.ReadConfig();

            // check emercoin config if corresponds to publisher settings
            if (!conf.ValidateParameters(Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword)) {
                confManager.FixToActive(conf);
                confManager.WriteConfig(conf, walletConfigPath);

                Settings.Instance.Host = "localhost";
                Settings.Instance.Port = conf.GetParameterValue(EmercoinConfig.portParam) ?? string.Empty;
                Settings.Instance.Username = conf.GetParameterValue(EmercoinConfig.userParam) ?? string.Empty;
                Settings.Instance.RpcPassword = conf.GetParameterValue(EmercoinConfig.rpcPasswordParam) ?? string.Empty;
                Settings.WriteSettings();
            }

            // restart wallet in order to apply new settings
            bool connectionOk = false;
            if (walletApp.IsExecuting()) {
                // test connection
                var testWallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);

                try {
                    connectionOk = await testWallet.CheckConnection(Settings.Instance.RootDPOName);
                }
                catch (EmercoinWalletException ex) {
                }

                if (!connectionOk) {
                    this.walletClose(walletApp);

                    // wait and start wallet
                    Action<Task> startNewWallet = (t) =>
                    {
                        if (walletApp.IsExecuting()) {
                            throw new SettingsWizardException("Emercoin wallet wasn't able to close in time");
                        }
                        else {
                            Process.Start(walletApp.FilePath);
                        }
                    };

                    await Task.Delay(15000).ContinueWith(startNewWallet);
                    await Task.Delay(15000);
                }
            }
            else {
                var proc = await Task.Run(() => Process.Start(walletApp.FilePath));
                await Task.Delay(15000);
            }

            // test wallet connection again
            var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
            try {
                connectionOk = await wallet.CheckConnection(Settings.Instance.RootDPOName);

                this.StatusTextBlock.Text = "Connected successfully";
                this.StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (EmercoinWalletException ex) {
                throw new SettingsWizardException("Connection to the local wallet failed");
            }

            bool walletLocked = true;
            bool pwdChecked = false;
            var walletInfo = await Task.Run(() => wallet.GetWalletInfo());

            walletLocked = (walletInfo != null && walletInfo.locked) || !string.IsNullOrEmpty(Settings.Instance.WalletPassphrase);
            if (walletLocked) {
                pwdChecked = await wallet.CheckWalletPassphrase(Settings.Instance.WalletPassphrase);
                if (!pwdChecked) {
                    throw new SettingsWizardException("Wallet passphrase check failed");
                }
            }

            this.closeDisabled = false;
            this.success = connectionOk && (!walletLocked || pwdChecked);
        }

        private async void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                this.closeDisabled = true;
                this.success = false;
                this.nextBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;

                // check type of the current page on which logic depends
                if (frame1.Content is ConnectionModePage)
                {
                    this.connectionModeLogic();
                    this.closeDisabled = false;
                }
                else if (frame1.Content is DefineSettingsPage)
                {
                    if (this.validateRemoteSettingsPage()) 
                    {
                        await this.remoteSettingsLogic();
                    }
                }
                else if (frame1.Content is LocalModePage)
                {
                    this.OperationProgress.IsIndeterminate = true;
                    await this.localModeLogic();
                    this.Activate();
                    this.DialogResult = true;
                }
            }
            catch (SettingsWizardException ex)
            {
                this.StatusTextBlock.Text = ex.Message;
                this.StatusTextBlock.Foreground = this.errorColor;
                this.DialogResult = false;
            }
            catch (Exception ex)
            {
                AppUtils.ShowException(ex, this);
                this.DialogResult = false;
            }
            finally 
            {
                this.closeDisabled = false;
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
                    closed = closed && proc.WaitForExit(15000);
                    if (!proc.HasExited) {
                        proc.Kill();
                        proc.WaitForExit(15000);
                    }
                }
                catch { 
                }
            }
            return closed;
        }

        private void saveSettingsFromUI() 
        {
            if (this.remoteSettingsPage == null) 
            {
                return;
            }

            Settings.Instance.Host = this.remoteSettingsPage.HostText.Text;
            Settings.Instance.Port = this.remoteSettingsPage.PortNumberText.Text;
            Settings.Instance.Username = this.remoteSettingsPage.UsernameText.Text;
            Settings.Instance.RpcPassword = this.remoteSettingsPage.RpcPassword.Password;
            Settings.WriteSettings();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private bool validateConnectionModePage()
        {
            if (string.IsNullOrWhiteSpace(this.conModePage.RootDPONameText.Text))
            {
                this.conModePage.RootDPONameText.Focus();
                this.StatusTextBlock.Text = "Root DPO name is not configured";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            int lifetime = 0;
            try {
                lifetime = int.Parse(this.conModePage.LifetimeText.Text);
            }
            catch {
            }
            if (lifetime <= 0) {
                this.conModePage.LifetimeText.Focus();
                this.StatusTextBlock.Text = "Lifetime is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.StatusTextBlock.Text = string.Empty;
            return true;
        }

        private bool validateRemoteSettingsPage()
        {
            string host = this.remoteSettingsPage.HostText.Text;
            if (!Checks.HostNameValid(host) && !Checks.IpAddressValid(host))
            {
                this.remoteSettingsPage.HostText.Focus();
                this.StatusTextBlock.Text = "Host is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!Checks.PortNumberValid(this.remoteSettingsPage.PortNumberText.Text))
            {
                this.remoteSettingsPage.PortNumberText.Focus();
                this.StatusTextBlock.Text = "Port number is invalid";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.StatusTextBlock.Text = string.Empty;
            return true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = this.closeDisabled;
        }

        private class SettingsWizardException : Exception
        {
            public SettingsWizardException(string msg)
                : base(msg)
            {
            }
        }
    }
}
