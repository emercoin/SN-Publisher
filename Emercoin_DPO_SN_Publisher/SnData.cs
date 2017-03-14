namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using ClosedXML.Excel;

    internal class SnData
    {
        public const string SerialColumnName = "SN";

        private const char separator = ',';

        private SnData()
        {
        }

        public static SnData LoadFromCsv(string filename)
        {
            var snData = new SnData();

            using (var reader = new StreamReader(File.OpenRead(filename))) {
                string line = reader.ReadLine();
                if (reader.EndOfStream) {
                    throw new EmptyException();
                }
                snData.HeaderRow = line.Split(separator);
                if (snData.HeaderRow.Length == 0 || snData.HeaderRow[0] != SerialColumnName) {
                    throw new SerialColumnException();
                }

                snData.Rows = new List<string[]>();
                while (!reader.EndOfStream) {
                    line = reader.ReadLine();
                    if (line.Trim() != string.Empty) {
                        string[] values = line.Split(separator);
                        if (values.Length != snData.HeaderRow.Length) {
                            throw new InconsistentException();
                        }
                        if (snData.Rows.Select(r => r[0]).Where(v => v == values[0]).Count() > 0) {
                            throw new DuplicateSerialException(values[0]);
                        }
                        snData.Rows.Add(values);
                    }
                }
            }

            return snData;
        }

        public static SnData LoadFromXlsx(string filename)
        {
            var snData = new SnData();
            snData.Rows = new List<string[]>();

            var workbook = new XLWorkbook(filename);
            IXLWorksheet worksheet = workbook.Worksheets.First();
            int i = 0;
            foreach (IXLRow row in worksheet.Rows()) {
                if (i == 0) {
                    var header = new List<string>();
                    foreach (IXLCell cell in row.Cells()) {
                        header.Add(cell.Value.ToString());
                    }
                    snData.HeaderRow = header.ToArray();
                    if (snData.HeaderRow.Length == 0 || snData.HeaderRow[0] != SerialColumnName) {
                        throw new SerialColumnException();
                    }
                }
                else {
                    var values = new List<string>();
                    foreach (IXLCell cell in row.Cells()) {
                        values.Add(cell.Value.ToString());
                    }
                    if (values.Count() >= 1 && values[0].Trim() != string.Empty) {
                        if (values.Count() != snData.HeaderRow.Length) {
                            throw new InconsistentException();
                        }
                        if (snData.Rows.Select(r => r[0]).Where(v => v == values[0]).Count() > 0) {
                            throw new DuplicateSerialException(values[0]);
                        }
                        snData.Rows.Add(values.ToArray());
                    }
                }
                i++;
            }

            if (snData.Rows.Count == 0) {
                throw new EmptyException();
            }

            return snData;
        }

        public string[] HeaderRow
        {
            get;
            private set;
        }
        public List<string[]> Rows
        {
            get;
            private set;
        }

        public class EmptyException : FileFormatException
        {
        }

        public class InconsistentException : FileFormatException
        {
        }

        public class SerialColumnException : FileFormatException
        {
        }

        public class DuplicateSerialException : FileFormatException
        {
            public DuplicateSerialException(string sn)
                : base(sn)
            {
            }
        }
    }
}
