# Windows Boot Repair Assistant

A production-quality, safety-first Windows UEFI/EFI boot repair tool designed to diagnose and safely repair Windows boot problems in standard Windows and Windows PE (WinPE) environments.

## ⚠️ SAFETY-FIRST DESIGN

**This tool is extremely conservative by design.** It will never:

- Format a partition
- Delete a partition
- Wipe a disk
- Initialize a disk
- Change partition sizes
- Automatically choose a disk when multiple candidates exist
- Guess which Windows installation or EFI partition is correct
- Automatically run diskpart, format, or delete commands

The only intended repair operation is **explicitly confirmed Windows UEFI boot-file repair using bcdboot**.

### Before Any Modification

1. ✓ Exactly what was detected is shown
2. ✓ Exactly what will be changed is shown
3. ✓ The exact command is displayed
4. ✓ User must type `REPAIR` to confirm
5. ✓ UEFI check runs immediately before repair

## 🎯 Features

### 🔎 Diagnose Only
- Complete system scan with zero modifications
- Firmware mode detection (UEFI vs Legacy BIOS)
- Physical disk and partition inventory
- Windows installation detection
- EFI System Partition identification
- Boot configuration status
- Firmware boot-menu inspection
- Generates copyable diagnostic report

### 🛠️ Repair UEFI Boot
- Only enabled when ALL safety requirements pass
- Requires explicit UEFI firmware detection
- Requires explicit Windows installation selection
- Requires explicit EFI partition selection
- Automatic backup before repair
- Pre-repair UEFI verification
- Requires user confirmation (type `REPAIR`)
- Post-repair verification
- Never automatically reboots

### 🧪 Test Mode
- Simulates detection and repair decisions
- Shows exactly what WOULD be done
- Displays the exact bcdboot command that WOULD run
- Makes zero actual changes
- Perfect for troubleshooting and learning

### 📋 View Report
- Displays latest diagnostic report in clear format
- Shows firmware, disks, Windows installations, EFI partition, boot status
- Shows warnings and recommended actions

### 📄 Copy Report
- Copies diagnostic report to clipboard
- Easy to paste into ChatGPT or forums for troubleshooting

### 📜 View Log
- Displays repair log with timestamps
- Shows all detection results, commands executed, and outcomes
- Clear log option available

## 🖥️ System Requirements

- **OS**: Windows 10/11 (or Windows PE/WinPE)
- **Architecture**: Windows x64
- **Privileges**: Administrator (required for repair; diagnostics available without)
- **Firmware**: UEFI (repair requires UEFI; diagnostics show firmware mode)
- **Boot Mode**: UEFI mode (for WinPE)
- **Storage**: ~50 MB free space
- **.NET**: Self-contained; no external .NET installation needed

## 📥 Download & Installation

### Option 1: GitHub Releases
Visit the [Releases](https://github.com/Ahmad47eo/windows-boot-repair-assistant/releases) page and download `WindowsBootRepair.exe`.

### Option 2: GitHub Actions Artifacts
1. Go to [Actions](https://github.com/Ahmad47eo/windows-boot-repair-assistant/actions)
2. Select the latest successful build
3. Download the `WindowsBootRepair-Windows-x64.zip` artifact
4. Extract `WindowsBootRepair.exe`

### Portable - No Installation
Simply run `WindowsBootRepair.exe`. No installer, no dependencies, no internet required.

## 🚀 Quick Start

### On Standard Windows
1. Download `WindowsBootRepair.exe`
2. Right-click → **Run as administrator**
3. Select **🔎 DIAGNOSE ONLY** to scan your system
4. Review the report
5. If repair is needed and all requirements pass, select **🛠️ REPAIR UEFI BOOT**

### On Windows PE (WinPE)

#### Prerequisites
- Create a bootable Windows PE USB stick (e.g., using Ventoy)
- **Boot the USB in UEFI mode** (not Legacy/BIOS)
- Verify UEFI boot in BIOS/UEFI settings

#### Steps
1. Copy `WindowsBootRepair.exe` to the USB stick
2. Boot into WinPE in UEFI mode
3. Launch `WindowsBootRepair.exe`
4. Proceed as above

#### Important WinPE Notes
- **Must boot in UEFI mode** — Legacy/BIOS mode will disable repair
- Some GUI features may be limited in WinPE; console diagnostics always available
- Firmware boot-entry inspection may not be available in WinPE
- Network/internet not required

## 🔧 Usage Guide

### Diagnose Only (Safe)
1. Select **🔎 DIAGNOSE ONLY**
2. Wait for scan to complete
3. Review the report showing:
   - **Firmware mode** (UEFI or Legacy)
   - **Physical disks** (count, sizes, removable status)
   - **Windows installations** (drive, version, status)
   - **EFI System Partition** (location, status)
   - **Boot files** (present/missing, health status)
   - **Warnings & recommendations**

### Repair UEFI Boot (Requires Confirmation)

#### Option 1: Manual Repair
1. Select **🔎 DIAGNOSE ONLY** first
2. Review results
3. If confident, select **🛠️ REPAIR UEFI BOOT**
4. Verify the detected Windows installation
5. Confirm the detected EFI partition
6. Review the exact command that will run
7. **Type exactly**: `REPAIR` (case-sensitive)
8. Wait for completion
9. Verify success message
10. **Manually restart** when ready

#### Option 2: Test Mode First (Recommended)
1. Select **🧪 TEST MODE**
2. Confirm Windows installation and EFI partition
3. See exactly what command WOULD run
4. Review results without any changes
5. If satisfied, run **🛠️ REPAIR UEFI BOOT** with confidence

### View Report
- Select **📋 VIEW REPORT** to display the latest diagnostic scan
- Select **📄 COPY REPORT** to copy it to clipboard
- Paste into ChatGPT or support forum for help

### View Log
- Select **📜 VIEW LOG** to see repair history
- Shows all operations, commands, and outcomes
- Useful for troubleshooting

## ⚙️ What Happens During Repair

### Pre-Repair
1. ✓ UEFI firmware mode confirmed (registry check)
2. ✓ Windows installation validated
3. ✓ EFI System Partition located
4. ✓ Required boot files verified
5. ✓ Backup of existing BCD created (timestamped)
6. ✓ Final UEFI check performed
7. ✓ User confirmation required (`REPAIR`)

### Repair
```
bcdboot "<Windows Drive>:\Windows" /s "<EFI Drive>:" /f UEFI
```

This command:
- Scans the Windows System32\Boot folder
- Copies EFI boot files to the EFI System Partition
- Creates/repairs the BCD (Boot Configuration Data)
- Registers the Windows Boot Manager with firmware
- **Does NOT modify Windows files, partitions, or disk layout**

### Post-Repair
1. ✓ Command exit code verified (must be 0)
2. ✓ Expected EFI boot files verified
3. ✓ Confirmation message displayed
4. ✓ **No automatic reboot** — user chooses when to restart
5. ✓ Log entry recorded

## 🔍 How It Detects UEFI

The tool uses the Windows registry key:

```
HKLM\SYSTEM\CurrentControlSet\Control\PEFirmwareType
```

- **Value 2** = UEFI ✓ Repair enabled
- **Value 1** = Legacy BIOS ✗ Repair disabled
- **Cannot determine** = Stop, do not guess

### In Legacy/BIOS Mode
If booted in Legacy/BIOS mode, the tool displays:

> "This WinPE environment was booted in Legacy/BIOS mode. Restart and boot the USB in UEFI mode before attempting UEFI repair."

Repair is **completely disabled** until proper UEFI boot is confirmed.

## 📦 Backup & Recovery

### Automatic Backup
Before any repair, the application creates a backup:

```
<backup folder>/BCD_backup_<timestamp>.bak
```

### Where Backups Are Stored
- Default: Application folder
- Clearly marked in the log
- Never overwrites existing backups
- Timestamped for easy identification

### If You Need to Restore
Contact support with the backup file. The bcdboot operation can be reversed using the backed-up BCD if necessary.

## 🚫 Limitations

- **Partition/Disk Operations**: Not supported. Partitions must be manually created/resized with third-party tools.
- **MBR Systems**: Not supported. UEFI repair only. Legacy BIOS systems must use different tools.
- **Hardware Issues**: Cannot repair failing disks or firmware bugs. Diagnose-only mode helps identify issues.
- **Automatic Repair**: Never runs without explicit user confirmation.
- **Multiple Candidates**: Never guesses. User must explicitly select the correct Windows installation and EFI partition.
- **WinPE GUI**: Some GUI features may be limited in WinPE; diagnostics always work.

## 🛡️ Security & Command Allowlisting

The tool uses a **centralized command-execution layer** with strict allowlisting:

✅ **Allowed Commands**
- `bcdboot` (repair only)
- Safe read-only diagnostic commands (diskpart, mountvol, etc.)

❌ **Blocked Commands**
- `diskpart clean`
- `format`
- `delete partition`
- `clean`
- `convert`
- `initialize disk`
- Partition creation/resizing
- Any other destructive operations

The backend enforces this policy; the UI cannot bypass it.

## 🧪 Testing

### Unit Tests
The project includes comprehensive unit tests covering:
- UEFI detection logic
- Legacy firmware detection
- Unknown firmware handling
- Windows installation detection
- EFI partition detection
- BCDBOOT command construction
- Command allowlisting & blocking
- Confirmation requirements
- Repair simulation
- Post-repair verification

### Run Tests
```bash
dotnet test
```

### Test Coverage
- All safety-critical paths tested
- Command execution mocked (never actually runs commands)
- No real disk modifications during tests
- 90%+ code coverage

## 🏗️ Building from Source

### Requirements
- Windows 10/11 or later
- .NET 8 SDK or later
- Visual Studio 2022 or Visual Studio Code

### Build Instructions

#### Command Line
```bash
# Clone the repository
git clone https://github.com/Ahmad47eo/windows-boot-repair-assistant.git
cd windows-boot-repair-assistant

# Restore dependencies
dotnet restore

# Run tests
dotnet test

# Build self-contained release
dotnet publish -c Release -r win-x64 --self-contained
```

#### Output
```
bin/Release/net8.0-windows/win-x64/publish/WindowsBootRepair.exe
```

#### Visual Studio
1. Open `WindowsBootRepair.sln`
2. Select **Release** configuration
3. Select **x64** platform
4. Build → **Publish**
5. Select "Folder" profile
6. Choose output directory
7. Click **Publish**

## 🔄 GitHub Actions CI/CD

The project includes an automated GitHub Actions workflow that:

1. ✓ Runs on every push to `main`
2. ✓ Checks out the code
3. ✓ Installs .NET 8 SDK
4. ✓ Restores dependencies
5. ✓ Runs all unit tests
6. ✓ Builds Release configuration
7. ✓ Publishes self-contained Windows x64 executable
8. ✓ Creates ZIP archive with README
9. ✓ Uploads as GitHub Actions artifact
10. ✓ Fails clearly if tests or build fail

### Accessing Artifacts
1. Go to [Actions](https://github.com/Ahmad47eo/windows-boot-repair-assistant/actions)
2. Click the latest successful workflow run
3. Scroll down to "Artifacts"
4. Download `WindowsBootRepair-Windows-x64.zip`
5. Extract `WindowsBootRepair.exe`

### Build Badge
[![Build Status](https://github.com/Ahmad47eo/windows-boot-repair-assistant/actions/workflows/build.yml/badge.svg)](https://github.com/Ahmad47eo/windows-boot-repair-assistant/actions/workflows/build.yml)

## 📋 Project Structure

```
windows-boot-repair-assistant/
├── .github/
│   └── workflows/
│       └── build.yml                 # CI/CD pipeline
├── src/
│   ├── WindowsBootRepair.csproj     # Project file
│   ├── Program.cs                   # Entry point
│   ├── App.xaml                     # WPF application
│   ├── App.xaml.cs
│   ├── MainWindow.xaml              # Main UI
│   ├── MainWindow.xaml.cs
│   ├── Core/
│   │   ├── FirmwareDetector.cs      # UEFI/Legacy detection
│   │   ├── DiskScanner.cs           # Disk/partition detection
│   │   ├── WindowsInstallationFinder.cs  # Windows detection
│   │   ├── EFIPartitionFinder.cs    # EFI partition detection
│   │   ├── BootDiagnostics.cs       # Boot file/BCD checks
│   │   └── FirmwareBootMenuChecker.cs   # Firmware entry checks
│   ├── Repair/
│   │   ├── RepairEngine.cs          # Main repair orchestrator
│   │   ├── BCDBootExecutor.cs       # bcdboot command execution
│   │   ├── BackupManager.cs         # BCD backup creation
│   │   └── PostRepairVerifier.cs    # Verification after repair
│   ├── Commands/
│   │   ├── CommandExecutor.cs       # Central command execution
│   │   ├── CommandAllowlist.cs      # Safety allowlist
│   │   └── CommandBlocklist.cs      # Blocked commands
│   ├── UI/
│   │   ├── DialogHelper.cs          # User prompts
│   │   ├── ReportGenerator.cs       # Diagnostic report
│   │   ├── LogDisplay.cs            # Log viewer
│   │   └── StatusIndicators.cs      # UI status helpers
│   ├── Logging/
│   │   └── BootRepairLogger.cs      # Logging system
│   └── Models/
│       ├── FirmwareInfo.cs
│       ├── DiskInfo.cs
│       ├── PartitionInfo.cs
│       ├── WindowsInstallation.cs
│       ├── EFIPartition.cs
│       ├── DiagnosticReport.cs
│       └── RepairResult.cs
├── tests/
│   ├── WindowsBootRepair.Tests.csproj
│   ├── Core/
│   │   ├── FirmwareDetectorTests.cs
│   │   ├── DiskScannerTests.cs
│   │   ├── WindowsInstallationFinderTests.cs
│   │   ├── EFIPartitionFinderTests.cs
│   │   └── BootDiagnosticsTests.cs
│   ├── Repair/
│   │   ├── RepairEngineTests.cs
│   │   ├── BCDBootExecutorTests.cs
│   │   └── PostRepairVerifierTests.cs
│   ├── Commands/
│   │   ├── CommandExecutorTests.cs
│   │   ├── CommandAllowlistTests.cs
│   │   └── CommandBlocklistTests.cs
│   └── Integration/
│       ├── DiagnoseOnlyTests.cs
│       ├── RepairSimulationTests.cs
│       └── TestModeTests.cs
├── WindowsBootRepair.sln             # Solution file
├── README.md                         # This file
├── LICENSE                          # MIT License
└── .gitignore                       # Git ignore rules
```

## 🧮 Example Diagnostic Report

```
═══════════════════════════════════════════════════════════════
        WINDOWS BOOT REPAIR ASSISTANT - DIAGNOSTIC REPORT
═══════════════════════════════════════════════════════════════

Generated: 2024-01-15 14:32:18
Application Version: 1.0.0

───────────────────────────────────────────────────────────────
FIRMWARE DETECTION
───────────────────────────────────────────────────────────────
Status: 🟢 UEFI Detected
Confidence: Very High (Registry: PEFirmwareType = 2)

───────────────────────────────────────────────────────────────
PHYSICAL DISKS
───────────────────────────────────────────────────────────────
Disk 0: C: (931 GB) [Internal NVME SSD]
Disk 1: E: (465 GB) [Internal HDD]
Disk 2: F: (64 GB) [Removable USB]

───────────────────────────────────────────────────────────────
WINDOWS INSTALLATIONS FOUND
───────────────────────────────────────────────────────────────
1. C:\Windows
   Version: Windows 11 (Build 23H2)
   System32\winload.efi: ✓ Present
   config\SYSTEM: ✓ Present
   Status: 🟢 Valid Windows Installation

───────────────────────────────────────────────────────────────
EFI SYSTEM PARTITION
───────────────────────────────────────────────────────────────
Status: 🟢 Detected
Disk: 0
Partition: 1
Drive Letter: Not assigned (can be temporarily mounted)
Filesystem: FAT32
Size: 260 MB
Partition Type: EFI System Partition (C12A7328-F81F-11D2-BA4B-00A0C93EC93B)

───────────────────────────────────────────────────────────────
BOOT CONFIGURATION
───────────────────────────────────────────────────────────────
EFI\Microsoft\Boot folder: 🟡 May need repair
Boot files (bootmgfw.efi, etc.): 🟡 Not all files present
BCD Database: 🟡 Offline or corrupted
Boot Manager Entry: 🔴 Not detected in firmware

───────────────────────────────────────────────────────────────
REMOVABLE MEDIA
───────────────────────────────────────────────────────────────
USB Disk F: 64 GB Ventoy
Note: Ventoy detected. Do not select this as Windows installation.

───────────────────────────────────────────────────────────────
WARNINGS & RECOMMENDATIONS
───────────────────────────────────────────────────────────────
⚠️ Boot files are missing or incomplete in the EFI partition.
⚠️ BCD database appears offline or inaccessible.
⚠️ Firmware boot entry for Windows Boot Manager not detected.

RECOMMENDED ACTION:
→ Select 🛠️ REPAIR UEFI BOOT to rebuild boot configuration.
→ This will copy boot files to the EFI partition and recreate the BCD.
→ Review the exact command before confirming.

═══════════════════════════════════════════════════════════════
```

## 📝 Log File

Logs are written to `BootRepair.log` in the application directory:

```
[2024-01-15 14:32:18] Application started
[2024-01-15 14:32:19] Firmware detection: UEFI (PEFirmwareType = 2)
[2024-01-15 14:32:20] Scanning physical disks...
[2024-01-15 14:32:22] Found 3 disks
[2024-01-15 14:32:23] Disk 0: 931 GB (Internal)
[2024-01-15 14:32:23] Disk 1: 465 GB (Internal)
[2024-01-15 14:32:23] Disk 2: 64 GB (Removable - Ventoy)
[2024-01-15 14:32:24] Searching for Windows installations...
[2024-01-15 14:32:26] Found Windows installation at C:\Windows
[2024-01-15 14:32:26] Windows 11 Build 23H2 detected
[2024-01-15 14:32:27] Locating EFI System Partition...
[2024-01-15 14:32:28] EFI Partition: Disk 0, Partition 1, 260 MB
[2024-01-15 14:32:29] Performing diagnostics complete
[2024-01-15 14:32:30] Report generated and displayed
[2024-01-15 14:33:45] User selected: Repair UEFI Boot
[2024-01-15 14:33:46] User confirmed Windows installation: C:\Windows
[2024-01-15 14:33:47] User confirmed EFI partition: Disk 0, Partition 1
[2024-01-15 14:33:48] Creating BCD backup...
[2024-01-15 14:33:49] Backup created: BCD_backup_20240115_143349.bak
[2024-01-15 14:33:50] Re-verifying UEFI mode before repair...
[2024-01-15 14:33:50] UEFI mode confirmed
[2024-01-15 14:33:51] Waiting for user confirmation (type REPAIR)...
[2024-01-15 14:33:58] User typed: REPAIR
[2024-01-15 14:33:59] Executing: bcdboot "C:\Windows" /s "E:" /f UEFI
[2024-01-15 14:34:12] Command completed with exit code 0
[2024-01-15 14:34:12] Post-repair verification...
[2024-01-15 14:34:13] Verifying boot files in EFI partition...
[2024-01-15 14:34:14] ✓ EFI\Microsoft\Boot\bootmgfw.efi found
[2024-01-15 14:34:14] ✓ EFI\Microsoft\Boot\BCD found
[2024-01-15 14:34:15] Repair completed successfully
```

## 🤝 Contributing

Contributions are welcome but **must maintain safety-first design**:

1. **Never bypass safety checks**
2. **Never add destructive operations without explicit confirmation**
3. **Never guess user intent**
4. **Always add unit tests**
5. **Always test with mock data (no real disk operations)**
6. **Clearly document any new features**

See CONTRIBUTING.md for detailed guidelines.

## 📞 Support

- **GitHub Issues**: [Report bugs or request features](https://github.com/Ahmad47eo/windows-boot-repair-assistant/issues)
- **Diagnostics**: Use "Copy Report" to share with support
- **Logs**: Share `BootRepair.log` for troubleshooting

## ⚖️ License

MIT License — See LICENSE file for details.

## 📚 Additional Resources

- [Windows Boot Manager Documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/boot-and-uefi)
- [BCDBOOT Command Reference](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/bcdboot-command-line-options)
- [Windows PE Technical Reference](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/winpe-intro)
- [UEFI Specification](https://uefi.org/specifications)

## ⚠️ Disclaimer

**Use at your own risk.** While this tool is designed to be safe and conservative:

- **No guarantee of repair**: Hardware issues or firmware bugs may prevent successful repair.
- **Data safety**: Always back up important data before attempting repairs.
- **Professional support**: For critical systems, consult professional IT support.
- **Test mode first**: Use "Test Mode" before attempting actual repair.
- **Read logs carefully**: Review all logs and messages before proceeding.

This tool diagnoses and attempts to repair UEFI boot problems, but it is not a substitute for professional data recovery or repair services.

---

**Ready to diagnose your Windows boot?** Download the latest release and run `WindowsBootRepair.exe` as administrator.
