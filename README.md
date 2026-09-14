# Windows Boot Repair Assistant 2

Windows Boot Repair Assistant 2 is a safety-first Windows PE utility for
diagnosing UEFI Windows boot problems and, only when the evidence is
unambiguous, rebuilding the boot files with `bcdboot`. The application is
written in C# with .NET 8 and WinForms. It is intended for elevated use in a
Windows x64 WinPE environment.

## Safety rules

- Test Mode is enabled by default.
- Diagnostics are read-only. They do not format volumes, edit partition
  tables, or write boot files.
- Repair is blocked unless the firmware is UEFI, exactly one valid Windows
  installation is found, and exactly one sufficiently trustworthy EFI System
  Partition is selected.
- Ambiguous firmware, Windows, or EFI results are reported instead of guessed.
- A backup is created before a real repair. Test Mode also creates the backup
  because copying existing files is harmless and provides a realistic preview.
- If an EFI volume has no drive letter, real repair temporarily assigns a free
  letter and removes it in a `finally` cleanup step.
- The application reports SUCCESS only when `bcdboot` succeeds and required
  verification checks pass.

## Modes

### Test Mode

Test Mode is selected by default. It performs diagnostics and creates a
timestamped backup, but it never starts `bcdboot` and never changes volume
mount points. The output describes the command that real repair would run.

### Diagnose Only

Diagnose Only performs the same read-only scan as Scan PC and displays the
detailed report, detection evidence, problems, and repair-blocking reasons.

### Repair

Repair Boot is enabled only after a successful unambiguous scan. The
confirmation dialog shows the detected firmware, Windows path, EFI volume,
planned command, and backup location. The operator must type `REPAIR`
case-sensitively before the operation can begin. Test Mode simulates this
operation; unchecked Test Mode runs the command after creating the backup.

## Status panel

- Green means the condition is suitable for the corresponding operation.
- Yellow means information is incomplete or optional verification is missing.
- Red means a required condition failed and repair cannot safely continue.
- Grey means the condition is unknown and the tool does not guess.

The Environment row explicitly reports either `Windows PE detected` or
`Windows PE not detected`, followed by the evidence. The tool checks the
`MiniNT` registry key and whether `SystemRoot` begins with `X:\`.

## How UEFI is detected

The first firmware check reads the registry **value** named `PEFirmwareType`
under:

```text
HKLM\SYSTEM\CurrentControlSet\Control
```

This is a value, not a subkey. The meanings are:

- `1` means Legacy BIOS.
- `2` means UEFI.
- Missing or unexpected values mean unknown.

When the registry value is missing or unexpected, the tool falls back to the
Windows `GetFirmwareType` API. If both sources fail, the result remains
unknown. The application never infers firmware mode from a drive letter or
from the presence of an EFI-looking directory.

## Windows installation detection

The scanner examines available volume letters and does not assume that
Windows is installed on `C:`. It skips the WinPE `X:` ramdisk. Required
markers are:

- `Windows\System32` directory.
- `Windows\System32\ntoskrnl.exe`.
- `Windows\System32\config\SYSTEM`.
- `Windows\System32\winload.efi`.

Additional markers such as `Windows\explorer.exe` and
`Windows\Boot\EFI\bootmgfw.efi` provide useful evidence but do not replace
the required markers.

## EFI detection scoring

Each volume receives an evidence score:

- GPT EFI System Partition type: +50.
- FAT or FAT32 filesystem: +20.
- Size from 32 MB through 2 GB: +10.
- `EFI\Microsoft\Boot` directory: +15.
- `EFI\Boot\bootx64.efi`: +5.
- NTFS: -100.
- Selected Windows volume: -100.
- Larger than 4 GB: -50.

Only candidates scoring at least 30 are considered. A tie between the best
candidates blocks repair. A drive letter alone is never sufficient evidence.

## Backup location and naming

The tool tries backup locations in this order:

1. `<WindowsDrive>:\BootRepairAssistant2\Backups`.
2. A `Backups` directory beside the executable.
3. `%TEMP%\BootRepairAssistant2\Backups`.

The first writable location is used. Each backup is placed in a
`yyyyMMdd_HHmmss` directory; if that directory already exists, `_2`, `_3`,
and subsequent suffixes are used rather than overwriting it. Existing
`EFI\Microsoft\Boot\BCD` and matching `BCD.LOG*` files are copied when
available. A `backup-info.txt` file records the scan context. A missing BCD
is reported as a note.

## Repair command

For a lettered Windows and EFI volume, the command is:

```text
bcdboot <WindowsDrive>:\Windows /s <EfiDrive>: /f UEFI
```

The executable is resolved from
`<SystemRoot>\System32\bcdboot.exe` when available, with `bcdboot` as the
fallback. Arguments are passed without a shell. When the EFI volume has no
letter, Test Mode explains that real repair would assign a free temporary
letter before running the command.

## Verification and outcomes

After real repair, the tool checks required files
`EFI\Microsoft\Boot\bootmgfw.efi` and `EFI\Microsoft\Boot\BCD`, and requires
the BCD file to be non-empty. It also checks optional
`EFI\Microsoft\Boot\bootmgr.efi` and `EFI\Boot\bootx64.efi`.

- SUCCESS means the command exited with code 0 and all required checks passed.
- WARNING means the command exited with code 0 but an optional check failed.
- FAILED means the command failed, was blocked, or a required check failed.
- NOT RUN means Test Mode or cancellation prevented command execution.

SUCCESS is never reported merely because `bcdboot` returned zero; verification
must also pass.

## Logging and Copy Report

The in-memory logger records timestamped operation lines and writes a log
named `BootRepairAssistant2_<yyyyMMdd_HHmmss>.log` beside the executable, with
a temporary-directory fallback. View Log displays only those log lines.
Copy Report copies the complete report, including diagnostics, selected
volumes, planned or executed command, command output, exit code, verification,
and repair summary. Clipboard failures are reported in the output area.

## Cancel semantics

Cancel remains enabled while an operation is running. Cancellation is honored
between diagnostic, backup, mount, command, and verification steps. A
`bcdboot` process that has already started is allowed to finish; cancellation
is not used to kill it. Temporary EFI drive letters are removed afterward.

## Using the application in WinPE

1. Build or download the Windows x64 executable.
2. Copy the executable to a WinPE USB drive.
3. Boot the target computer in UEFI mode.
4. Open an elevated command prompt or launch the application with
   administrator rights.
5. Run Scan PC or Diagnose Only and review every status.
6. Use Test Mode first. Use real Repair only when the findings are clear and
   the backup location is acceptable.

## Downloading from Actions

Each push and pull request can produce a Windows x64 Actions artifact named
`BootRepairAssistant2-win-x64`. Download the artifact from the successful
workflow run, extract it, and copy the self-contained executable to WinPE.

## Building

See [BUILDING.md](BUILDING.md) for SDK requirements, Linux build commands,
tests, and Windows self-contained publishing instructions. The Core library
and tests target plain `net8.0`, so diagnostics and safety logic can be tested
on Linux.

## Limitations

This release targets UEFI Windows installations and x64 WinPE. It does not
repair Legacy BIOS boot chains, format volumes, modify partition tables,
choose among ambiguous candidates, or replace missing Windows system files.
Native volume and firmware APIs are Windows-only; Linux support is for
building and running the cross-platform test suite.

## License

See [LICENSE](LICENSE).
