using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class FirmwareDetector
    {
        public static FirmwareInfo DetectFirmware()
        {
            // Stub - already defined in Core folder
            throw new NotImplementedException();
        }

        public static bool VerifyUEFIMode()
        {
            var firmware = DetectFirmware();
            return firmware.Mode == "UEFI";
        }
    }
}
