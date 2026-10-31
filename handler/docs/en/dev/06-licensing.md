# Licensing

## The project itself — MIT

The handler code is released under the **MIT** license — the full text is in [LICENSE.txt](../../LICENSE.txt), `Copyright (c) 2026 Sophron Ragozin`: you are free to use, modify and distribute it as long as the copyright notice is retained.

## The Moza SDK is not MIT

The Moza SDK (the native `MOZA_API_C.dll` / `MOZA_SDK.dll`, the `MOZA_API_CSharp.dll` C# wrapper and the utilities under `sdk/`) carries a **separate Moza Racing license**, not MIT: <https://mozaracing.com/pages/sdk>. The `sdk/Licenses/` folder holds the licenses of the SDK's bundled components (OpenSSL, libcoap, nanaCbor).

**What this means:**

- **for users** — the project itself is free (MIT), but the SDK is distributed under Moza Racing's terms; it is not part of this repository's MIT grant. Every user downloads the SDK from the Moza website themselves and accepts its license;
- **for contributors** — only the project's code (MIT) can be contributed. The SDK's rules (redistribution, modification, shipping binaries in your own builds) are defined by Moza Racing, not by this license: always check the SDK license before any operation involving it.

## Other dependencies

| Dependency | License | Where it is used |
|---|---|---|
| Svg.NET (`Svg` 3.4.7) | MS-PL | rasterizing the SVG icons of the About dialog (a NuGet package, pulled in automatically) |
| ExCSS | MIT | the HTML/CSS parser named in the project specification; the package is not referenced in the current tree |
| Moza SDK | Moza Racing | wheel-base control (see above) |

---
[← Back to contents](../../README.md)
