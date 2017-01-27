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

    public class EmercoinWallet
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
