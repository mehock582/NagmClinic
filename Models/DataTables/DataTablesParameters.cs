using System.Collections.Generic;

namespace NagmClinic.Models.DataTables
{
    public class DataTablesParameters
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public Dictionary<string, string> Search { get; set; } = new();
        public List<DataTablesOrder> Order { get; set; } = new();
        public List<DataTablesColumn> Columns { get; set; } = new();
    }

    public class DataTablesOrder
    {
        public int Column { get; set; }
        public string Dir { get; set; } = null!;
    }

    public class DataTablesColumn
    {
        public string Data { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool Searchable { get; set; }
        public bool Orderable { get; set; }
        public Dictionary<string, string> Search { get; set; } = new();
    }
}
