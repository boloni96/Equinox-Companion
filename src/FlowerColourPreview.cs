namespace EquinoxCompanion;

// Display-only rare-colour illustrations. This is neither an outcome nor a probability table.
public static class FlowerColourPreview
{
    public static string At(double elapsedSeconds) => ((int)(Math.Max(0,elapsedSeconds)%3)) switch {0=>"white",1=>"black",_=>"rainbow"};
}
