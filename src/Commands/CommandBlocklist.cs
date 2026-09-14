namespace WindowsBootRepair.Commands
{
    public static class CommandBlocklist
    {
        private static readonly string[] DangerousPatterns = new[]
        {
            "clean",
            "format",
            "delete partition",
            "convert",
            "initialize",
            "new-partition",
            "remove-partition",
            "resize-partition",
            "clear-disk",
            "diskpart clean",
            "format /",
            "del /s",
            "rd /s"
        };

        public static bool ContainsDangerousPatterns(string args)
        {
            var lowerArgs = args.ToLower();
            return DangerousPatterns.Any(pattern => lowerArgs.Contains(pattern));
        }
    }
}
