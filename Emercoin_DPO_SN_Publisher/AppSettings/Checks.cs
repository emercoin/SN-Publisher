namespace EmercoinDPOSNP.AppSettings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class Checks
    {
        public static bool IpAddressValid(string value)
        {
            var validIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
            return validIpAddressRegex.IsMatch(value);
        }

        public static bool HostNameValid(string value)
        {
            var validHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");
            return validHostnameRegex.IsMatch(value);
        }

        public static bool PortNumberValid(string value)
        {
            var regex = new Regex("^[0-9]{1,5}$");
            return regex.IsMatch(value);
        }
    }
}
