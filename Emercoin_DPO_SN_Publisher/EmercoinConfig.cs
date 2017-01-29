using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmercoinDPOSNP
{
    public class EmercoinConfig
    {
        public const string serverParam = "server";
        public const string listenParam = "listen";
        public const string userParam = "rpcuser";
        public const string rpcPasswordParam = "rpcpassword";
        public const string debugParam = "debug";
        public const string portParam = "port";

        public Dictionary<string, EmercoinConfigValue> Parameters = new Dictionary<string, EmercoinConfigValue>();

        public string GetParameterValue(string name)
        {
            EmercoinConfigValue paramVal;
            this.Parameters.TryGetValue(name, out paramVal);

            if (paramVal == null || !paramVal.Enabled) 
            {
                return null;
            }
            else
            {
                return paramVal.Value;
            }
        }

        public void SetParameter(string name, string value)
        {
            EmercoinConfigValue paramObj = null;
            this.Parameters.TryGetValue(name, out paramObj);
            if (paramObj == null)
            {
                paramObj = new EmercoinConfigValue(name, value, true);
                this.Parameters.Add(name, paramObj);
            }
            else
            {
                paramObj.Value = value;
                paramObj.Enabled = true;
            }
        }

        public bool ValidateParameters(string port, string username, string password)
        {
            string server = this.GetParameterValue(EmercoinConfig.serverParam) ?? string.Empty;
            string listen = this.GetParameterValue(EmercoinConfig.serverParam) ?? string.Empty;

            string confPort = this.GetParameterValue(EmercoinConfig.portParam) ?? string.Empty;
            string confUsername = this.GetParameterValue(EmercoinConfig.userParam) ?? string.Empty;
            string confPassword = this.GetParameterValue(EmercoinConfig.rpcPasswordParam) ?? string.Empty;

            var portequal = string.Equals(port, confPort);
            var usernameequal = string.Equals(username, confUsername);
            var passwordequal = string.Equals(password, confPassword);
            //&& (listen != "0")
            return (server == "1")  && portequal && usernameequal && passwordequal;
        }
    }

    public class EmercoinConfigValue 
    {
        public EmercoinConfigValue(string name, string value, bool enabled, int lineId = -1) 
        {
            this.Name = name;
            this.Value = value;
            this.Enabled = enabled;
            this.LineId = lineId;
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public bool Enabled { get; set; }

        // zero-based element id in line's array
        public int LineId { get; set; }

        public override string ToString()
        {
            return (this.Enabled ? string.Empty : "#") + this.Name + "=" + this.Value;
        }
    }
}
