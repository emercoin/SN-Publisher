namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    internal class AppUtils
    {
        public const string AppName = "Emercoin DPO SN Publisher";

        public static void ShowException(Exception ex, Window owner)
        {
            ExceptionViewer ev = new ExceptionViewer("An unexpected error occurred in the application.", ex, owner);
            ev.Title = "Error - " + AppName;
            ev.ShowDialog();
        }

        public static void ShowExceptionMsg(Exception e)
        {
            string errorMsg = e.ToString();

            MessageBox.Show(
                 errorMsg,
                 AppName + " error",
                 MessageBoxButton.OK,
                 MessageBoxImage.Error);
        }
    }
}
