# Measured test report — 2026-10-06

Measurements below were recorded on the development machine, local time Asia/Shanghai. Results are not extrapolated to other models. Raw logs and generated wallets were removed during release cleanup; test source is retained for reproduction.

## Environment

- NVIDIA GeForce **RTX 5090 D v2**, 24,455 MiB reported VRAM, compute capability 12.0, WDDM.
- NVIDIA driver **610.74**, CUDA Driver API 13.3 (13030), NVRTC **12.8.93**.
- AMD Ryzen 7 9800X3D 8-Core, Windows 11 kernel 10.0.26100, .NET Framework release 533509.
- CPU cryptographic verification: BouncyCastle.Cryptography 2.6.2.
- Normal desktop use during tests. One sample showed approximately 93% GPU use, 2,484 MiB total VRAM use, 397.98 W and 57°C. This includes other desktop activity and is not exclusive program usage or average power.

## 30-second comparison

Recorded around 20:54–20:57. Each run used `--count 0 --seconds 30`, all legal repeated characters, no prefix. Batch is per lane; each group has 128 lanes. Rate is total candidates divided by measured search time, including reseeding, CPU verification and output, but excluding startup checks.

| Network and rule | Groups / batch | Candidates | Average M candidates/s | Verified/saved | Saved/s |
|---|---:|---:|---:|---:|---:|
| Ethereum, exact-case eight-repeat | 128 / 32 | 19,056,295,936 | 635.074 | 48 | ~1.60 |
| Polygon, exact-case eight-repeat | 128 / 32 | 18,953,011,200 | 631.630 | 50 | ~1.67 |
| TRON, exact-case eight-repeat | 128 / 32 | 10,124,525,568 | 337.413 | 0 | 0 |
| Polygon, exact-case eight-repeat | 256 / 64 | 34,043,068,416 | **1,134.450** | 70 | ~2.33 |
| Polygon, case-insensitive one-repeat (all addresses match) | 128 / 32 | 123,207,680 | 4.105 | 30,080 | ~1,002.7 |

Ethereum and Polygon used separate freshly seeded tasks and the same EVM core; no computation is counted twice. Match counts fluctuate randomly and should not be used as a throughput measurement. No TRON eight-repeat hit in 30 seconds is plausible at that difficulty; easier TRON rules and key/address pairs were checked by integration tests.

The default 256/64 configuration was approximately 80% faster than 128/32 for the measured Polygon difficult rule. This is not proof of a global optimum for other rules or devices. Easy rules are limited by one-delivery-per-group isolation, reseeding, CPU checks and output.

Reproduce with `tests/benchmark.ps1 -Seconds 30`.

## Reseeding and continuous output

Around 21:02–21:04: Polygon exact-case eight-repeat, 256/64, `--seconds 120 --reseed-seconds 2`. The short reseed interval deliberately exercised group replacement.

| Metric | Measurement |
|---|---:|
| Search duration | 120.007340 s |
| Candidates | 125,648,764,928 |
| Average candidate rate | 1,047.009 M/s |
| Verified and flushed wallets | 299 |
| Saved rate | 2.492/s |
| Independent seed samples | 15,594 |
| Last CUDA search call, including synchronization | 1.537 ms |
| Initialization and startup checks | 2.189900 s |

The run exited normally without a detected GPU/CPU mismatch or write error. Reproduce with `tests/soak.ps1 -Seconds 120 -ReseedSeconds 2`. Two minutes does not establish 24-hour or multi-day stability.

## Compilation and initialization

- NVRTC `vanity.exe --compile`: 4.590 seconds wall time including process startup in the earlier measurement.
- Each 30-second benchmark took approximately 32.22–32.32 seconds total, including startup.
- Extended self-test: 4.91 seconds in an earlier final run.
- PTX JIT caching affects startup; the driver cache was not cleared for measurement.

## Correctness coverage

The retained test sources exercise:

- Public private key 1: Ethereum and Polygon both `0x7E5F4552091A69125d5DfCb7b8C2659029395Bdf`; TRON `TMVQGm1qAQYVdetCeGRRkTWYYrLXuHK2HC`.
- Keccak-256 empty-string vector, official EIP-55 vectors and full-address checksum cases.
- Independent CUDA arithmetic, Keccak, SHA-256 and Base58 versus Bouncy Castle/.NET CPU computation.
- Startup samples with 32 public scalars; extended samples with 256, including the n−1 boundary.
- All 4,096 candidates per production batch for 3 networks × 2 case modes × 4 rules = 98,304 candidates, verifying keys, addresses, offsets, positive and negative matches, final lane state and offset continuity.
- Fixed prefix AND suffix, repeated suffixes, case rules, presets, custom sets, invalid input, overlap conflicts, unreachable TRON prefixes, folded-case deduplication and equal Ethereum/Polygon difficulty.
- Per-network count, labels, append, no-overwrite behavior and independent key/address validation.
- Cross-task/network/append samples without duplicates or adjacent scalars within 2^40. This sample check is not proof of randomness.
- Count/time limits, periodic reseeding, cancellation, accepted-versus-flushed count agreement and injected writer failure by closing the stream.
- Real Windows CTRL_C_EVENT: an earlier final TXT run saved 2,816 records and exited with code 0 in 38.6 ms after the signal.
- Independent XLSX parsing, leading zeros, volume ordering, ACLs and export failure handling. See [UI/export report](UI_EXPORT_UPDATE.md).

## Limitations

No physical RTX 5080, standard RTX 5090, other RTX 50 desktop/laptop, or Windows 10 measurement. No independent security audit, multi-day run, power-loss recovery certification, exhaustive disk-failure test or all-driver TDR validation. Search seeds are not persisted, so append starts a new independent search rather than resuming an old sequence. See [Security](SECURITY.md).
