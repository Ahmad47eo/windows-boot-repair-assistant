namespace BootRepairAssistant2.Core;

public interface IVolumeProvider
{
    IReadOnlyList<VolumeInfo> GetVolumes();
}
