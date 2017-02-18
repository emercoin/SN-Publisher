namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    // High level logic of config file modification:Логика верхнего уровня при обновлении конфиг файла
    // 1. If the file does not exist, create a new one;
    // 2. Reading all the lines of existing config file;
    // 3. Updating necessary parameters (lines) in the file, adding non-existing ones;
    // 4. Writing all the lines (incl. updated) into the file.

    /// <summary>
    /// Class for managing of Emercoin wallet config file. Used to read, write and update the file.
    /// </summary>
    public class EmercoinConfigManager
    {
        private List<string> lines = new List<string>();
        private string[] supportedParams = new string[] 
        { 
            EmercoinConfig.serverParam, EmercoinConfig.listenParam, EmercoinConfig.userParam, 
            EmercoinConfig.rpcPasswordParam, EmercoinConfig.debugParam, EmercoinConfig.portParam
        };

        public EmercoinConfigManager(string filePath) 
        {
            if (File.Exists(filePath))
            {
                this.lines = File.ReadAllLines(filePath).ToList();
            }
        }

        public EmercoinConfig ReadConfig() 
        {
            var config = new EmercoinConfig();

            int lineId = 0;
            foreach (var l in this.lines) 
            {
                var param = this.parseLine(l, lineId);
                if (param != null && !config.Parameters.ContainsKey(param.Name)) 
                {
                    config.Parameters.Add(param.Name, param);
                }
                lineId++;
            }

            return config;
        }

        public void FixToActive(EmercoinConfig config) 
        {
            config.SetParameter(EmercoinConfig.serverParam, "1");
            config.SetParameter(EmercoinConfig.listenParam, "1");
            config.SetParameter(EmercoinConfig.userParam, "rpcemc");
            config.SetParameter(EmercoinConfig.rpcPasswordParam, randomString(6));
            config.SetParameter(EmercoinConfig.debugParam, "rpc");
            config.SetParameter(EmercoinConfig.portParam, "6662");
        }

        public void WriteConfig(EmercoinConfig config, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) 
            {
                throw new Exception("Invalid file path");
            }
            
            foreach (var p in config.Parameters.Values) 
            {
                // adding new values into the end of the list
                // changing existing values
                if (p.LineId == -1) 
                {
                    this.lines.Add(p.ToString());
                }
                else 
                {
                    this.lines[p.LineId] = p.ToString();
                }
            }

            File.WriteAllLines(filePath, this.lines);
        }

        private static string randomString(int length)
        {
            var random = new Random();

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private EmercoinConfigValue parseLine(string line, int lineId) 
        {
            if (!string.IsNullOrWhiteSpace(line)) 
            {
                var arr = line.Split(new char[] { '=' });
                if (arr.Length == 2)
                {
                    string nameStr = arr[0];
                    nameStr = nameStr.TrimStart('\t', ' ');
                    bool enabled = !nameStr.StartsWith("#");
                    string name = nameStr.Trim('\t', ' ', '#');
                    string value = arr[1].Trim('\t', ' ');

                    if (this.supportedParams.Contains(name)) 
                    {
                        return new EmercoinConfigValue(name, value, enabled, lineId);
                    }
                }
            }

            return null;
        }
    }
}
