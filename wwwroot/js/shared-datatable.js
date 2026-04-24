// shared-datatable.js
function initDataTable(selector, url, columns, extraOptions = {}) {
    var defaultOptions = {
        "pageLength": 5,
        "processing": true,
        "serverSide": true,
        "dom": '<"table-responsive"<"table-hover align-middle"t>>lrip',
        "ajax": {
            "url": url,
            "type": "POST"
        },
        "columnDefs": [
            { "className": "text-center align-middle text-nowrap", "targets": "_all" }
        ],
        "columns": columns,
        "language": {
            // Centralized Arabic translation URL
            "url": "https://cdn.datatables.net/plug-ins/1.13.7/i18n/ar.json"
        },
        "responsive": true,
        "ordering": true,
        "searching": true,
        "lengthChange": true
    };
    
    // Deep merge extra options (like default order or custom callbacks)
    var finalOptions = $.extend(true, {}, defaultOptions, extraOptions);
    return $(selector).DataTable(finalOptions);
}
