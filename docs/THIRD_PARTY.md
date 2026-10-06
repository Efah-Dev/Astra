# Third-party dependencies

| Component | Version | Purpose | License / terms |
|---|---|---|---|
| BouncyCastle.Cryptography | 2.6.2 / net461 | Independent CPU secp256k1 and Keccak verification | Bouncy Castle MIT-style; original text in `licenses/` |
| NVIDIA CUDA NVRTC | 12.8.93 | CUDA source compilation and builtins | NVIDIA CUDA Toolkit EULA; original text in `licenses/` |
| NVIDIA driver | Installed by user; tested 610.74 | CUDA Driver API and PTX JIT | NVIDIA driver terms; driver not bundled |
| Microsoft .NET Framework | 4.8+ recommended; tested release 533509 | WinForms, C#, OS randomness and I/O | Windows component; Framework not redistributed |

Pinned official download sources and SHA-256 values:

- https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/2.6.2/bouncycastle.cryptography.2.6.2.nupkg
  `623936fb1fd171579c706390390711282898cf2c759a5167852e5ab82208f8cb`
- https://developer.download.nvidia.com/compute/cuda/redist/cuda_nvrtc/windows-x86_64/cuda_nvrtc-windows-x86_64-12.8.93-archive.zip
  `a63302a077f0248a743a1a7caa7dbd80d0fac56c6cfa9c41fa05fac9b7e5eda5`

The NVRTC archive checksum is also published in NVIDIA's `redistrib_12.8.1.json`. Only the needed NVRTC and builtins DLLs are in the portable package. NVRTC redistribution terms appear in the CUDA EULA's NVRTC supplement. Review the supplied license texts when redistributing.

EIP-55 public test vectors: https://github.com/ethereum/ercs/blob/master/ERCS/erc-55.md

Optional independent spreadsheet test dependency: Python with openpyxl (not bundled, not required to run Astra). The project owner has not selected a license for Astra's first-party code; third-party licenses do not license Astra's own code.
