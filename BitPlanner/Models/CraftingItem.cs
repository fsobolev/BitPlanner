using System;
using System.Collections.Generic;

public class CraftingItem
{
    public string Name { get; set; }
    public int Tier { get; set; }
    public int Rarity { get; set; }
    public string Icon { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
    public int ExtractionSkill { get; set; } = -1;
    public bool Craftable => Recipes.Count > 0;
    public string GenericName
    {
        get
        {
            foreach (var prefix in new string[]{
                "Rough", "Simple", "Sturdy", "Fine", "Exquisite", "Peerless", "Ornate", "Pristine", "Magnificent", "Flawless",
                "Ferralith", "Pyrelite", "Emarium", "Elenvar", "Luminite", "Rathium", "Aurumite", "Celestium", "Umbracite", "Astralite",
                "Automata's", "Basic", "Infused", "Magnificient"
            })
            {
                var index = Name.IndexOf(prefix);
                if (index > -1)
                {
                    return Name.Remove(index, prefix.Length + 1);
                }
            }
            return Name;
        }
    }
}