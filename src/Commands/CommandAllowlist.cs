namespace WindowsBootRepair.Commands
{
    public static class CommandAllowlist
    {
        private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "bcdboot",
            "diskpart",
            "mountvol",
            "wmic",
            "powershell"
        };

        public static bool IsCommandAllowed(string command)
        {
            return AllowedCommands.Contains(System.IO.Path.GetFileNameWithoutExtension(command));
        }
    }
}
