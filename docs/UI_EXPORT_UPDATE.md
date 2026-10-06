# Interface and export behavior

The main window is titled **Astra v1.1.0**, with a fixed 820 × 588 client area at 96 DPI. It cannot be resized or maximized. The network badge appears at the upper right of the hardware card beside the detected GPU name. Hardware details are collected at startup, without continuous hardware polling.

Colors follow Windows application light/dark mode through system notifications, with high-contrast system-color fallback. Disabled inputs and the separate log window use the same palette. UI regression checks cover fixed size, current-user Desktop defaults, folder-button bounds, all three metric-card borders and live palette changes. Unusual DPI and multi-monitor configurations still need validation on the target machine.

## Folder-based output

Choose an existing folder and TXT, Excel or both. The default is the current user's system Desktop path, including redirected Desktops; no username or drive is hard-coded. Choose an unsynced local folder for private-key storage. Permission failures stop the task without silently changing the destination.

TXT folder append creates or continues `eth_results.txt`, `tron_results.txt` or `polygon_results.txt`; every task uses new random seeds. Legacy `--output file.txt --append` is supported and requires an existing file. Excel always creates new files, never rewrites old workbooks, and does not create an empty workbook when no results are found.

```bat
vanity.exe --chain polygon --suffix-repeat 6 --count 20 --output-dir D:\Results --format both
vanity.exe --chain tron --prefix M --suffix 88 --count 20 --output-dir D:\Results --format xlsx
vanity.exe --chain eth --suffix 888 --count 20 --output-dir D:\Results --format txt --append
vanity.exe --export-test
```

Excel uses native .NET ZIP and OOXML writing; no Office, COM, Python or extra runtime is required. Each workbook has Network, Address and Private key columns, stored as text with leading zeros preserved, frozen headers, filters and alternating row colors. In this English distribution the worksheet is named **Results**.

## Flushing and performance

A bounded background queue publishes XLSX volumes every 5 seconds or 10,000 rows. Intermediate `.xlsx.pending` / `.xlsx.writing` data has the same protected ACL as final output and is flushed every second. Normal stop finishes the remaining workbook. The flushed metric includes intermediate Excel data; the safely-stopped state indicates final export completion.

Export failures stop search and retain intermediate files. Those files are not directly openable Excel workbooks, and automatic crash recovery is not implemented. Dual output is not a cross-file transaction. Background export can still reduce throughput on rules that match very frequently; no zero-overhead claim is made.

## Recorded checks

The implementation was checked for all three networks, TXT/XLSX row agreement, Excel-only export, ACLs, count limits, public keys 1–7 with leading zeros, 3+3+1 volume ordering, no-overwrite behavior, cancellation and retained staging data after injected failure. `tests/verify-export.py` uses independent openpyxl parsing without printing private keys. Microsoft Excel opened an earlier public-vector workbook without repair prompts.

Earlier real Ctrl+C tests completed dual-format output with 3,056 records in 62.6 ms after the signal; a final repeat saved 2,640 records in 65.6 ms with exit code 0. These are individual observations, not latency guarantees.

On 2026-10-06, RTX 5090 D v2 / driver 610.74, Polygon exact-case, 256 groups × batch 64, 12 seconds per test:

| Rule / metric | TXT | TXT + Excel |
|---|---:|---:|
| Eight repeated suffix characters, M candidates/s | 1,108.851 | 1,105.646 |
| Every address matches, verified/saved records/s | 1,086.770 | 995.957 |

The easy dual-format run saved 12,032 records, independently matched across TXT and all Excel volumes. These short runs took place during normal desktop use and do not establish a precise long-term performance difference. Generated test wallets and raw logs are excluded from the distributed project.
