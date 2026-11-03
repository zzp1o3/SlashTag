namespace CircleLight.Storage;

public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CircleLight");

    public static string DatabasePath => Path.Combine(DataDirectory, "circlelight.db");
}
