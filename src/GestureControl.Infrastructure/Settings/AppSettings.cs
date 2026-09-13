namespace GestureControl.Infrastructure.Settings;

public class AppSettings
{
    public int CameraIndex { get; set; } = 0;
    public int CameraWidth { get; set; } = 640;
    public int CameraHeight { get; set; } = 480;
    public int TargetFps { get; set; } = 30;
    public bool ShowOverlay { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public string ActiveProfileId { get; set; } = "global_default";
    public string LogsDirectory { get; set; } = "logs";
    public string ModelsDirectory { get; set; } = "models";
}
