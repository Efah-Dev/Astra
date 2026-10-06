# Security model and limitations

## Independent search groups

Each seed is 32 bytes from the Windows OS cryptographic random source through .NET `RandomNumberGenerator`. Zero and values greater than or equal to the secp256k1 group order are rejected. Sampling covers the full legal scalar range; there is no fixed, timestamp, 32-bit or 64-bit production seed. Random-source failures stop execution without a fallback PRNG.

Candidates within a group are `k = (seed + offset) mod n`. Only the public point `seed * G` is sent to the GPU. Each group has 128 lanes searching non-overlapping offsets. An atomic claim permits only one hit to leave a group. After CPU verification, the entire group is discarded and independently reseeded, including hits withheld because the global count limit was reached. Delivering multiple related private keys from a group is prohibited and cannot be enabled.

This prevents one delivered private key from revealing the offsets of another delivered key from the same seed. The guarantee depends on a functioning OS random source and an uncompromised host. Every launch, new network task and append task samples fresh randomness. Public self-test vectors have a separate test entry point. Unmatched groups are periodically reseeded and have a candidate cap of approximately 2^40.

## Verification and failure behavior

CUDA implements independent 8-by-32-bit field arithmetic, batch inversion, secp256k1 point addition, Keccak, SHA-256 and address encoding. CPU verification uses Bouncy Castle for secp256k1 and Keccak, .NET SHA-256, and separate encoding and matching code.

Startup checks compare all candidates in their test sample. Extended checks export every candidate from the production batch template and compare public keys, addresses, offsets and both true and false matches, including false negatives. Production hits must pass scalar-range, 64-byte public-key, address and match checks. Mismatches and CUDA, random-source or write failures stop the task. Production non-hits are not individually CPU-verified; runtime soft errors that cause missed matches cannot all be ruled out.

## Files, flushing and recovery

TXT, XLSX and intermediate files contain plaintext private keys. New files use no-overwrite semantics, exclusive handles, and a protected DACL permitting only the current Windows user and SYSTEM. ACL failures are fatal; existing reparse points are rejected. Use a local NTFS folder. ACL behavior on network shares or FAT/exFAT is not guaranteed. ACLs do not encrypt data or prevent administrators, SYSTEM, backups, offline disk access or malware running as the same user from reading it.

Do not use cloud-synced output folders. The GUI defaults to the Windows Desktop, which may be redirected or synced; choose another folder when necessary. Never commit generated wallets to a repository. Network labels are not key isolation: disclosure of a key can compromise assets controlled by that key on multiple networks.

The background queue is bounded at 1,024 records and calls `Flush(true)` each second. Excel intermediate records are flushed before the saved count advances. Workbooks are published every 5 seconds or 10,000 rows, flushed, renamed, and their corresponding intermediate data deleted. Normal stop drains the queue and completes export. TXT and XLSX are not a cross-file transaction; a failure can leave only one format complete.

Write failures stop search and report the error. Flushed `.xlsx.pending` / `.xlsx.writing` files are retained on export failure, but are not directly usable workbooks. There is no automatic crash-recovery tool. Records accepted but not yet flushed may be unrecoverable after an error. Use Ctrl+C, Stop and save, or normal GUI closure. Forced termination, closing CMD, power loss and driver resets do not guarantee complete output. Check the last TXT line before appending after an interrupted run.

## Sensitive memory

Console output and application logs never intentionally print private keys or seeds. Byte arrays, temporary encoding buffers, seeds and CUDA buffers are cleared on a best-effort basis. GPU state contains public points rather than seed private keys.

The managed runtime and third-party BigInteger/EC code can create internal copies that cannot be explicitly cleared. Garbage collection, paging, hibernation, crash dumps and debuggers may retain sensitive data. Locked memory and system dump controls are not implemented. No complete-erasure, local-administrator, malware or side-channel resistance claim is made.

## Scope of assurance

- No independent cryptographic/security audit, formal proof or professional penetration test.
- No 24-hour or multi-day stability certification.
- No correctness or performance guarantee for untested GPUs, drivers, overclocking or unstable hardware.
- Not every disk-full, filesystem failure, power-loss or driver-TDR path has been exercised.
- Windows, NVIDIA drivers, NVRTC and Bouncy Castle are trusted dependencies.
- Generation has no RPC, balance lookup, transaction or upload logic. Dependency downloads happen during development bootstrap; portable operation is offline.
