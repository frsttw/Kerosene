<div align="center">

<img src="docs/icon-preview.png" width="190" alt="Kerosene icon">

# KEROSENE

`created by @frstt` · [GitHub @frsttw](https://github.com/frsttw) · [frstt.dev](https://frstt.dev)

<br>

![Windows](https://img.shields.io/badge/Windows-10_%7C_11-B46BFF?style=for-the-badge&logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_Framework-4.8-CE7EFF?style=for-the-badge&logo=dotnet&logoColor=17121F)
![Version](https://img.shields.io/badge/version-1.0.2-EFAAFF?style=for-the-badge&logoColor=17121F)
![License](https://img.shields.io/badge/license-MIT-78EBAA?style=for-the-badge)

**Restore Windows responsiveness after demanding gaming sessions.**

</div>

---

## About

Kerosene is a portable Windows utility that analyzes system state, applies conservative adjustments, and closes automatically. Open the executable whenever you want to restore your computer's responsiveness.

It uses no internet connection, installs no services, does not close your programs, and does not delete files, caches, or personal data.

## What it does

```text
[01] Maps RAM and paging pressure
[02] Reapplies the power plan that is already active
[03] Normalizes known launchers and helper processes
[04] Preserves the cache or releases standby memory only under real pressure
[05] Validates the final state and closes automatically
```

- Measures available physical RAM and memory load.
- Reapplies only the current power plan, without machine-specific settings.
- Normalizes elevated priorities on known launchers.
- Trims the working set only for heavy helpers without an active window.
- Releases standby memory only when load reaches 80% or available memory falls below the adaptive reserve.
- Saves the latest report to `%LOCALAPPDATA%\Kerosene\last-run.log`.

## Safety

Kerosene is designed to behave predictably and reversibly:

- it does not close browsers, documents, games, or work applications;
- it does not delete temporary files or game caches;
- it does not make permanent Registry changes;
- it does not install services or background components;
- it does not force a different power plan;
- it does not perform aggressive memory cleaning.

## Use

Download or build `dist\Kerosene.exe` and run it. Windows will request administrator permission because some memory-management APIs require elevation.

The panel completes the process without interaction and closes automatically.

## Build

Requirements: Windows 10 or 11 with .NET Framework 4.x.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The executable is created at `dist\Kerosene.exe` with `AnyCPU` support, an embedded administrator manifest, the official icon, and product metadata.

## License and credits

Distributed under the MIT License.

**Kerosene — created by @frstt · [GitHub @frsttw](https://github.com/frsttw) · [frstt.dev](https://frstt.dev)**
