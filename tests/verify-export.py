"""Independent OOXML validation. Never print generated private keys."""
import sys
from pathlib import Path
from openpyxl import load_workbook

root = Path(sys.argv[1])
def rows(path):
    w = load_workbook(path)
    s = w.active
    assert s.title == 'Results'
    assert s.freeze_panes == 'A4'
    assert s.auto_filter.ref == f'A3:C{s.max_row}'
    assert str(s['C4'].number_format) == '@'
    assert s.column_dimensions['C'].width >= 70
    data = []
    for row in s.iter_rows(min_row=4):
        assert all(c.data_type == 's' for c in row)
        network, address, key = (c.value for c in row)
        assert len(key) == 64 and all(c in '0123456789abcdef' for c in key)
        data.append((network, address, key))
    w.close()
    return data

for chain in ('eth', 'polygon', 'tron', 'cancel'):
    folder = root / chain
    expected = []
    for p in folder.glob('*.txt'):
        expected += [tuple(x.split('\t')) for x in p.read_text(encoding='utf-8-sig').splitlines() if x and not x.startswith('#')]
    actual = [r for p in sorted(folder.glob('*.xlsx')) for r in rows(p)]
    assert actual == expected, f'{chain}: TXT/XLSX rows differ'
    assert len(set(actual)) == len(actual)
    print(f'{chain}: {len(actual)} records match TXT, type=text, frozen header/filter PASS')

actual = [r for p in sorted((root/'public-vectors').glob('*.xlsx')) for r in rows(p)]
assert [r[2] for r in actual] == [f'{i:064x}' for i in range(1, 8)]
assert len(rows(next((root/'excel-only').glob('*.xlsx')))) == 3
assert not list((root/'excel-only').glob('*.txt'))
print('Leading zeros, split order 3+3+1, Excel-only PASS')
