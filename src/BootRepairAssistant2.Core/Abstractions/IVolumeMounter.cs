namespace BootRepairAssistant2.Core;

public interface IVolumeMounter
{
    string? AssignLetter(string volumeGuidPath);

    void RemoveLetter(string letter);
}
