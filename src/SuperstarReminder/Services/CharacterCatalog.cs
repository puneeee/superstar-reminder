using System.Text.Json;
using SuperstarReminder.Domain;
namespace SuperstarReminder.Services;
public sealed class CharacterCatalog : ICharacterCatalog
{
    private readonly List<CharacterDefinition> _characters=[];
    public CharacterCatalog()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Assets","Characters");
        if(!Directory.Exists(root))return;
        foreach(var path in Directory.EnumerateFiles(root,"character.json",SearchOption.AllDirectories))
        {
            try
            {
                using var json=JsonDocument.Parse(File.ReadAllText(path));var e=json.RootElement;
                var height=e.GetProperty("defaultHeightFraction").GetDouble();
                _characters.Add(new(e.GetProperty("id").GetString()!,e.GetProperty("displayName").GetString()!,path,Enum.GetValues<CharacterAction>().ToHashSet(),"top-center",Math.Clamp(height,.18,.40)));
            }
            catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException){}
        }
    }
    public IReadOnlyCollection<CharacterDefinition> All=>_characters;
    public CharacterDefinition Default=>_characters.FirstOrDefault()??throw new InvalidOperationException("No valid character package was installed.");
    public static string? FramePath(CharacterDefinition character,string relative)
    {
        var root=Path.GetDirectoryName(Path.GetFullPath(character.ManifestPath))!+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(root,relative));
        return path.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)&&File.Exists(path)?path:null;
    }
}
