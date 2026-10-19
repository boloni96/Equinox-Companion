using EquinoxCompanion;
internal static class CameraHeight194Tests
{
    internal static void Run()
    {
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS camera height " + name); }
        var p = new CameraHeightPolicy();
        Check(!p.Restore(true, 0, -.62f, 0), "wait for stable reset");
        Check(!p.Restore(true, 0, -.62f, 1), "do not apply while settling");
        Check(p.Restore(true, 0, -.62f, 1.5), "restore settled reset");
        Check(!p.Restore(true, -.62f, -.62f, 2), "no repeated write when correct");
        Check(!p.Restore(true, .4f, -.62f, 3), "wait for nonzero reset to settle");
        Check(p.Restore(true, .4f, -.62f, 4.5), "restore nonzero game reset while enabled");
        p.Restore(true, 0, -.62f, 5);
        Check(!p.Restore(false, 0, -.62f, 6), "special camera cannot restore");
        Check(!p.Restore(true, 0, -.62f, 7), "settle again after cutscene");
        p.Reset();
        Check(!p.Restore(true, 0, -.62f, 10), "new character or camera settles again");
        Check(!p.Restore(true, float.NaN, -.62f, 12), "reject invalid camera read");
        Check(!p.Restore(true, 0, float.PositiveInfinity, 14), "reject invalid saved value");
        Check(!p.Restore(true, 0, 4, 16), "reject out of range preference");
        Check(!p.Restore(true, 0, 0, 18), "zero preference never writes");
        var config = new Configuration();
        Check(!config.RememberCameraHeight, "disabled by default");
        config.RememberCameraHeight = true; config.PreferredCameraHeight = -.62056684f;
        var restored = System.Text.Json.JsonSerializer.Deserialize<Configuration>(System.Text.Json.JsonSerializer.Serialize(config))!;
        Check(restored.RememberCameraHeight && restored.PreferredCameraHeight == config.PreferredCameraHeight, "saved preference survives reload");
    }
}
