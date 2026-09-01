using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DanaProcessing
{
    /// <summary>
    /// A single row of a Table, equivalent to Processing's TableRow —
    /// https://processing.org/reference/TableRow.html. Values are stored as
    /// strings internally and parsed on demand by GetInt/GetFloat, matching
    /// Processing's own loosely-typed CSV model (a cell doesn't have a fixed
    /// type; it's just text you can ask to be interpreted a certain way).
    /// </summary>
    public sealed class TableRow
    {
        private readonly Table _table;
        internal readonly List<string> Values;

        internal TableRow(Table table, List<string> values)
        {
            _table = table;
            Values = values;
        }

        public string GetString(int col) => col >= 0 && col < Values.Count ? Values[col] : "";
        public string GetString(string columnName) => GetString(_table.ColumnIndex(columnName));
        public int GetInt(int col) => int.TryParse(GetString(col), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        public int GetInt(string columnName) => GetInt(_table.ColumnIndex(columnName));
        public float GetFloat(int col) => float.TryParse(GetString(col), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        public float GetFloat(string columnName) => GetFloat(_table.ColumnIndex(columnName));

        public void SetString(int col, string value) => Ensure(col, value);
        public void SetString(string columnName, string value) => SetString(_table.ColumnIndex(columnName), value);
        public void SetInt(int col, int value) => SetString(col, value.ToString(CultureInfo.InvariantCulture));
        public void SetInt(string columnName, int value) => SetInt(_table.ColumnIndex(columnName), value);
        public void SetFloat(int col, float value) => SetString(col, value.ToString(CultureInfo.InvariantCulture));
        public void SetFloat(string columnName, float value) => SetFloat(_table.ColumnIndex(columnName), value);

        private void Ensure(int col, string value)
        {
            while (Values.Count <= col)
                Values.Add("");
            Values[col] = value;
        }
    }

    /// <summary>
    /// A simple table of rows and columns, equivalent to Processing's Table —
    /// https://processing.org/reference/Table.html. Reads/writes CSV
    /// (Processing's Table also handles TSV and its own binary .tbl format;
    /// only CSV is implemented here, by far the common case). Get one via
    /// Sketch.LoadTable(path, "header") or `new Table()`, then
    /// Sketch.SaveTable(table, path) to write it back out.
    /// </summary>
    public sealed class Table
    {
        private readonly List<string> _columnNames = new();
        private readonly List<TableRow> _rows = new();

        /// <summary>Whether this table has named columns (set by loading with the "header" option, or by calling SetColumnTitle).</summary>
        public bool HasHeader { get; set; }

        public int ColumnCount => _columnNames.Count;
        public int RowCount => _rows.Count;

        public TableRow GetRow(int i) => _rows[i];

        /// <summary>All rows, in order, like iterating Processing's table.rows().</summary>
        public IEnumerable<TableRow> Rows => _rows;

        public string ColumnName(int i) => i >= 0 && i < _columnNames.Count ? _columnNames[i] : "";

        internal int ColumnIndex(string name)
        {
            int i = _columnNames.IndexOf(name);
            if (i == -1)
                throw new ArgumentException($"La tabla no tiene una columna llamada '{name}'.");
            return i;
        }

        public void SetColumnTitle(int col, string name)
        {
            while (_columnNames.Count <= col)
                _columnNames.Add("");
            _columnNames[col] = name;
            HasHeader = true;
        }

        /// <summary>Appends a new, empty row and returns it for filling in, like Processing's addRow().</summary>
        public TableRow AddRow()
        {
            var row = new TableRow(this, new List<string>());
            _rows.Add(row);
            return row;
        }

        /// <summary>Loads a CSV file into a Table, like Processing's loadTable(path, "header"). `options` supports "header" (first line names the columns) — pass "" for a plain headerless CSV.</summary>
        public static Table LoadCsv(string path, string options = "")
        {
            var table = new Table { HasHeader = options.Contains("header") };
            var lines = File.ReadAllLines(path);
            int start = 0;
            if (table.HasHeader && lines.Length > 0)
            {
                table._columnNames.AddRange(SplitCsvLine(lines[0]));
                start = 1;
            }
            for (int i = start; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;
                table._rows.Add(new TableRow(table, SplitCsvLine(lines[i])));
            }
            return table;
        }

        /// <summary>Writes this table out as CSV, like Processing's saveTable(table, path).</summary>
        public void SaveCsv(string path)
        {
            using var writer = new StreamWriter(path);
            if (HasHeader)
                writer.WriteLine(string.Join(",", _columnNames.Select(EscapeCsv)));
            foreach (var row in _rows)
                writer.WriteLine(string.Join(",", row.Values.Select(EscapeCsv)));
        }

        // Manejo simple de CSV: soporta campos entre comillas con comas
        // internas, pero no comillas escapadas (dobles) dentro de un campo
        // citado — suficiente para el CSV "normal" que produce la mayoría de
        // hojas de cálculo, no un parser RFC 4180 completo.
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            foreach (char ch in line)
            {
                if (ch == '"')
                { inQuotes = !inQuotes; continue; }
                if (ch == ',' && !inQuotes)
                { result.Add(current.ToString()); current.Clear(); continue; }
                current.Append(ch);
            }
            result.Add(current.ToString());
            return result;
        }

        private static string EscapeCsv(string value) =>
            value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}