namespace EmercoinDPOSNP.SettingsWizard
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Win32;

    public class WalletInstallInfo
    {
        private const string fileName = "emercoin-qt.exe";

        public enum BitnessEnum
        {
            x32,
            x64
        }

        public string DisplayName { get; set; }
        public Version Version { get; set; }
        public string Folder { get; set; }

        public string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(this.Folder))
                {
                    return null;
                }

                return this.Folder + "\\" + fileName;
            }
        }

        public BitnessEnum Bitness { get; set; }

        public static IEnumerable<WalletInstallInfo> GetInfo()
        {
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var wi = GetInfo(Registry.CurrentUser, registry_key);
            if (wi == null) {
                wi = GetInfo(Registry.LocalMachine, registry_key);
            }

            return wi;
        }

        public static IEnumerable<WalletInstallInfo> GetInfo(RegistryKey regKeyBase, string regKeyStr)
        {
            using (Microsoft.Win32.RegistryKey key = regKeyBase.OpenSubKey(regKeyStr)) {
                if (key != null) {
                    foreach (string subkey_name in key.GetSubKeyNames().Where(k => !string.IsNullOrEmpty(k))) {
                        using (RegistryKey subkey = key.OpenSubKey(subkey_name)) {
                            var dispName = subkey.GetValue("DisplayName");
                            var dispVersion = subkey.GetValue("DisplayVersion");
                            var path = subkey.GetValue("UninstallString");
                            if (dispName != null && dispName.ToString().ToUpper().StartsWith("EMERCOIN CORE")) {
                                Version version = new Version(0, 0);
                                if (dispVersion != null) {
                                    Version.TryParse(dispVersion.ToString(), out version);
                                }

                                var info = new WalletInstallInfo();
                                info.DisplayName = dispName.ToString();
                                info.Version = version;
                                info.Folder = path != null ? Directory.GetParent(path.ToString()).FullName : string.Empty;
                                info.Bitness = dispName.ToString().ToUpper() == "Emercoin Core (64-bit)".ToUpper() ? BitnessEnum.x64 : BitnessEnum.x32;

                                yield return info;
                            }
                        }
                    }
                }
            }
        }

        public bool IsExecuting() 
        {
            return this.ActiveProcess() != null;
        }

        public Process ActiveProcess()
        {
            var processName = Path.GetFileNameWithoutExtension(fileName);
            return Process.GetProcesses().FirstOrDefault(p => string.Equals(p.ProcessName, processName, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
