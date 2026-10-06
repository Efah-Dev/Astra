# Astra v1.1.0

A local Windows CUDA address generator for **Ethereum, TRON and Polygon PoS**. Astra includes a compact .NET desktop interface and a command-line interface, with custom prefixes, fixed or repeated suffixes, and TXT / Excel export.

Private-key safety comes first. Each independently seeded search group delivers **at most one wallet**, then reseeds. Every GPU hit is independently verified on the CPU. No RPC, balance lookup, transaction submission, telemetry or address upload is needed.

**Output files contain plaintext private keys. Never share them, commit them to Git, or store them in a cloud-synced folder. This software has not undergone an independent security audit.**

## Download and run

Use `Astra-v1.1.0-en-US-Windows-x64.zip` from the repository's `releases/` directory or the corresponding GitHub release asset. Extract the entire archive before running:

- **`Astra.exe`** — graphical interface.
- **`vanity.exe`** — CMD interface; no arguments opens the interactive menu.

The source checkout keeps distributable ZIPs in `releases/`. An extracted portable archive places the executables next to this README. Published archives have SHA-256 file manifests; `releases/SHA256SUMS.txt` lists archive checksums.

Requirements: Windows 10/11 x64, .NET Framework 4.8 or newer, and a compatible NVIDIA CUDA driver. Windows 11 was tested. No CUDA Toolkit, Visual Studio, Microsoft Office, Python, account or online service is required to run the portable build. Keep the supplied DLLs and CUDA files beside the executables. Other GPU models and Windows 10 have not been tested on physical hardware.

## Graphical interface

1. Select Ethereum mainnet, TRON mainnet or **Polygon PoS mainnet (chain ID 137)**.
2. Enter an optional custom prefix, excluding the fixed `0x` or `T`.
3. Choose a fixed suffix or a repeated suffix. A prefix combines with either mode using AND.
4. Select a character preset or type a custom set such as `58aB`. Enable **Match case** for exact output case.
5. Choose a folder and TXT, Excel, or both. The default folder is the current Windows user's Desktop. Choose a different local folder if the Desktop is cloud-synced.
6. Set count and time limits (`0` = unlimited), then click **Start**. **Stop and save** or closing the window completes verification, flush and export before exit.

The fixed-size window follows Windows light/dark application settings. The top card displays GPU, VRAM, CUDA Driver API, compute capability, CPU and search configuration. Logs never display private keys or random seeds.

## CMD examples

```bat
REM TRON: fixed prefix and suffix
vanity.exe --chain tron --prefix M --suffix 8888 --count 100

REM Ethereum: repeated suffix, case-insensitive
vanity.exe --chain eth --suffix-repeat 8 --case insensitive --count 100

REM Polygon PoS: exact-case repeated suffix, continuous search
vanity.exe --chain polygon --suffix-repeat 8 --case sensitive --count 0

REM Polygon PoS: digits only, up to one day
vanity.exe --chain polygon --suffix-repeat 8 --repeat-chars digits --count 0 --seconds 86400

REM Custom prefix and suffix, export both formats to an existing folder
vanity.exe --chain polygon --prefix ab --suffix cd --count 10 --output-dir D:\Results --format both

REM Explicit append to the network's TXT summary file
vanity.exe --chain eth --suffix 888 --count 10 --output-dir D:\Results --format txt --append

vanity.exe --self-test --extended
vanity.exe --help
```

CLI defaults: `--count 1 --seconds 0 --case insensitive --format txt`; output goes to the current working directory. `--output-dir` accepts a folder; automatic filenames include the network and do not overwrite existing results. `--output PATH` is a legacy explicit TXT filename option. Folder append creates or appends to `eth_results.txt`, `tron_results.txt` or `polygon_results.txt`; explicit-file append requires an existing file. Every new task uses fresh random seeds, including append tasks.

Ctrl+C is the supported console stop path. Forced termination or power loss may lose unflushed results. Count and time limits stop on whichever is reached first; startup checks and final output completion can add wall-clock time.

## Matching and networks

| Network | Address | Prefix input | Character set |
|---|---|---|---|
| Ethereum | EIP-55 `0x…` | After `0x` | `0–9`, `a–f`, `A–F` |
| Polygon PoS, 137 | EIP-55 `0x…` | After `0x` | Same as Ethereum |
| TRON | Base58Check `T…` | After `T` | Base58, excluding `0 O I l` |

All three use secp256k1. Ethereum and Polygon share one Keccak-256 / EIP-55 search core; the same private key produces the same address. Chain ID does not participate in account derivation. The software generates ordinary accounts, not token contracts, and never counts the same calculation twice for two networks. Independent tasks do not reuse seeds across networks.

`--suffix-repeat N` searches all accepted repeated characters at once. `--repeat-chars` accepts `all`, `digits`, `letters`, `lower`, `upper`, or a custom set. Presets filter unsupported characters; invalid custom characters cause an error. Exact-case `aaaaaaaa` and `AAAAAAAA` qualify, but `aAaAaAaA` does not. Case-insensitive comparison accepts the mixed example while preserving the real output address. Nine repeated characters satisfy an eight-character rule. Counts apply to all accepted characters combined. Fixed and repeated suffix modes are mutually exclusive.

Difficulty accounts for overlapping prefix/suffix positions, distinct accepted targets and case rules. Ethereum and Polygon use the same calculation. EIP-55 checksum bits and TRON checksums use a random-hash probability model. ETA is an estimate, never a deadline. **M/s means million candidate addresses per second, not million saved matches.**

## Output and security

TXT and `.xlsx` include network, address and the full 64-character hexadecimal private key. Excel stores these as text, preserving leading zeros, with frozen headers and filters. Excel publishes a new workbook every 5 seconds or 10,000 rows and finishes remaining rows on normal stop. It does not modify old workbooks. TXT + Excel contains the same results in two formats and counts each wallet once.

OS cryptographic randomness covers the full legal scalar range. GPU search uses public-key offsets; only one result per independent group may be delivered. Matching groups are discarded and reseeded. Unmatched groups also reseed after the configured interval (default 600 seconds) or candidate cap. CPU verification uses Bouncy Castle 2.6.2. Mismatches, RNG errors, CUDA errors and write failures stop the task.

Output uses a bounded background queue and protected Windows ACLs for the current user and SYSTEM; permission failures are fatal. Flushed counts include Excel intermediate data. ACLs are not encryption. Managed memory and cryptographic-library internals prevent a guarantee of complete memory erasure. Read [Security](docs/SECURITY.md) and [UI and export behavior](docs/UI_EXPORT_UPDATE.md) before storing funds.

## Build from source

Run in PowerShell on Windows:

```powershell
.\bootstrap.ps1
.\build.ps1
.\package.ps1
```

Bootstrap downloads official dependencies and verifies pinned SHA-256 hashes. Build uses the Windows .NET Framework x64 C# compiler (`csc.exe`), BouncyCastle.Cryptography **2.6.2**, and NVIDIA NVRTC **12.8.93**. Source ZIPs exclude downloaded dependencies; bootstrap recreates them. Network access is needed for bootstrap only.

Build outputs go into `dist/`; packaging creates versioned portable and source ZIPs plus checksums in `releases/`. Runtime dependencies are bundled in the portable ZIP. NVRTC builds `sm_120` PTX; the NVIDIA driver compiles it to machine code. Other architectures can compile the bundled CUDA source via NVRTC. No performance claim is made for untested devices. Executables are not Authenticode-signed.

## Tests and measurements

```powershell
.\tests\run.ps1
.\tests\ui.ps1 -Theme light
.\tests\ui.ps1 -Theme dark
.\tests\benchmark.ps1 -Seconds 30
.\tests\soak.ps1 -Seconds 120 -ReseedSeconds 2
```

`tests/run.ps1` runs extended GPU/CPU checks, integration tests, export tests and a real Windows Ctrl+C harness. For independent XLSX validation, install Python with `openpyxl`, then run `python tests/verify-export.py <EXPORT_TEST_DIR>` using the directory printed by the export test. Python is only a test dependency. Generated test wallets must never be funded or distributed.

Measured on **RTX 5090 D v2**, 24,455 MiB, driver 610.74, Windows 11, Ryzen 7 9800X3D on 2026-10-06: a 30-second Polygon exact-case eight-repeat test reached **1,134.450 M candidates/s**, with 70 verified and saved results, using 256 groups and batch 64. This is not a measurement of a standard RTX 5090 or RTX 5080. See [Test report](docs/TEST_REPORT.md) for conditions, easy-rule throughput, initialization cost and limits. A two-minute soak is not evidence of multi-day reliability.

## Repository layout

```text
src/           C# application, CUDA kernel and self-tests
tests/         Reproducible test scripts and UI fixture
docs/          Security, measurements and behavior
licenses/      Original third-party license texts
releases/      Portable ZIP, source ZIP and SHA-256 checksums
release.json   Version and locale metadata
bootstrap.ps1  Pinned dependency download
build.ps1      Console, GUI and PTX build
package.ps1    Allowlisted release packaging
```

Build caches, test wallets, screenshots and raw test logs are excluded from this release. The test source remains available so checks can be repeated. Do not upload `dist/`, `.tools/`, `third_party/`, `artifacts/`, `test-output/` or generated wallet files. Publish the portable ZIP as a GitHub Release asset.

## License and third-party notices

Original Bouncy Castle and NVIDIA NVRTC license texts are in `licenses/`; dependency sources and versions are listed in [Third-party notices](docs/THIRD_PARTY.md). The project owner has not selected a license for Astra's first-party code. Availability of source alone does not grant an open-source license. Third-party components retain their own licenses.
