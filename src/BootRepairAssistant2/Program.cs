using BootRepairAssistant2.Core;

namespace BootRepairAssistant2;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var fileSystem = new SystemFileSystem();
        var logger = new BootRepairLogger(fileSystem);
        Application.ThreadException += (_, e) => { logger.Log($"UI exception: {e.Exception}"); MessageBox.Show(e.Exception.Message, "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => logger.Log($"Unhandled exception: {e.ExceptionObject}");
        var registry = new WindowsRegistryReader();
        var firmware = new FirmwareDetector(registry, new WindowsFirmwareApi(), logger);
        var environment = new SystemEnvironmentInfo();
        var provider = new WindowsVolumeProvider();
        var winPe = new WinPeDetector(registry, environment);
        var windows = new WindowsInstallationFinder(provider, fileSystem, logger);
        var efi = new EfiPartitionFinder(provider, fileSystem, logger);
        var diagnostics = new Diagnostics(firmware, winPe, windows, efi, logger);
        var backup = new BackupManager(fileSystem, logger);
        var verifier = new Verifier(fileSystem);
        var repair = new RepairEngine(new ProcessRunner(), backup, verifier, diagnostics, firmware, fileSystem, logger);
        Application.Run(new MainForm(diagnostics, repair, verifier, logger));
    }
}
