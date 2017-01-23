using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmercoinDPOSNP.SettingsWizard
{
    public class WalletInstallInfo
    {
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public BitnessEnum Bitness { get; set; }

        public static WalletInstallInfo GetInfo()
        {
            //TODO: Проверить установку 32-битного кошелька. Вероятно путь в реестре будет другой
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var wi = GetInfo(Registry.CurrentUser, registry_key);
            if (wi == null) 
            {
                wi = GetInfo(Registry.LocalMachine, registry_key);
            }

            return wi;
        }

        private static WalletInstallInfo GetInfo(RegistryKey regKeyBase, string regKeyStr)
        {
            using (Microsoft.Win32.RegistryKey key = regKeyBase.OpenSubKey(regKeyStr))
            {
                foreach (string subkey_name in key.GetSubKeyNames().Where(k => !string.IsNullOrEmpty(k)))
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        var info = new WalletInstallInfo();

                        var dispName = subkey.GetValue("DisplayName");
                        var version = subkey.GetValue("DisplayVersion");
                        var path = subkey.GetValue("UninstallString");
                        if (dispName != null && dispName.ToString().ToUpper().StartsWith("EMERCOIN CORE"))
                        {
                            info.DisplayName = dispName.ToString();
                            info.Version = version != null ? version.ToString() : string.Empty;
                            info.Path = path != null ? Directory.GetParent(path.ToString()).FullName : string.Empty;
                            info.Bitness = dispName.ToString().Contains("64") ? BitnessEnum.x64 : BitnessEnum.x32;
                            
                            return info;
                        }
                    }
                }
            }

            return null;
        }

        public enum BitnessEnum 
        {
            x32,
            x64
        }
    }
}
