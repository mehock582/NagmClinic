// shared-datatable.js
function initDataTable(selector, url, columns, extraOptions = {}) {
    var defaultOptions = {
        "pageLength": 5,
        "processing": true,
        "serverSide": true,
        "autoWidth": false,
        "responsive": true,
        "ordering": true,
        "searching": true,
        "lengthChange": true,
        
        // Bootstrap 5 DOM structure for DataTables
        "dom": '<"row align-items-center mb-3"<"col-12 col-md-6 mb-2 mb-md-0"l><"col-12 col-md-6 d-flex justify-content-md-end"f>>' +
               'rt' +
               '<"row align-items-center mt-3"<"col-12 col-md-5 mb-2 mb-md-0"i><"col-12 col-md-7 d-flex justify-content-md-end"p>>',
               
        "ajax": {
            "url": url,
            "type": "POST"
        },
        "columnDefs": [
            { "className": "text-center align-middle text-nowrap", "targets": "_all" }
        ],
        "columns": columns,
        
        // Complete Arabic Localization
        "language": {
            "search": "_INPUT_",
            "searchPlaceholder": "بحث في السجلات...",
            "lengthMenu": "إظهار _MENU_ مدخلات",
            "info": "إظهار _START_ إلى _END_ من أصل _TOTAL_ مدخل",
            "infoEmpty": "يعرض 0 إلى 0 من أصل 0 سجل",
            "infoFiltered": "(مفلترة من مجموع _MAX_ مدخل)",
            "emptyTable": "لا توجد بيانات متاحة في الجدول",
            "zeroRecords": "لم يتم العثور على سجلات مطابقة",
            "paginate": {
                "first": "الأول",
                "previous": "السابق",
                "next": "التالي",
                "last": "الأخير"
            }
        }
    };
    
    // Deep merge extra options (like default order or custom callbacks)
    var finalOptions = $.extend(true, {}, defaultOptions, extraOptions);
    return $(selector).DataTable(finalOptions);
}
