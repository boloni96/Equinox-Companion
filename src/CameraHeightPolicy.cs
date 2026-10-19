namespace EquinoxCompanion;

// Restore a stable deviation while enabled; saved preferences change only through explicit UI edits.
internal sealed class CameraHeightPolicy
{
    private double? zeroSince;
    private float lastValue;
    public void Reset() => zeroSince = null;
    public static bool Valid(float value) => float.IsFinite(value) && value >= -3f && value <= 3f;
    public bool Restore(bool safe, float current, float preferred, double seconds)
    {
        if (!safe || !Valid(current) || !Valid(preferred) || MathF.Abs(current-preferred) < .0001f)
        { Reset(); return false; }
        if(zeroSince is not null&&MathF.Abs(lastValue-current)>.0001f)Reset();
        lastValue=current;zeroSince ??= seconds;
        if (seconds - zeroSince.Value < 1.5) return false;
        Reset();
        return true;
    }
}
