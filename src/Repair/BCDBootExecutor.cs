using WindowsBootRepair.Commands;
using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Repair
{
    public static class BCDBootExecutor
    {
        public static RepairResult ExecuteBCDBoot(WindowsInstallation windows, EFIPartition efi)
        {
            var result = new RepairResult { ExecutedAt = DateTime.Now };

            try
            {
                string command = "bcdboot";
                string args = $"\"{windows.Path}\\Windows\" /s \"{efi.DriveLetter}:\" /f UEFI";

                BootRepairLogger.Log($"Executing: {command} {args}");

                var cmdResult = CommandExecutor.ExecuteCommand(command, args);

                result.CommandExitCode = cmdResult.ExitCode;
                result.CommandOutput = cmdResult.Output;
                result.CommandError = cmdResult.Error;
                result.Success = cmdResult.ExitCode == 0;

                if (!result.Success)
                {
                    result.ErrorMessage = $"BCDBOOT failed with exit code {cmdResult.ExitCode}";
                }

                BootRepairLogger.Log($"BCDBOOT exit code: {cmdResult.ExitCode}");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                BootRepairLogger.Log($"BCDBOOT execution error: {ex.Message}");
                return result;
            }
        }
    }
}
