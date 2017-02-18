namespace EmercoinDPOSNP.AppSettings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    [Serializable]
    public class Settings
    {
        private Settings()
        {
            this.RootDPOName = string.Empty;
            this.Host = "localhost";
            this.Port = "6662";
            this.Username = string.Empty;
            this.RpcPassword = string.Empty;
            this.WalletPassphrase = string.Empty;
        }

        private Settings(bool loadDefaults)
        {
            this.RootDPOName = "myname";
            this.Host = "localhost";
            this.Port = "6662";
            this.Username = "rpcemc";
            this.RpcPassword = string.Empty;
            this.WalletPassphrase = string.Empty;
        }

        public static Settings Instance
        {
            get
            {
                if (settings == null) {
                    settings = new Settings();
                }
                return settings;
            }
        }

        public string Host { get; set; }
        public string Port { get; set; }
        public string Username { get; set; }
        public string RpcPassword { get; set; }
        public string RootDPOName { get; set; }
        public string WalletPassphraseEncoded { get; set; }

        [XmlIgnore]
        public string WalletPassphrase
        {
            get
            {
                string encStr = string.Empty;
                if (!string.IsNullOrEmpty(this.WalletPassphraseEncoded)) {
                    encStr = Encryption.DecryptString_Aes(this.WalletPassphraseEncoded);
                }
                return encStr;
            }

            set
            {
                if (!string.IsNullOrEmpty(value)) {
                    this.WalletPassphraseEncoded = Encryption.EncryptString_Aes(value);
                }
                else {
                    this.WalletPassphraseEncoded = string.Empty;
                }
            }
        }

        private static Settings settings { get; set; }

        private static string settingsPath
        {
            get
            {
                return GetWorkingFolder() + "\\" + "settings.xml";
            }
        }

        public static void ReadSettings()
        {
            try {
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                using (var sw = new StreamReader(settingsPath)) {
                    Settings obj = x.Deserialize(sw) as Settings;
                    if (obj != null) {
                        settings = obj;
                    }
                }
            }
            catch {
            }
        }

        public static void WriteSettings()
        {
            try
            {
                if (settings == null) 
                {
                    settings = new Settings();
                }

                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                using (var sw = new StreamWriter(settingsPath))
                {
                    x.Serialize(sw, settings);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception while saving application settings: {0}", ex);
            }
        }

        public bool HostIsLocal() 
        {
            string[] localAddresses = { "localhost", "127.0.0.1" };
            return localAddresses.Contains(this.Host, StringComparer.InvariantCultureIgnoreCase);
        }

        private static string GetWorkingFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "Emercoin SN Publisher";
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}
