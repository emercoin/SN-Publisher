namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using EmercoinDPOSNP.AppSettings;
    using EmercoinDPOSNP.SettingsWizard;
    using Microsoft.Win32;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Brush defaultColor = new SolidColorBrush(Colors.Black);
        private Brush errorColor = new SolidColorBrush(Colors.Red);

        private Settings settings;

        private EmercoinWallet wallet;

        private SnData snData;

        private CancellationTokenSource tokenSource;

        public MainWindow()
        {
            this.InitializeComponent();
            StatusTextBlock.Text = string.Empty;
        }

        private static int getPercent(int rowNumber, int count)
        {
            return count > 0 ? (int)((double)rowNumber / count * 100) : 0;
        }

        private async Task initialValidation()
        {
            // read settings and validate
            try 
            {
                Settings.ReadSettings();
                this.settings = Settings.Instance;
                var connectionOk = await this.checkConnection();
                if (connectionOk) 
                {
                    StatusTextBlock.Text = "Settings OK";
                    StatusTextBlock.Foreground = this.defaultColor;
                }
                else 
                {
                    return;
                }

                // unlock wallet if needed
                bool walletLocked = false;
                try
                {
                    var wallet = new EmercoinWallet(Settings.Instance.Host, Settings.Instance.Port, Settings.Instance.Username, Settings.Instance.RpcPassword);
                    var walletInfo = await Task.Run(() => wallet.GetWalletInfo());

                    walletLocked = walletInfo != null && walletInfo.locked;
                }
                catch (EmercoinWalletException ex)
                {
                    StatusTextBlock.Text = "Error while checking lock";
                    StatusTextBlock.Foreground = this.errorColor;
                    AppUtils.ShowException(ex, this);
                }

                try
                {
                    if (walletLocked)
                    {
                        await Task.Run(() => this.wallet.UnlockWallet(Settings.Instance.WalletPassphrase, 100000));
                    }
                }
                catch (EmercoinWalletException ex)
                {
                    StatusTextBlock.Text = "Could not unlock the configured wallet";
                    StatusTextBlock.Foreground = this.errorColor;
                    AppUtils.ShowException(ex, this);
                }
            }
            catch 
            {
                StatusTextBlock.Text = "Check settings";
                StatusTextBlock.Foreground = this.errorColor;
            }
            finally 
            {
                this.Activate();
            }
        }

        private async Task startWalletIfLocal() 
        {
            var walletApps = WalletInstallInfo.GetInfo();
            if (walletApps.Count() > 0) {
                var wi = walletApps.OrderBy(i => i.Version).ThenBy(i => i.Bitness).Last();

                if (Settings.Instance.HostIsLocal()) {
                    if (wi != null && !wi.IsExecuting()) {
                        Process.Start(wi.FilePath);
                        await Task.Delay(15000);
                    }
                }
            }
        }

        private bool validateConnectionSettings()
        {
            if (this.settings == null) 
            {
                StatusTextBlock.Text = "Check settings";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!Checks.HostNameValid(this.settings.Host) && !Checks.IpAddressValid(this.settings.Host))
            {
                StatusTextBlock.Text = "Host is invalid. Check settings";
                StatusTextBlock.Foreground = this.errorColor;
                return false;
            }

            if (!Checks.PortNumberValid(this.settings.Port))
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

        private async Task<bool> checkConnection()
        {
            if (!this.validateConnectionSettings()) {
                return false;
            }

            OperationProgress.IsIndeterminate = true;
            SettingsGrid.IsEnabled = false;
            OperationsGrid.IsEnabled = false;

            bool success = false;
            try {
                this.wallet = new EmercoinWallet(this.settings.Host, this.settings.Port, this.settings.Username, this.settings.RpcPassword);
                string balance = await Task.Run(() => this.wallet.GetBalance());
                this.wallet.LoadRootDPO(this.settings.RootDPOName);
                
                BalanceLabel.Content = "Balance: " + balance + " EMC";
                StatusTextBlock.Text = "Connected to the wallet successfully";
                StatusTextBlock.Foreground = this.defaultColor;
                success = true;
            }
            catch (EmercoinWalletException ex) {
                StatusTextBlock.Text = ex.Message;
                StatusTextBlock.Foreground = this.errorColor;
            }

            OperationProgress.IsIndeterminate = false;
            SettingsGrid.IsEnabled = true;
            OperationsGrid.IsEnabled = true;
            return success;
        }

        private ReservationStats reserveSerialNumbers(IProgress<int> progress, CancellationToken ct)
        {
            var stats = new ReservationStats();

            int i = 0;
            foreach (string[] row in this.snData.Rows) {
                string sn = row[0];
                string name = this.settings.RootDPOName + ":" + sn;

                // iterate through unique names
                for (int j = 0; j < 100; j++) {
                    string nameUnique = name + ":" + j.ToString(CultureInfo.InvariantCulture);
                    EmercoinWallet.NameCreationStatusEnum result = this.wallet.CreateOrCheckName(nameUnique, this.settings.DpoLifetime);
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
                    progress.Report(getPercent(i, this.snData.Rows.Count));
                }
                ct.ThrowIfCancellationRequested();
            }

            return stats;
        }

        private SigningStats signSerialNumbers(IProgress<int> progress, CancellationToken ct)
        {
            var stats = new SigningStats();

            var signedColumns = new HashSet<int>();
            for (int n = 0; n < this.snData.HeaderRow.Length; n++) {
                string col = this.snData.HeaderRow[n];
                if (col.StartsWith("F-")) {
                    signedColumns.Add(n);
                }
            }

            int i = 0;
            foreach (string[] row in this.snData.Rows) {
                string sn = row[0];
                string name = this.settings.RootDPOName + ":" + sn;

                for (int j = 0; j < 100; j++) {
                    string nameUnique = name + ":" + j.ToString(CultureInfo.InvariantCulture);
                    if (this.wallet.CheckNameIsMine(nameUnique)) {
                        var record = string.Empty;
                        var messageParts = new List<string>() { nameUnique };
                        for (int k = 1; k < this.snData.HeaderRow.Length; k++) {
                            string part = this.snData.HeaderRow[k] + "=" + row[k];
                            record = record + part + "\n";
                            if (signedColumns.Contains(k)) {
                                messageParts.Add(part);
                            }
                        }

                        string signedMessage = this.wallet.SignMessage(string.Join("|", messageParts));
                        record = record + "Signature=" + signedMessage;
                        this.wallet.UpdateName(nameUnique, record, 1, this.settings.OwnerAddress);
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
                    progress.Report(getPercent(i, this.snData.Rows.Count));
                }
                ct.ThrowIfCancellationRequested();
            }

            return stats;
        }

        private async void ReserveBtn_Click(object sender, RoutedEventArgs e)
        {
            bool success = await this.checkConnection();
            if (!success) {
                return;
            }

            this.tokenSource = new CancellationTokenSource();
            CancellationToken ct = this.tokenSource.Token;
            CancelBtn.IsEnabled = true;
            SettingsGrid.IsEnabled = false;
            OperationsGrid.IsEnabled = false;

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

            SettingsGrid.IsEnabled = true;
            OperationsGrid.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            this.tokenSource.Dispose();

            MessageBox.Show(
                this,
                "Total serial numbers processed: " + stats.Processed + "\n" + "Records you owned before: " + stats.MyNumbers + "\n" + "New records created: " + stats.NewNumbers, 
                AppUtils.AppName);
        }

        private async void FillSignBtn_Click(object sender, RoutedEventArgs e)
        {
            bool success = await this.checkConnection();
            if (!success) {
                return;
            }

            this.tokenSource = new CancellationTokenSource();
            CancellationToken ct = this.tokenSource.Token;
            CancelBtn.IsEnabled = true;
            SettingsGrid.IsEnabled = false;
            OperationsGrid.IsEnabled = false;

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

            SettingsGrid.IsEnabled = true;
            OperationsGrid.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            this.tokenSource.Dispose();

            MessageBox.Show(
                this,
                "Total serial numbers processed: " + stats.Processed + "\n" + "Records signed: " + stats.Signed,
                AppUtils.AppName);
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            RowsNumberLabel.Content = "...";
            ReserveBtn.IsEnabled = false;
            FillSignBtn.IsEnabled = false;

            var dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.Filter = "Excel files|*.xlsx|CSV files|*.csv";
            dlg.AddExtension = true;
            dlg.DefaultExt = "xlsx";
            dlg.ValidateNames = true;

            if (dlg.ShowDialog() != null) {
                if (!string.IsNullOrEmpty(dlg.FileName)) {
                    try {
                        if (dlg.FileName.EndsWith(".xlsx")) {
                            this.snData = SnData.LoadFromXlsx(dlg.FileName);
                        }
                        else {
                            this.snData = SnData.LoadFromCsv(dlg.FileName);
                        }
                        RowsNumberLabel.Content = this.snData.Rows.Count;
                        StatusTextBlock.Text = string.Empty;
                        ReserveBtn.IsEnabled = true;
                        FillSignBtn.IsEnabled = true;
                    }
                    catch (SnData.EmptyException) {
                        StatusTextBlock.Text = "File is empty or header row missing";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (SnData.InconsistentException) {
                        StatusTextBlock.Text = "Number of values in a data row does not correspond to the header row";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (SnData.SerialColumnException) {
                        StatusTextBlock.Text = SnData.SerialColumnName + " column header missing";
                        StatusTextBlock.Foreground = this.errorColor;
                    }
                    catch (SnData.DuplicateSerialException ex) {
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

        private void AppWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CancelBtn.IsEnabled) {
                e.Cancel = true;
            }
        }

        private void defineSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var wizardFrm = new SettingsWizardWindow();
            wizardFrm.Owner = this;
            wizardFrm.Top = this.Top + 20;
            wizardFrm.Left = this.Left + 20;

            bool? dlgResult = null;
            try { 
                dlgResult = wizardFrm.ShowDialog(); 
            }
            catch (Exception ex)
            {
                AppUtils.ShowException(ex, this);
            }

            // if dialog not canceled
            if (dlgResult == true) 
            {
                if (wizardFrm != null && wizardFrm.Success)
                {
                    StatusTextBlock.Text = "Settings OK";
                    StatusTextBlock.Foreground = this.defaultColor;
                }
                else
                {
                    StatusTextBlock.Text = "Settings are not configured successfuly";
                    StatusTextBlock.Foreground = this.errorColor;
                }
                this.settings = Settings.Instance;
            }
        }

        private async void checkConnectionBtn_Click(object sender, RoutedEventArgs e)
        {
            await this.checkConnection();
        }

        private async void AppWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await this.startWalletIfLocal();
            await this.initialValidation();
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
    }
}
