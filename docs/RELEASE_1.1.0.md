# Astra v1.1.0 release verification

Date: 2026-10-06. Hardware: RTX 5090 D v2, NVIDIA driver 610.74, Windows 11.

- Window title: `Astra v1.1.0`. GUI and CLI file version: `1.1.0.0`; product version: `1.1.0`.
- This distribution contains English source, GUI, CLI, export headers, README and documentation.
- The English and Chinese builds use identical CUDA source and PTX. This release work changes branding, localization and distribution layout.
- Both builds passed extended GPU/CPU all-candidate verification, three-network integration, append/no-overwrite checks, export tests and injected writer-failure checks.
- Independent openpyxl validation matched seven records per network against TXT, plus 2,768 English cancellation-test records and 2,576 Chinese cancellation-test records. Leading zeros, volume order and Excel-only output passed.
- Actual Windows Ctrl+C: English saved 3,216 records and exited in 38.0 ms after the signal; Chinese saved 2,992 records and exited in 43.3 ms. Both exit codes were zero. These are single observations, not latency guarantees.
- Light and dark UI regression checks passed for the release title, fixed window, current-user Desktop, folder button and three metric cards. Actual application windows were visually inspected.
- Release packaging uses explicit file allowlists. Runtime DLLs, CUDA files, documentation and original third-party licenses are included. Portable and source ZIPs have SHA-256 manifests.
- Old generated wallets, raw logs, screenshots, UI preview programs and build caches were cleaned after validation. Test source remains available for reproduction.

No new performance benchmark or long-duration test was conducted during release preparation. Earlier measurements and limitations are in `TEST_REPORT.md`. The code is not independently audited and executables are not Authenticode-signed. Source ZIP dependencies are restored using `bootstrap.ps1`.
