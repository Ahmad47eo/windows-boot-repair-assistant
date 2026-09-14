using System.Diagnostics;
using WindowsBootRepair.Logging;

namespace WindowsBootRepair.Commands
{
    public class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public static class CommandExecutor
    {
        public static CommandResult ExecuteCommand(string command, string args)
        {
            var result = new CommandResult();

            try
            {
                // Safety check: only allow specific commands
                if (!CommandAllowlist.IsCommandAllowed(command))
                {
                    BootRepairLogger.Log($"BLOCKED: Command not in allowlist: {command}");
                    result.ExitCode = -1;
                    result.Error = "Command not authorized";
                    return result;
                }

                // Safety check: block dangerous arguments
                if (CommandBlocklist.ContainsDangerousPatterns(args))
                {
                    BootRepairLogger.Log($"BLOCKED: Dangerous arguments detected: {args}");
                    result.ExitCode = -1;
                    result.Error = "Arguments contain dangerous patterns";
                    return result;
                }

                BootRepairLogger.Log($"Executing: {command} {args}");

                using (var process = new Process())
                {
                    process.StartInfo.FileName = command;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    result.Output = process.StandardOutput.ReadToEnd();
                    result.Error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    result.ExitCode = process.ExitCode;
                }

                BootRepairLogger.Log($"Command completed with exit code: {result.ExitCode}");
                return result;
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Command execution error: {ex.Message}");
                result.ExitCode = -1;
                result.Error = ex.Message;
                return result;
            }
        }
    }
}
