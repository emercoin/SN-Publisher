namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class CsvData
    {
        public const string SerialColumnName = "SN";

        private const char separator = ',';

        private string[] headerRow;
        private List<string[]> rows;

        public CsvData(string filename)
        {
            using (var reader = new StreamReader(File.OpenRead(filename))) {
                string line = reader.ReadLine();
                if (reader.EndOfStream) {
                    throw new EmptyException();
                }
                this.headerRow = line.Split(separator);
                if (this.headerRow.Length == 0 || this.headerRow[0] != SerialColumnName) {
                    throw new SerialColumnException();
                }

                this.rows = new List<string[]>();
                while (!reader.EndOfStream) {
                    line = reader.ReadLine();
                    if (line.Trim() != string.Empty) {
                        string[] values = line.Split(separator);
                        if (values.Length != this.headerRow.Length) {
                            throw new InconsistentException();
                        }
                        if (this.rows.Select(r => r[0]).Where(v => v == values[0]).Count() > 0) {
                            throw new DuplicateSerialException(values[0]);
                        }
                        this.rows.Add(values);
                    }
                }
            }
        }

        public string[] HeaderRow
        {
            get { return this.headerRow; }
        }
        public List<string[]> Rows
        {
            get { return this.rows; }
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
