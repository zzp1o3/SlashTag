namespace SlashTag.Storage;

public static class AppPaths
{
    private const string FolderName = "SlashTag";
    private const string DatabaseFileName = "slashtag.db";
    private const string LegacyFolderName = "CircleLight";
    private const string LegacyDatabaseFileName = "circlelight.db";

    public static string DataDirectory => Path.Combine(LocalAppData, FolderName);

    public static string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>第一次用新名字启动时，把旧名字的数据目录迁移过来；新库已存在时不动。</summary>
    public static void MigrateLegacyData()
    {
        if (File.Exists(DatabasePath)) return;

        var oldDatabase = Path.Combine(LocalAppData, LegacyFolderName, LegacyDatabaseFileName);
        if (!File.Exists(oldDatabase)) return;

        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.Move(oldDatabase, DatabasePath);
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var source = oldDatabase + suffix;
                if (File.Exists(source)) File.Move(source, DatabasePath + suffix);
            }
        }
        catch (IOException)
        {
            // 旧进程还占着文件时，下次启动再迁移。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
}
