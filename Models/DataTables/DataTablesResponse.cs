namespace NagmClinic.Models.DataTables
{
    public class DataTablesResponse<T>
    {
        public int draw { get; set; }
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public T data { get; set; } = default!;
        public string? error { get; set; }
    }
}
