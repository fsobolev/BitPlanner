using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

[JsonSourceGenerationOptions(AllowTrailingCommas = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Dictionary<ulong, CraftingItem>))]
[JsonSerializable(typeof(CraftingItem))]
[JsonSerializable(typeof(ConsumedItem))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(List<TravelerData>))]
[JsonSerializable(typeof(TravelerData))]
[JsonSerializable(typeof(TravelerTask))]
public partial class GameDataContext : JsonSerializerContext
{
}

public sealed class GameData
{
    private static GameData _instance;

    public static GameData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameData();
            }
            return _instance;
        }
    }

    public DateOnly Version { get; private set; } = new();
    public Dictionary<ulong, CraftingItem> CraftingItems { get; private set; }
    public List<TravelerData> Travelers { get; private set; }

    private GameData()
    {
    }

    public void Load(bool forceBuiltIn = false)
    {
        var pathPrefix = "res:/";
        using var builtInDataVersionFile = FileAccess.Open("res://data_version.txt", FileAccess.ModeFlags.Read);
        Version = DateOnly.Parse(builtInDataVersionFile.GetAsText(), CultureInfo.InvariantCulture);
        if (!forceBuiltIn && FileAccess.FileExists("user://data/data_version.txt"))
        {
            using var userDataVersionFile = FileAccess.Open("user://data/data_version.txt", FileAccess.ModeFlags.Read);
            var userDataVersion = DateOnly.Parse(userDataVersionFile.GetAsText(), CultureInfo.InvariantCulture);
            if (userDataVersion > Version)
            {
                pathPrefix = "user://data";
                Version = userDataVersion;
            }
        }

        using var craftingDataFile = FileAccess.Open($"{pathPrefix}/crafting_data.json", FileAccess.ModeFlags.Read);
        using var travelersDataFile = FileAccess.Open($"{pathPrefix}/travelers_data.json", FileAccess.ModeFlags.Read);
        CraftingItems = JsonSerializer.Deserialize(craftingDataFile.GetAsText(), GameDataContext.Default.DictionaryUInt64CraftingItem);
        Travelers = JsonSerializer.Deserialize(travelersDataFile.GetAsText(), GameDataContext.Default.ListTravelerData);
    }
}