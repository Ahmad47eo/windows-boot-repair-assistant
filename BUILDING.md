# Building

## Windows

Install the .NET 8 SDK and run:

```powershell
dotnet restore
dotnet build BootRepairAssistant2.sln -c Release
dotnet test tests/BootRepairAssistant2.Tests -c Release
dotnet publish src/BootRepairAssistant2/BootRepairAssistant2.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Linux

Install the .NET 8 SDK. The WinForms project already sets
`EnableWindowsTargeting`, so the following works on Linux:

```bash
dotnet build BootRepairAssistant2.sln -c Release
dotnet test tests/BootRepairAssistant2.Tests
```

The `-p:EnableWindowsTargeting=true` property is only needed if building a
copy of the project without that csproj property:

```bash
dotnet build BootRepairAssistant2.sln -c Release -p:EnableWindowsTargeting=true
```

The Core library and tests target plain `net8.0` and run cross-platform. The
WinForms executable targets `net8.0-windows` and is intended to run on Windows.
