namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using Microsoft.Win32;
    using Newtonsoft.Json.Linq;
    using EmercoinDPOSNP.SettingsWizard;
    using EmercoinDPOSNP.AppSettings;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Brush defaultColor = new SolidColorBrush(Colors.Black);
        private Brush errorColor = new SolidColorBrush(Colors.Red);

        private Settings settings;
        //private string host;
        //private string port;
        //private string username;
        //private string password;

        private EmercoinWallet wallet;

        private int lifetime;
        private CsvData csv;

        private CancellationTokenSource tokenSource;

        public MainWindow()
        {
            this.InitializeComponent();
            StatusTextBlock.Text = string.Empty;

            initialValidation();
        }

        private async void initialValidation()
        {
            //read settings and validate
            try 
            {
                Settings.ReadSettings();
                this.settings = Settings.Instance;
                var valid = await checkConnection();
                if (valid) 
                {
                    Settings.Instance.Validated = true;
                    StatusTextBlock.Text = "Settings OK";
                    StatusTextBlock.Foreground = this.defaultColor;
                }
            }
            catch 
            {
                StatusTextBlock.Text = "Check settings";
                StatusTextBlock.Foreground = this.errorColor;
            }
        }

        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }

        private static int getPercent(int rowNumber, int count)
        {
            return count > 0 ? (int)((double)rowNumber / count * 100) : 0;
        }

        private bool validateConnectionSettings()
        {
            if (settings == null) 
            {
                StatusTextBlock.Text = "Check settings";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");
            if (!validHostnameRegex.IsMatch(this.settings.Host) && !validIpAddressRegex.IsMatch(this.settings.Host))
            {
                StatusTextBlock.Text = "Host is invalid. Check settings";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!portNumberTextAllowed(this.settings.Port))
            {
                StatusTextBlock.Text = "Port number is invalid. Check settings";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (string.IsNullOrWhiteSpace(this.settings.RootDPOName))
            {
                this.StatusTextBlock.Text = "Root DPO name is not configured. Check settings";
                this.StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            StatusTextBlock.Text = string.Empty;
            return true;
        }

        private bool validateLifetime()
        {
            this.lifetime = 0;
            try {
                this.lifetime = int.Parse(LifetimeText.Text);
            }
            catch {
            }
            if (this.lifetime <= 0) {
                LifetimeText.Focus();
                StatusTextBlock.Text = "Lifetime is invalid";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }
            return true;
        }

        private async Task<bool> checkConnection()
        {
            if (!this.validateConnectionSettings()) {
                return false;
            }

            this.OperationProgress.IsIndeterminate = true;
            defineSettingsBtn.IsEnabled = false;

            bool success = false;
            try {
                this.wallet = new EmercoinWallet(this.settings.Host, this.settings.Port, this.settings.Username, this.settings.Password);
                string balance = await Task.Run(() => this.wallet.GetBalance());
                this.wallet.LoadRootDPO(this.settings.RootDPOName);
                BalanceLabel.Content = "Balance: " + balance + " EMC";
                StatusTextBlock.Text = "Connected successfully";
                StatusTextBlock.Foreground = this.defaultColor;
                success = true;
            }
            catch (EmercoinWalletException ex) {
                AppUtils.ShowException(ex, this);
                StatusTextBlock.Text = "Connection failed";
                StatusTextBlock.Foreground = this.errorColor;
            }

            this.OperationProgress.IsIndeterminate = false;
            defineSettingsBtn.IsEnabled = true;
            return success;
        }

        private ReservationStats reserveSerialNumbers(IProgress<int> progress, CancellationToken ct)
        {
            var stats = new ReservationStats();

            int i = 0;
            foreach (string[] row in this.csv.Rows) {
                string sn = row[0];
                string name = this.settings.RootDPOName + ":" + sn;

                // iterate through unique names
                for (int j = 0; j < 100; j++) {
                    string nameUnique = name + ":" + j.ToString(CultureInfo.InvariantCulture);
                    EmercoinWallet.NameCreationStatusEnum result = this.wallet.CreateOrCheckName(nameUnique, this.lifetime);
                    if (result == EmercoinWallet.NameCreationStatusEnum.Created) {
                        stats.NewNumbers++;
                        break;
                    }
                    else if (result == EmercoinWallet.NameCreationStatusEnum.Exists) {
                        stats.MyNumbers++;
                        break;
                    }
                }
                stats.Processed++;

                if (i > 0 && i % 10 == 0) {
                    Thread.Sleep(5000);
                }

                i++;
                if (progress != null) {
                    progress.Report(getPercent(i, this.csv.Rows.Count));
                }
                ct.ThrowIfCancellationRequested();
            }

            return stats;
        }

        private SigningStats signSerialNumbers(IProgress<int> progress, CancellationToken ct)
        {
            var stats = new SigningStats();

            var signedColumns = new HashSet<int>();
            for (int n = 0; n < this.csv.HeaderRow.Length; n++) {
                string col = this.csv.HeaderRow[n];
                if (col.StartsWith("F-")) {
                    signedColumns.Add(n);
                }
            }

            int i = 0;
            foreach (string[] row in this.csv.Rows) {
                string sn = row[0];
                string name = this.settings.RootDPOName + ":" + sn;

                for (int j = 0; j < 100; j++) {
                    string nameUnique = name + ":" + j.ToString(CultureInfo.InvariantCulture);
                    if (this.wallet.CheckNameIsMine(nameUnique)) {
                        var record = string.Empty;
                        var messageParts = new List<string>() { nameUnique };
                        for (int k = 1; k < this.csv.HeaderRow.Length; k++) {
                            string part = this.csv.HeaderRow[k] + "=" + row[k];
                            record = record + part + "\n";
                            if (signedColumns.Contains(k)) {
                                messageParts.Add(part);
                            }
                        }

                        string signedMessage = this.wallet.SignMessage(string.Join("|", messageParts));
                        record = record + "Signature=" + signedMessage;
                        this.wallet.UpdateName(nameUnique, record, 1);
                        stats.Signed++;
                        break;
                    }
                }
                stats.Processed++;

                if (i > 0 && i % 10 == 0) {
                    Thread.Sleep(5000);
                }

                i++;
                if (progress != null) {
                    progress.Report(getPercent(i, this.csv.Rows.Count));
                }
                ct.ThrowIfCancellationRequested();
            }

            return stats;
        }

        private async void ReserveBtn_Click(object sender, RoutedEventArgs e)
        {
            bool success = await this.checkConnection();
            if (!success || !this.validateLifetime()) {
                return;
            }

            this.tokenSource = new CancellationTokenSource();
            CancellationToken ct = this.tokenSource.Token;
            CancelBtn.IsEnabled = true;
            defineSettingsBtn.IsEnabled = false;

            // The Progress<T> constructor captures our UI context, so the lambda will be run on the UI thread.
            var progress = new Progress<int>(percent =>
            {
                OperationProgress.Value = percent;
                ProgressLabel.Content = percent + "%";
            });

            var stats = new ReservationStats() { Processed = 0, MyNumbers = 0, NewNumbers = 0 };
            try {
                // reserveSerialNumbers is run on the thread pool.
                stats = await Task.Run(() => this.reserveSerialNumbers(progress, ct));
                OperationProgress.Value = 100;
                StatusTextBlock.Text = "Done!";
                StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (OperationCanceledException) {
                StatusTextBlock.Text = "Stopped!";
                StatusTextBlock.Foreground = this.errorColor;
            }
            catch (EmercoinWalletException ex) {
                StatusTextBlock.Text = "Emercoin operation error";
                StatusTextBlock.Foreground = this.errorColor;
                AppUtils.ShowException(ex, this);
            }
            ProgressLabel.Content = string.Empty;

            defineSettingsBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            this.tokenSource.Dispose();

            MessageBox.Show(
                this,
                "Total serial numbers processed: " + stats.Processed + "\n"
                + "Records you owned before: " + stats.MyNumbers + "\n" 
                + "New records created: " + stats.NewNumbers, 
                AppUtils.AppName);
        }

        private async void FillSignBtn_Click(object sender, RoutedEventArgs e)
        {
            bool success = await this.checkConnection();
            if (!success || !this.validateLifetime()) {
                return;
            }

            this.tokenSource = new CancellationTokenSource();
            CancellationToken ct = this.tokenSource.Token;
            CancelBtn.IsEnabled = true;
            defineSettingsBtn.IsEnabled = false;

            // The Progress<T> constructor captures our UI context, so the lambda will be run on the UI thread.
            var progress = new Progress<int>(percent =>
            {
                OperationProgress.Value = percent;
                ProgressLabel.Content = percent + "%";
            });

            var stats = new SigningStats() { Processed = 0, Signed = 0 };
            try {
                // signSerialNumbers is run on the thread pool.
                stats = await Task.Run(() => this.signSerialNumbers(progress, ct));
                OperationProgress.Value = 100;
                StatusTextBlock.Text = "Done!";
                StatusTextBlock.Foreground = this.defaultColor;
            }
            catch (OperationCanceledException) {
                StatusTextBlock.Text = "Stopped!";
                StatusTextBlock.Foreground = this.errorColor;
            }
            catch (EmercoinWalletException ex) {
                StatusTextBlock.Text = "Emercoin operation error";
                StatusTextBlock.Foreground = this.errorColor;
                AppUtils.ShowException(ex, this);
            }
            ProgressLabel.Content = string.Empty;

            defineSettingsBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            this.tokenSource.Dispose();

            MessageBox.Show(
                this,
                "Total serial numbers processed: " + stats.Processed + "\n"
                + "Records signed: " + stats.Signed,
                AppUtils.AppName);
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            RowsNumberLabel.Content = "...";
            ReserveBtn.IsEnabled = false;
            FillSignBtn.IsEnabled = false;

            var dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.Filter = "CSV files|*.csv";
            dlg.AddExtension = true;
            dlg.DefaultExt = "csv";
            dlg.ValidateNames = true;

            if (dlg.ShowDialog() != null) {
                if (!string.IsNullOrEmpty(dlg.FileName)) {
                    try {
                        this.csv = new CsvData(dlg.FileName);
                        RowsNumberLabel.Content = this.csv.Rows.Count;
                        StatusTextBlock.Text = string.Empty;
                        ReserveBtn.IsEnabled = true;
                        FillSignBtn.IsEnabled = true;
                    }
                    catch (CsvData.EmptyException) {
                        StatusTextBlock.Text = "File is empty or header row missing";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (CsvData.InconsistentException) {
                        StatusTextBlock.Text = "Number of values in a data row does not correspond to the header row";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (CsvData.SerialColumnException) {
                        StatusTextBlock.Text = CsvData.SerialColumnName + " column header missing";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (CsvData.DuplicateSerialException ex) {
                        StatusTextBlock.Text = "Duplicate serial number in the file: " + ex.Message;
                        StatusTextBlock.Foreground = this.errorColor;
                    }

                    CsvFileText.Text = dlg.FileName;
                }
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.tokenSource.Cancel();
            CancelBtn.IsEnabled = false;
        }

        private void PortNumberText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !portNumberTextAllowed(e.Text);
        }

        private void LifetimeText_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !portNumberTextAllowed(e.Text);
        }

        private void AppWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CancelBtn.IsEnabled) {
                e.Cancel = true;
            }
        }

        private struct ReservationStats
        {
            public int Processed;
            public int MyNumbers;
            public int NewNumbers;
        }

        private struct SigningStats
        {
            public int Processed;
            public int Signed;
        }

        private void defineSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var wizardFrm = new SettingsWizardWindow();
            wizardFrm.Owner = this;
            wizardFrm.Top = this.Top + 20;
            wizardFrm.Left = this.Left + 20;

            wizardFrm.ShowDialog();
            if (Settings.Instance.Validated) 
            {
                StatusTextBlock.Text = "Settings OK";
                StatusTextBlock.Foreground = this.defaultColor;
            }
            else
            {
                StatusTextBlock.Text = "Settings are invalid";
                StatusTextBlock.Foreground = this.errorColor;
            }
        }
    }
}
