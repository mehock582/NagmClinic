import os
import glob

views_dir = r"c:\Users\le\source\repos\NagmClinic\NagmClinic\Views"

css_link = '<link rel="stylesheet" href="https://cdn.datatables.net/1.13.7/css/dataTables.bootstrap5.min.css" />'
js_link1 = '<script src="https://cdn.datatables.net/1.13.7/js/jquery.dataTables.min.js"></script>'
js_link2 = '<script src="https://cdn.datatables.net/1.13.7/js/dataTables.bootstrap5.min.js"></script>'

for filepath in glob.glob(os.path.join(views_dir, '**', '*.cshtml'), recursive=True):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    new_content = content.replace(css_link, '').replace(js_link1, '').replace(js_link2, '')

    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated {filepath}")
