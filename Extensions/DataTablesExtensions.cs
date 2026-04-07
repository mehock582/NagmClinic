using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Extensions
{
    public static class DataTablesExtensions
    {
        public static DataTablesParameters GetDataTablesParameters(this HttpRequest request)
        {
            var form = request.Form;
            
            return new DataTablesParameters
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out var draw) ? draw : 0,
                Start = int.TryParse(form["start"].FirstOrDefault(), out var start) ? start : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out var length) ? length : 10,
                Search = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "value", form["search[value]"].FirstOrDefault() ?? string.Empty },
                    { "regex", form["search[regex]"].FirstOrDefault() ?? "false" }
                },
                Order = Enumerable.Range(0, 10)
                    .Select(i => new DataTablesOrder
                    {
                        Column = int.TryParse(form[$"order[{i}][column]"].FirstOrDefault(), out var col) ? col : -1,
                        Dir = form[$"order[{i}][dir]"].FirstOrDefault() ?? string.Empty
                    })
                    .Where(o => o.Column != -1)
                    .ToList()
            };
        }
    }
}
