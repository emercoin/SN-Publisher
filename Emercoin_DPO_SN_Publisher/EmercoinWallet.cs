namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class EmercoinWallet
    {
        private JsonRpcClient client;
        private string rootDpoAddress;

        public EmercoinWallet(string host, string port, string username, string password)
        {
            this.client = JsonRpcClient.Create(host, port, username, password);
        }

        public enum NameCreationStatusEnum
        {
            Occupied,
            Exists,
            Created
        }

        public static async Task<bool> CheckConnection(string host, string port, string username, string password, string rootDPOName)
        {
            bool success = false;

            // TODO: use wallet.CheckConnection() instead
            var wallet = new EmercoinWallet(host, port, username, password);
            string balance = await Task.Run(() => wallet.GetBalance());
            await Task.Run(() => wallet.LoadRootDPO(rootDPOName));

            // TODO: is this needed?
            success = true;
            return success;
        }

        public async Task<bool> CheckConnection(string rootDpoName)
        {
            bool success = false;

            string balance = await Task.Run(() => this.GetBalance());
            await Task.Run(() => this.LoadRootDPO(rootDpoName));

            success = true;
            return success;
        }

        public async Task<bool> CheckWalletPassphrase(string passphrase) 
        {
            bool success = false;

            try
            {
                await Task.Run(() => this.LockWallet());
                await Task.Run(() => this.UnlockWallet(passphrase, 100000));
                GetInfoResult info = await Task.Delay(2000).ContinueWith<GetInfoResult>((t) => this.GetWalletInfo());
                success = !info.locked;
            }
            catch (Exception ex)
            {
                success = false;
            }

            return success;
        }

        public string GetBalance()
        {
            var command = new JObject();
            command["method"] = "getinfo";

            try {
                JObject result = this.client.SendCommand(command);
                return result["result"]["balance"].ToObject<string>();
            }
            catch (JsonRpcException ex) {
                throw new EmercoinWalletException("Could not get wallet balance", ex);
            }
        }

        public GetInfoResult GetWalletInfo()
        {
            var command = new JObject();
            command["method"] = "getinfo";

            try
            {
                JObject resJson = this.client.SendCommand(command);
                var res = resJson["result"];
                string balance = res["balance"].ToObject<string>();
                string errors = res["errors"].ToObject<string>();
                bool locked = string.Equals(errors, "Info: Minting suspended due to locked wallet.", StringComparison.InvariantCultureIgnoreCase);

                return new GetInfoResult() { balance = balance, locked = locked };
            }
            catch (JsonRpcException ex)
            {
                throw new EmercoinWalletException("Could not get wallet balance", ex);
            }
        }

        public void LoadRootDPO(string rootDpoName)
        {
            JObject rootName = this.listMyName(rootDpoName);
            if (rootName == null) {
                throw new EmercoinWalletException("Could not get root DPO address");
            }
            this.rootDpoAddress = rootName["address"].ToObject<string>();
        }

        public NameCreationStatusEnum CreateOrCheckName(string name, int days)
        {
            // if the name is occupied
            if (this.checkNameExists(name)) {
                // if the name is mine
                if (this.CheckNameIsMine(name)) {
                    return NameCreationStatusEnum.Exists;
                }
                return NameCreationStatusEnum.Occupied;
            }
            else {
                // reserve the name
                var command = new JObject();
                command["method"] = "name_new";
                command["params"] = new JArray() { name, "(empty)", days, this.rootDpoAddress };
                try {
                    JObject result = this.client.SendCommand(command);
                }
                catch (JsonRpcException ex) {
                    JObject responseJson = JObject.Parse(ex.Response);
                    if (responseJson["error"]["code"].ToObject<string>() == "-32603") {
                        return NameCreationStatusEnum.Exists;
                    }
                    else {
                        throw new EmercoinWalletException("Could not name_new", ex);
                    }
                }
                return NameCreationStatusEnum.Created;
            }
        }

        public string SignMessage(string message)
        {
            var command = new JObject();
            command["method"] = "signmessage";
            command["params"] = new JArray() { this.rootDpoAddress, message };

            try {
                JObject response = this.client.SendCommand(command);
                return response["result"].ToObject<string>();
            }
            catch (JsonRpcException ex) {
                throw new EmercoinWalletException("Could not signmessage", ex);
            }
        }

        public void UpdateName(string name, string value, int days)
        {
            var command = new JObject();
            command["method"] = "name_update";
            command["params"] = new JArray() { name, value, days, this.rootDpoAddress };

            try {
                JObject response = this.client.SendCommand(command);
            }
            catch (JsonRpcException ex) {
                throw new EmercoinWalletException("Could not name_update", ex);
            }
        }

        public void UnlockWallet(string passPhrase, int timeout)
        {
            var command = new JObject();
            command["method"] = "walletpassphrase";
            command["params"] = new JArray() { passPhrase, timeout };

            try
            {
                JObject response = this.client.SendCommand(command);
            }
            catch (JsonRpcException ex)
            {
                throw new EmercoinWalletException("Could not walletpassphrase", ex);
            }
        }

        public void LockWallet()
        {
            var command = new JObject();
            command["method"] = "walletlock";

            try
            {
                JObject response = this.client.SendCommand(command);
            }
            catch (JsonRpcException ex)
            {
                throw new EmercoinWalletException("Could not walletlock", ex);
            }
        }

        public bool CheckNameIsMine(string name)
        {
            return this.listMyName(name) != null;
        }

        private bool checkNameExists(string name)
        {
            var command = new JObject();
            command["method"] = "name_show";
            command["params"] = new JArray() { name };

            try {
                JObject result = this.client.SendCommand(command);
                return true;
            }
            catch (JsonRpcException ex) {
                if (ex.InnerException is System.Net.WebException) {
                    var innerException = (WebException)ex.InnerException;
                    if (innerException.Status == WebExceptionStatus.ProtocolError) {
                        return false;
                    }
                }
                throw new EmercoinWalletException("Could not name_show", ex);
            }
        }

        private JObject listMyName(string name)
        {
            var command = new JObject();
            command["method"] = "name_list";
            command["params"] = new JArray() { name };

            try {
                JObject response = this.client.SendCommand(command);
                if (response["result"].HasValues) {
                    return (JObject)response["result"][0];
                }
                return null;
            }
            catch (JsonRpcException ex) {
                throw new EmercoinWalletException("Could not name_list", ex);
            }
        }
    }

    internal class GetInfoResult
    {
        public string balance { get; set; }
        public bool locked { get; set; }
    }

    internal class EmercoinWalletException : Exception
    {
        public EmercoinWalletException(string message)
            : base(message)
        {
        }

        public EmercoinWalletException(string message, JsonRpcException ex)
            : base(message, ex)
        {
        }
    }
}
