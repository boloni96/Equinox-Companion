namespace EquinoxCompanion;

// A zero reset must remain stable; never treat an intentional nonzero edit as a reset.
internal sealed class CameraHeightPolicy
{
    private double? zeroSince;
    public void Reset() => zeroSince = null;
    public static bool Valid(float value) => float.IsFinite(value) && value >= -3f && value <= 3f;
    public bool Restore(bool safe, float current, float preferred, double seconds)
    {
        if (!safe || !Valid(current) || !Valid(preferred) || MathF.Abs(current) > .0001f || MathF.Abs(preferred) < .0001f)
        { Reset(); return false; }
        zeroSince ??= seconds;
        if (seconds - zeroSince.Value < 1.5) return false;
        Reset();
        return true;
    }
}
