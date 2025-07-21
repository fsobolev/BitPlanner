using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class RecipeTab : VBoxContainer
{
    private class ItemMetadata
    {
        public ulong Id;
        public HashSet<ulong> ShownIds;
        public uint RecipeIndex;
        public uint MinQuantity;
        public uint MaxQuantity;

        public Godot.Collections.Array<Variant> ToGodotArray()
        {
            return [
                Id,
                new Godot.Collections.Array(ShownIds.Select(i => Variant.CreateFrom(i))),
                RecipeIndex,
                MinQuantity,
                MaxQuantity
            ];
        }

        public static ItemMetadata FromGodotArray(Godot.Collections.Array array)
        {
            return new()
            {
                Id = array[0].AsUInt64(),
                ShownIds = new(array[1].AsGodotArray().Select(i => i.AsUInt64())),
                RecipeIndex = array[2].AsUInt32(),
                MinQuantity = array[3].AsUInt32(),
                MaxQuantity = array[4].AsUInt32()
            };
        }
    }

    private readonly GameData _data = GameData.Instance;
    private bool _allCollapsed = Config.CollapseTreesByDefault;
    private Tree _recipeTree;
    private TextureRect _recipeIcon;
    private Label _recipeName;
    private Label _recipeTier;
    private Label _recipeRarity;
    private TextureRect _skillIcon;
    private AtlasTexture _skillIconTexture;
    private Label _skillLabel;
    private OptionButton _recipeSelection;
    private SpinBox _quantitySelection;
    private Texture2D _errorIcon;
    private PopupPanel _recipeLoopPopup;
    private Label _recipeLoopLabel;

    public bool AllCollapsed
    {
        get => _allCollapsed;

        set
        {
            _allCollapsed = value;
            foreach (var item in _recipeTree.GetRoot().GetChildren())
            {
                CollapseItem(item);
            }
        }
    }

    public override void _Ready()
    {
        var recipeHeader = GetNode<HBoxContainer>("MarginContainer/Header");

        _recipeIcon = recipeHeader.GetNode<TextureRect>("MarginContainer/Icon");
        _recipeName = recipeHeader.GetNode<Label>("VBoxContainer/Name");
        _recipeTier = recipeHeader.GetNode<Label>("VBoxContainer/Tier");
        _recipeRarity = recipeHeader.GetNode<Label>("VBoxContainer/Rarity");

        _skillIcon = recipeHeader.GetNode<TextureRect>("VBoxContainer2/HBoxContainer/SkillIcon");
        _skillIconTexture = _skillIcon.Texture as AtlasTexture;
        _skillLabel = recipeHeader.GetNode<Label>("VBoxContainer2/HBoxContainer/SkillLabel");
        _recipeSelection = recipeHeader.GetNode<OptionButton>("VBoxContainer2/HBoxContainer/RecipeSelection");
        _recipeSelection.ItemSelected += OnRecipeChanged;

        _quantitySelection = recipeHeader.GetNode<SpinBox>("VBoxContainer2/HBoxContainer2/Quantity");
        _quantitySelection.ValueChanged += OnQuantityChanged;

        _recipeTree = GetNode<Tree>("RecipeTree");
        _recipeTree.SetColumnCustomMinimumWidth(1, 86);
        _recipeTree.SetColumnExpand(1, false);
        _recipeTree.SetColumnExpandRatio(0, (int)Math.Round(10 / Config.Scale * 2));
        _recipeTree.SetColumnCustomMinimumWidth(2, 98);
        _recipeTree.ItemEdited += OnTreeItemEdited;
        _recipeTree.ButtonClicked += OnRecipeTreeButtonClicked;

        _errorIcon = GD.Load<Texture2D>("res://Assets/Error.png");
        _recipeLoopPopup = GetNode<PopupPanel>("RecipeLoopPopup");
        _recipeLoopPopup.Visible = false;
        _recipeLoopLabel = _recipeLoopPopup.GetNode<Label>("MarginContainer/Label");

        ThemeChanged += OnThemeChanged;
        OnThemeChanged();
    }

    public void ShowRecipe(ulong id, uint quantity = 1)
    {
        var craftingItem = _data.CraftingItems[id];
        var tabName = craftingItem.Tier > -1 ? $"T{craftingItem.Tier} {craftingItem.GenericName}" : $"{craftingItem.Name}";
        SetName(tabName.Replace(":", ""));

        if (!string.IsNullOrEmpty(craftingItem.Icon))
        {
            var resourcePath = $"res://Assets/{craftingItem.Icon}.png";
            if (ResourceLoader.Exists(resourcePath))
            {
                _recipeIcon.Texture = GD.Load<Texture2D>(resourcePath);
            }
        }
        _recipeName.Text = craftingItem.Name;
        _recipeTier.Text = $"Tier {craftingItem.Tier}";
        _recipeTier.Visible = craftingItem.Tier > -1;
        _recipeRarity.Text = Rarity.GetName(craftingItem.Rarity);
        _recipeRarity.AddThemeColorOverride("font_color", Rarity.GetColor(craftingItem.Rarity));

        _skillIconTexture.Region = Skill.GetAtlasRect(craftingItem.Recipes[0].LevelRequirements[0]);
        _skillLabel.Text = $"{Skill.GetName(craftingItem.Recipes[0].LevelRequirements[0])} Lv. {craftingItem.Recipes[0].LevelRequirements[1]}";

        _recipeSelection.Clear();
        for (var i = 1; i <= craftingItem.Recipes.Count; i++)
        {
            _recipeSelection.AddItem($"Recipe {i}");
        }
        _recipeSelection.Visible = craftingItem.Recipes.Count > 1;

        _recipeTree.Clear();
        var rootItem = _recipeTree.CreateItem();
        var meta = new ItemMetadata()
        {
            Id = id,
            ShownIds = [],
            RecipeIndex = 0,
            MinQuantity = quantity,
            MaxQuantity = quantity
        };
        BuildTree(rootItem, meta);
        _recipeSelection.Select(0);
        _quantitySelection.SetValueNoSignal(quantity);
    }

    public void SetQuantity(ulong quantity) => _quantitySelection.Value = quantity;

    public string GetTreeAsText()
    {
        var recipeRoot = _recipeTree.GetRoot();
        var text = new StringBuilder();
        text.Append($"**{recipeRoot.GetText(0)} x{recipeRoot.GetText(2)}**\n");
        text.Append("\n```\n");
        TraverseAndAppendText(recipeRoot, [], ref text);
        text.Append("```");
        return text.ToString();
    }

    public string GetTreeAsCSV()
    {
        var recipeRoot = _recipeTree.GetRoot();
        var csv = new StringBuilder();
        csv.Append($"{recipeRoot.GetText(0)},{(uint)_quantitySelection.Value}");
        csv.AppendLine("\nItem,Minimum Quantity,Maximum Quantity,In Stock");
        TraverseAndAppendCSV(recipeRoot, [], ref csv);
        return csv.ToString();
    }

    public void GetBaseIngredients(ref Dictionary<ulong, (int, int)> data) => TraverseAndCountBaseIngredients(_recipeTree.GetRoot(), ref data);

    public static string GetQuantityString(uint minQuantity, uint maxQuantity)
    {
        if (maxQuantity == minQuantity)
        {
            return $"{minQuantity:N0}";
        }
        else if (maxQuantity == 0)
        {
            return $"≥ {minQuantity:N0}";
        }
        return $"{minQuantity:N0}—{maxQuantity:N0}";
    }

    /// <summary>
    /// Gets a possible output range for the given recipe.
    /// If the output is fixed and guaranteed, returned minimum and maximum outputs are the same.
    /// If the item is not guaranteed to craft, returned minimum output is 0.
    /// </summary>
    /// <param name="recipe">A recipe to calculate output for</param>
    /// <returns>Minimum and maximum output</returns>
    private static (uint, uint) CalculateRecipeOutput(Recipe recipe)
    {
        var minOutput = UInt32.MaxValue;
        var maxOutput = 1u;
        var possibilitiesSum = 0.0;
        foreach (var possibility in recipe.Possibilities)
        {
            possibilitiesSum += possibility.Value;
            // 8.0 seems to indicate equal chance to get 1-2 items
            if (possibility.Value >= 8.0)
            {
                minOutput = 1;
                maxOutput = possibility.Key;
            }
            // 2.0 seems to indicate equal chance to get 0-1 item
            else if (possibility.Value >= 2.0 || possibility.Value < 1.0)
            {
                minOutput = 0;
                maxOutput = possibility.Key;
            }

            if (possibility.Key < minOutput)
            {
                minOutput = possibility.Key;
            }
            if (possibility.Key > maxOutput)
            {
                maxOutput = possibility.Key;
            }
        }
        if (minOutput == UInt32.MaxValue)
        {
            minOutput = 1;
        }
        else if (minOutput == 0 && possibilitiesSum >= 1.0)
        {
            minOutput = recipe.Possibilities.Min(p => p.Key);
        }
        minOutput *= recipe.OutputQuantity;
        maxOutput *= recipe.OutputQuantity;
        return (minOutput, maxOutput);
    }

    private static string BuildTreeIndent(bool[] relationshipLines)
    {
        var result = new StringBuilder();
        foreach (var line in relationshipLines)
        {
            result.Append(line ? "| " : "  ");
        }
        return result.ToString();
    }

    private static void TraverseAndCountBaseIngredients(TreeItem item, ref Dictionary<ulong, (int, int)> data)
    {
        var guaranteedCraft = true;
        if (Config.TreatNonGuaranteedItemsAsBase)
        {
            foreach (var child in item.GetChildren())
            {
                var childMaxQuantity = ItemMetadata.FromGodotArray(child.GetMetadata(0).AsGodotArray()).MaxQuantity;
                if (childMaxQuantity == 0)
                {
                    guaranteedCraft = false;
                    break;
                }
            }
        }
        if (item.GetChildCount() > 0 && guaranteedCraft)
        {
            foreach (var child in item.GetChildren())
            {
                TraverseAndCountBaseIngredients(child, ref data);
            }
            return;
        }

        var meta = ItemMetadata.FromGodotArray(item.GetMetadata(0).AsGodotArray());
        data.TryGetValue(meta.Id, out var quantity);
        if (meta.MaxQuantity > 0)
        {
            quantity.Item1 += (int)meta.MinQuantity;
            if (quantity.Item2 >= 0)
            {
                quantity.Item2 += (int)meta.MaxQuantity;
            }
        }
        else
        {
            if (meta.MinQuantity > quantity.Item1)
            {
                quantity.Item1 = (int)meta.MinQuantity;
            }
            // For the sake of code simplification, here -1 means unknown maximum quantity, unlike in metadata where it's 0
            // That's needed to differentiate between unknown quantity and the default value when we start counting
            quantity.Item2 = -1;
        }
        data[meta.Id] = quantity;
    }

    private static void TraverseAndAppendText(TreeItem parent, bool[] relationshipLines, ref StringBuilder text)
    {
        if (Config.IgnoreHiddenInTreesExport && parent.Collapsed)
        {
            return;
        }
        const int MAX_LENGTH = 52;
        foreach (var child in parent.GetChildren())
        {
            var firstColumnString = new StringBuilder();
            firstColumnString.Append(BuildTreeIndent(relationshipLines));
            firstColumnString.Append(child.GetText(0));
            while (firstColumnString.Length < MAX_LENGTH)
            {
                firstColumnString.Append(' ');
            }
            text.Append(firstColumnString, 0, MAX_LENGTH);
            text.Append(' ');
            text.Append(child.GetText(2));
            text.Append('\n');

            var hasMoreSiblings = child.GetIndex() != parent.GetChildCount() - 1;
            var newRelationshipLines = relationshipLines.Append(hasMoreSiblings).ToArray();
            TraverseAndAppendText(child, newRelationshipLines, ref text);
        }
    }

    private static void TraverseAndAppendCSV(TreeItem parent, bool[] relationshipLines, ref StringBuilder csv)
    {
        if (Config.IgnoreHiddenInTreesExport && parent.Collapsed)
        {
            return;
        }
        foreach (var child in parent.GetChildren())
        {
            var indent = BuildTreeIndent(relationshipLines);
            var name = child.GetText(0);
            var meta = ItemMetadata.FromGodotArray(child.GetMetadata(0).AsGodotArray());
            csv.AppendLine($"{indent}{name},{meta.MinQuantity},{meta.MaxQuantity},0");

            var hasMoreSiblings = child.GetIndex() != parent.GetChildCount() - 1;
            var newRelationshipLines = relationshipLines.Append(hasMoreSiblings).ToArray();
            TraverseAndAppendCSV(child, newRelationshipLines, ref csv);
        }
    }

    private void BuildTree(TreeItem treeItem, ItemMetadata meta)
    {
        foreach (var child in treeItem.GetChildren())
        {
            treeItem.RemoveChild(child);
            child.Free();
        }
        treeItem.SetMetadata(0, meta.ToGodotArray());
        var craftingItem = _data.CraftingItems[meta.Id];

        treeItem.SetText(0, craftingItem.Tier > -1 ? $"{craftingItem.Name} (T{craftingItem.Tier})" : craftingItem.Name);
        var tooltipName = craftingItem.Tier > -1 ? $"T{craftingItem.Tier} {craftingItem.GenericName}" : craftingItem.Name;
        treeItem.SetTooltipText(0, $"{tooltipName} ({Rarity.GetName(craftingItem.Rarity)})");
        treeItem.SetCustomColor(0, Rarity.GetColor(craftingItem.Rarity));
        if (!string.IsNullOrEmpty(craftingItem.Icon))
        {
            var resourcePath = $"res://Assets/{craftingItem.Icon}.png";
            if (ResourceLoader.Exists(resourcePath))
            {
                treeItem.SetIcon(0, GD.Load<Texture2D>(resourcePath));
            }
        }

        if (treeItem.GetCellMode(1) != TreeItem.TreeCellMode.Range)
        {
            if (craftingItem.Recipes.Count > 1)
            {
                treeItem.SetCellMode(1, TreeItem.TreeCellMode.Range);
                treeItem.SetRangeConfig(1, 1, craftingItem.Recipes.Count, 1.0);
                var rangeText = new StringBuilder();
                for (var i = 1; i <= craftingItem.Recipes.Count; i++)
                {
                    rangeText.Append($"Recipe {i},");
                }
                rangeText.Remove(rangeText.Length - 1, 1);
                treeItem.SetText(1, rangeText.ToString());
                treeItem.SetRange(1, meta.RecipeIndex);
                treeItem.SetEditable(1, true);
            }
            else
            {
                treeItem.SetText(1, "");
            }
        }

        treeItem.SetTextAlignment(2, HorizontalAlignment.Right);
        var quantityString = GetQuantityString(meta.MinQuantity, meta.MaxQuantity);
        treeItem.SetText(2, quantityString);
        if (quantityString.Length > 9)
        {
            treeItem.SetTooltipText(2, quantityString);
        }

        if (meta.RecipeIndex < craftingItem.Recipes.Count)
        {
            var recipe = craftingItem.Recipes[(int)meta.RecipeIndex];
            var shownIds = new HashSet<ulong>(meta.ShownIds);
            foreach (var consumedItem in recipe.ConsumedItems)
            {
                if (!shownIds.Add(consumedItem.Id))
                {
                    treeItem.AddButton(0, _errorIcon);
                    return;
                }
            }

            var (minOutput, maxOutput) = CalculateRecipeOutput(recipe);
            foreach (var consumedItem in recipe.ConsumedItems)
            {
                if (!_data.CraftingItems.ContainsKey(consumedItem.Id))
                {
                    continue;
                }
                var childItem = treeItem.CreateChild();
                childItem.Collapsed = _allCollapsed;
                var childMinQuantity = (uint)Math.Ceiling((double)meta.MinQuantity / maxOutput) * consumedItem.Quantity;
                // If minOutput is 0 it means that the item is not guaranteed to craft, so we can't know maximum quantity for ingredients and it's therefore set to 0
                var childMaxQuantity = minOutput > 0 ? (uint)Math.Ceiling((double)meta.MaxQuantity / minOutput) * consumedItem.Quantity : 0;
                var childMetadata = new ItemMetadata
                {
                    Id = consumedItem.Id,
                    ShownIds = shownIds,
                    RecipeIndex = 0,
                    MinQuantity = childMinQuantity,
                    MaxQuantity = childMaxQuantity
                };
                BuildTree(childItem, childMetadata);
            }
        }
    }

    private void TraverseAndChangeQuantity(TreeItem item)
    {
        var meta = ItemMetadata.FromGodotArray(item.GetMetadata(0).AsGodotArray());
        var quantityString = GetQuantityString(meta.MinQuantity, meta.MaxQuantity);
        item.SetText(2, quantityString);
        item.SetTooltipText(2, quantityString.Length > 9 ? quantityString : "");

        if (_data.CraftingItems[meta.Id].Recipes.Count == 0)
        {
            return;
        }
        var recipe = _data.CraftingItems[meta.Id].Recipes[(int)meta.RecipeIndex];
        var (minOutput, maxOutput) = CalculateRecipeOutput(recipe);
        foreach (var child in item.GetChildren())
        {
            var consumedQuantity = recipe.ConsumedItems[child.GetIndex()].Quantity;
            var minQuantity = (uint)Math.Ceiling((double)meta.MinQuantity / maxOutput) * consumedQuantity;
            // If minOutput is 0 it means that the item is not guaranteed to craft, so we can't know maximum quantity for ingredients and it's therefore set to 0
            var maxQuantity = minOutput > 0 ? (uint)Math.Ceiling((double)meta.MaxQuantity / minOutput) * consumedQuantity : 0;
            var childMeta = ItemMetadata.FromGodotArray(child.GetMetadata(0).AsGodotArray());
            childMeta.MinQuantity = minQuantity;
            childMeta.MaxQuantity = maxQuantity;
            child.SetMetadata(0, childMeta.ToGodotArray());
            TraverseAndChangeQuantity(child);
        }
    }

    private void CollapseItem(TreeItem item)
    {
        item.Collapsed = _allCollapsed;
        foreach (var child in item.GetChildren())
        {
            CollapseItem(child);
        }
    }

    private void OnRecipeChanged(long index)
    {
        var treeItem = _recipeTree.GetRoot();
        var meta = ItemMetadata.FromGodotArray(treeItem.GetMetadata(0).AsGodotArray());
        if (meta.RecipeIndex == index)
        {
            return;
        }
        meta.RecipeIndex = (uint)index;
        BuildTree(treeItem, meta);
    }

    private void OnQuantityChanged(double quantity)
    {
        var root = _recipeTree.GetRoot();
        var meta = ItemMetadata.FromGodotArray(root.GetMetadata(0).AsGodotArray());
        if (meta.MinQuantity == quantity)
        {
            return;
        }
        meta.MinQuantity = (uint)quantity;
        meta.MaxQuantity = meta.MinQuantity;
        root.SetMetadata(0, meta.ToGodotArray());
        TraverseAndChangeQuantity(root);
    }

    private void OnTreeItemEdited()
    {
        var treeItem = _recipeTree.GetEdited();
        var meta = ItemMetadata.FromGodotArray(treeItem.GetMetadata(0).AsGodotArray());
        var newRecipeIndex = (uint)treeItem.GetRange(1);
        if (meta.RecipeIndex == newRecipeIndex)
        {
            return;
        }
        meta.RecipeIndex = newRecipeIndex;
        treeItem.ClearButtons();
        var collapsed = treeItem.Collapsed;
        BuildTree(treeItem, meta);
        treeItem.Collapsed = collapsed;
    }

    private void OnRecipeTreeButtonClicked(TreeItem item, long column, long it, long mouseButtonIndex)
    {
        _recipeLoopPopup.PopupCentered();
        _recipeLoopLabel.Text = $"Selected recipe for {item.GetText(0)} requires items that are already present on this branch of the crafting tree, creating an infinite loop.";
    }

    private void OnThemeChanged()
    {
        _skillIcon.Modulate = Color.FromHtml(Config.Theme == Config.ThemeVariant.Dark ? "e9dfc4" : "15567e");
    }
}
