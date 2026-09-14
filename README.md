# Windows Boot Repair Assistant 2

Windows Boot Repair Assistant 2 is a safety-first WinPE utility for diagnosing
and, only when the evidence is unambiguous, repairing UEFI Windows boot files.
It is a .NET 8 WinForms application and should be run from an elevated WinPE
environment.

## Safety rules

- **Test Mode is enabled by default.** It performs the scan and backup planning
  but never runs `bcdboot` or changes mount points.
- Do not use Repair unless all diagnostic indicators are successful.
- The tool never guesses between firmware modes, Windows installations, or EFI
  partitions. Ambiguous results block repair.
- Backups are created before any real repair. The BCD is copied when present;
  a missing existing BCD is reported, not treated as a reason to invent one.
- A repair is reported as **SUCCESS only when the command exits successfully and
  required verification checks pass**. Optional boot files may result in a
  WARNING.

## Modes

- **Scan PC** runs the read-only diagnostic scan.
- **Diagnose Only** runs the same scan and displays the detailed report and
  plain-language problems.
- **Repair Boot** requires one valid Windows installation, one high-confidence
  EFI System Partition, UEFI firmware, and explicit confirmation by typing
  `REPAIR`. In Test Mode it simulates the exact command.
- **Verify Repair** checks the required and optional boot files and BCD size.

## Detection and repair

Firmware detection first reads the `PEFirmwareType` **registry value** under
`HKLM\SYSTEM\CurrentControlSet\Control` (`1` = BIOS, `2` = UEFI). A missing or
unexpected value falls back to `GetFirmwareType`; the tool never guesses.
Windows PE is identified from the `MiniNT` key and/or an `X:\` system root.

The repair command is equivalent to:

```text
bcdboot <WindowsDrive>:\Windows /s <EfiDrive>: /f UEFI
```

If the EFI volume has no letter, real mode can assign a free temporary letter
and removes it afterward. Test Mode only reports that this would be necessary.
Partition enumeration and inspection are read-only; mount-point changes are the
only volume changes made by the repair workflow.

## Backups, verification, and logging

Backups are selected in this order: the Windows installation's
`BootRepairAssistant2\Backups`, the executable directory, then `%TEMP%`.
Timestamped directories are never overwritten. A timestamped log is written
next to the executable, with a temporary-directory fallback. The View Log and
Copy Report actions include firmware, selected paths, command output, exit
codes, verification, and problems.

## WinPE usage and downloads

Build or publish the application, copy the resulting executable to WinPE, and
run it elevated. On GitHub, download `BootRepairAssistant2-win-x64` from the
successful **Actions** workflow artifact; the artifact is self-contained and
targets Windows x64.

## Limitations

This release targets UEFI Windows installations and x64 WinPE. It does not
repair legacy BIOS boot chains, modify partition tables, format volumes, or
choose among ambiguous candidates. Native volume metadata APIs are guarded for
Windows; Linux is supported for building and running the Core tests only.
