using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmercoinDPOSNP
{
    // Логика верхнего уровня при обновлении конфиг файла
    // 1. Читаем все строки из существующего конфига
    // 2 Если отсутствует, то создаем новый
    // 3 обновляем некоторые нужные строки-параметры (добавляем, если их нет)
    // 4 Записываем все строки прочитанного конфига (в т.ч. измененные) в файл

    /// <summary>
    /// Управляет файлом конфигурации.
    /// Позволяет читать, обновлять параметры конфигурации
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
                lines = File.ReadAllLines(filePath).ToList();
            }
        }

        public EmercoinConfig ReadConfig() 
        {
            var config = new EmercoinConfig();

            int lineId = 0;
            foreach (var l in this.lines) 
            {
                var param = parseLine(l, lineId);
                if (param != null) 
                {
                    config.Parameters.Add(param.Name, param);
                }
                lineId++;
            }

            return config;
        }

        public void FixToActive(EmercoinConfig config) 
        {
            if (config == null) 
            {
                return;
            }

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
                // новые значения добавляем в конец списка
                // старые - изменяем
                if (p.LineId == -1) 
                {
                    this.lines.Add(p.ToString());
                }
                else 
                {
                    this.lines[p.LineId] = p.ToString();
                }
            }

            File.WriteAllLines(filePath, lines);
        }

        private EmercoinConfigValue parseLine(string line, int lineId) 
        {
            if (!string.IsNullOrWhiteSpace(line)) 
            {
                var arr = line.Split(new char[] { '=' });
                if(arr.Length == 2)
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

        private string randomString(int length)
        {
            var random = new Random();

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
