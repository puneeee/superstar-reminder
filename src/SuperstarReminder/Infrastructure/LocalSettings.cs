using System.Text.Json;
using SuperstarReminder.Domain;
namespace SuperstarReminder.Infrastructure;
public sealed class LocalSettings
{
    public ScreenCorner Corner { get; set; } = ScreenCorner.BottomRight;
    public double HeightFraction { get; set; } = OverlayPresentation.DefaultHeightFraction;
    public int PresentationVersion { get; set; } = 2;
    public string CharacterId { get; set; } = "rajinikanth-jailer";
    public bool StartAtLogin { get; set; }
    public bool Paused { get; set; }
    public static string DataDirectory => OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","SuperstarReminder")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperstarReminder");
    public static LocalSettings Load()
    {
        Directory.CreateDirectory(DataDirectory);
        try {
            var raw=File.ReadAllText(Path.Combine(DataDirectory,"settings.json"));
            var settings=JsonSerializer.Deserialize<LocalSettings>(raw)??new();
            using var json=JsonDocument.Parse(raw);
            // Upgrade only the previous stock size; preserve a user's deliberate custom sizing.
            if(!json.RootElement.TryGetProperty("PresentationVersion",out _) && Math.Abs(settings.HeightFraction-.18)<.001)
                settings.HeightFraction=OverlayPresentation.DefaultHeightFraction;
            settings.PresentationVersion=2;
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var path=Path.Combine(DataDirectory,"settings.json");
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
}
