namespace EquinoxCompanion;
[Serializable]
public sealed class FloatingLauncherOptions
{
    public bool Enabled { get; set; } = true;
    public float X { get; set; } = 24;
    public float Y { get; set; } = 180;
    public float Opacity { get; set; } = 1;
    public bool Blur { get; set; }
    public bool Locked { get; set; }
}
