using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmercoinDPOSNP.SettingsWizard
{
    public class WalletClient
    {
        private string rootDPOName;
        private string host;
        private string port;
        private string username;
        private string password;

        private EmercoinWallet wallet;

        public async Task<bool> checkConnection(string host, string port, string username, string password, string rootDPOName)
        {
            bool success = false;
            
            this.host = host;
            this.port = port;
            this.username = username;
            this.password = password;
            this.rootDPOName = rootDPOName;

            this.wallet = new EmercoinWallet(this.host, this.port, this.username, this.password);
            string balance = await Task.Run(() => this.wallet.GetBalance());
            await Task.Run(() => this.wallet.LoadRootDPO(this.rootDPOName));
            
            success = true;
            
            return success;
        }


        private static bool portNumberTextAllowed(string text)
        {
            Regex regex = new Regex("[0-9]{1,5}");
            return regex.IsMatch(text);
        }
    }
}
