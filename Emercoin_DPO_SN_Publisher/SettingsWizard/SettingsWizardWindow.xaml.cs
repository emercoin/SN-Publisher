using EmercoinDPOSNP.AppSettings;
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

        private Brush defaultColor = new SolidColorBrush(Colors.Black);
        private Brush errorColor = new SolidColorBrush(Colors.Red);
        private bool connectionChecked = false;

        public SettingsWizardWindow()
        {
            InitializeComponent();

            this.conModePage = new ConnectionModePage();
            this.conModePage.RemoteWalletBtn.IsChecked = true;
            this.frame1.Content = this.conModePage;

            this.defineSettingsPage = new DefineSettingsPage();
        }

        private async void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            // проверяем на какой странице сейчас находимся (тип страницы)
            // анализируем выбор на странице
            // принимаем решение какое действие предпринять (вызов метода, переход к странице)

            // Окно выбора режима подключения
            if (frame1.Content is ConnectionModePage) 
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
            // Окно записи настроек
            else if (frame1.Content is DefineSettingsPage) 
            {
                // Check settings
                this.connectionChecked = await checkConnection();

                if (this.connectionChecked) 
                {
                    SaveSettings();
                    this.DialogResult = true;
                }
                else 
                {
                    var mbResult = MessageBox.Show(this, "Settings error. Save settings anyway?", "Save settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (mbResult == MessageBoxResult.Yes) 
                    {
                        SaveSettings();
                        this.DialogResult = true;
                    }
                }
            }
            else if (frame1.Content is LocalModePage) 
            {
                // проверяем установлено ли app
                var wi = WalletInstallInfo.GetInfo();
                if (wi != null)
                {
                    // TODO: Устанавливается только под пользователя а не на всю машину?
                    var walletPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "Emercoin" + "\\emercoin.conf";
                    var confManager = new EmercoinConfigManager(walletPath);
                    var conf = confManager.ReadConfig();

                    //if (conf.GetParameterValue(EmercoinConfig.serverParam) != "1")
                    //{
                    confManager.FixToActive(conf);

                    Settings.Instance.RootDPOName = "myname";
                    Settings.Instance.Host = "localhost";
                    Settings.Instance.Port = conf.GetParameterValue(EmercoinConfig.portParam) ?? string.Empty;
                    Settings.Instance.Username = conf.GetParameterValue(EmercoinConfig.userParam) ?? string.Empty;
                    Settings.Instance.Password = conf.GetParameterValue(EmercoinConfig.passwordParam) ?? string.Empty;
                    Settings.WriteSettings();

                    confManager.WriteConfig(conf, walletPath);

                    //}
                }
                else 
                {
                    throw new Exception("There'are no Emercoin Core instances installed on the local machine");
                }

                // Check settings
                this.connectionChecked = await checkConnection();
                Settings.Instance.Validated = this.connectionChecked;
                this.DialogResult = true;
            }
        }

        private void SaveSettings() 
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


        // TODO: наверное стоит выделить код ниже в отдельный объект
        private string rootDPOName;
        private string host;
        private string port;
        private string username;
        private string password;

        private EmercoinWallet wallet;

        private async Task<bool> checkConnection()
        {
            if (!this.validateConnectionSettings())
            {
                return false;
            }

            this.defineSettingsPage.OperationProgress.IsIndeterminate = true;

            bool success = false;
            try
            {
                this.wallet = new EmercoinWallet(this.host, this.port, this.username, this.password);
                string balance = await Task.Run(() => this.wallet.GetBalance());
                this.wallet.LoadRootDPO(this.rootDPOName);
                this.defineSettingsPage.StatusTextBlock.Text = "Connected successfully";
                this.defineSettingsPage.StatusTextBlock.Foreground = this.defaultColor;
                success = true;
            }
            catch (EmercoinWalletException ex)
            {
                AppUtils.ShowException(ex, this);
                this.defineSettingsPage.StatusTextBlock.Text = "Connection failed";
                this.defineSettingsPage.StatusTextBlock.Foreground = this.errorColor;
            }

            this.defineSettingsPage.OperationProgress.IsIndeterminate = false;
            return success;
        }

        private bool validateConnectionSettings()
        {
            this.host = this.defineSettingsPage.HostText.Text;
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");
            if (!validHostnameRegex.IsMatch(this.host) && !validIpAddressRegex.IsMatch(this.host))
            {
                this.defineSettingsPage.StatusTextBlock.Text = "Host is invalid";
                this.defineSettingsPage.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.port = this.defineSettingsPage.PortNumberText.Text;
            if (!portNumberTextAllowed(this.port))
            {
                this.defineSettingsPage.PortNumberText.Focus();
                this.defineSettingsPage.StatusTextBlock.Text = "Port number is invalid";
                this.defineSettingsPage.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.rootDPOName = this.defineSettingsPage.RootDPONameText.Text;
            if (string.IsNullOrWhiteSpace(this.rootDPOName))
            {
                this.defineSettingsPage.RootDPONameText.Focus();
                this.defineSettingsPage.StatusTextBlock.Text = "Root DPO name is not configured";
                this.defineSettingsPage.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            this.username = this.defineSettingsPage.UsernameText.Text;
            this.password = this.defineSettingsPage.RpcPassword.Password;
            this.defineSettingsPage.StatusTextBlock.Text = string.Empty;
            return true;
        }

        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }
    }
}
