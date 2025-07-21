using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class RecipeTab : VBoxContainer
{
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
        _recipeTree.SetColumnExpandRatio(0, (int)Math.Round(10 / Config.Scale * 2));
        var rootItem = _recipeTree.CreateItem();
        BuildTree(id, rootItem, [id], 0, quantity, quantity);
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

    public TreeItem GetTreeRoot() => _recipeTree.GetRoot();

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

	private static string BuildTreeIndent(bool[] relationshipLines)
	{
        var result = new StringBuilder();
        foreach (var line in relationshipLines)
        {
            result.Append(line ? "| " : "  ");
        }
        return result.ToString();
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
            var name = parent.GetText(0);
            var quantity = parent.GetMetadata(2).AsGodotArray();
            var minQuantity = quantity[0].AsUInt32();
            var maxQuantity = quantity[1].AsUInt32();
            csv.AppendLine($"{indent}{name},{minQuantity},{maxQuantity},0");

            var hasMoreSiblings = child.GetIndex() != parent.GetChildCount() - 1;
            var newRelationshipLines = relationshipLines.Append(hasMoreSiblings).ToArray();
            TraverseAndAppendCSV(child, newRelationshipLines, ref csv);
        }
    }

    private void BuildTree(ulong id, TreeItem treeItem, HashSet<ulong> shownIds, uint recipeIndex, uint minQuantity, uint maxQuantity)
    {
        foreach (var child in treeItem.GetChildren())
        {
            treeItem.RemoveChild(child);
            child.Free();
        }

        var craftingItem = _data.CraftingItems[id];
        treeItem.SetMetadata(0, id);

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
                treeItem.SetRange(1, recipeIndex);
                treeItem.SetEditable(1, true);
            }
            else
            {
                treeItem.SetText(1, "");
            }
        }
        var recipeMeta = new Godot.Collections.Array()
        {
            recipeIndex,
            new Godot.Collections.Array(shownIds.Select(i => Variant.CreateFrom(i)))
        };
        treeItem.SetMetadata(1, recipeMeta);

        treeItem.SetTextAlignment(2, HorizontalAlignment.Right);
        var quantityString = GetQuantityString(minQuantity, maxQuantity);
        treeItem.SetText(2, quantityString);
        if (quantityString.Length > 9)
        {
            treeItem.SetTooltipText(2, quantityString);
        }
        var quantityMeta = new Godot.Collections.Array
        {
            minQuantity,
            maxQuantity
        };
        treeItem.SetMetadata(2, quantityMeta);

        if (recipeIndex < craftingItem.Recipes.Count)
        {
            // Calculating quantity of items produced by the recipe.
            // If the quantity is fixed and guaranteed, minOutput and maxOutput are the same.
            var recipe = craftingItem.Recipes[(int)recipeIndex];
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

            foreach (var consumedItem in recipe.ConsumedItems)
            {
                if (!shownIds.Add(consumedItem.Id))
                {
                    treeItem.AddButton(0, _errorIcon);
                    return;
                }
            }
            foreach (var consumedItem in recipe.ConsumedItems)
            {
                if (!_data.CraftingItems.ContainsKey(consumedItem.Id))
                {
                    continue;
                }
                var child = treeItem.CreateChild();
                child.Collapsed = _allCollapsed;
                var childMinQuantity = (uint)Math.Ceiling((double)minQuantity / maxOutput) * consumedItem.Quantity;
                // If minOutput is 0 it means that the item is not guaranteed to craft, so we can't know maximum quantity for ingredients and it's therefore set to 0
                var childMaxQuantity = minOutput > 0 ? (uint)Math.Ceiling((double)maxQuantity / minOutput) * consumedItem.Quantity : 0;
                BuildTree(consumedItem.Id, child, new(shownIds), 0, childMinQuantity, childMaxQuantity);
            }
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
        var recipeMeta = treeItem.GetMetadata(1).AsGodotArray();
        if (recipeMeta[0].AsInt64() == index)
        {
            return;
        }

        var id = treeItem.GetMetadata(0).AsUInt64();
        var quantityMeta = treeItem.GetMetadata(2).AsGodotArray();
        BuildTree(id, treeItem, [id], (uint)index, quantityMeta[0].AsUInt32(), quantityMeta[1].AsUInt32());
    }

    private void OnQuantityChanged(double quantity)
    {
        var treeItem = _recipeTree.GetRoot();
        var quantityMeta = treeItem.GetMetadata(2).AsGodotArray();
        if (quantityMeta[0].AsDouble() == quantity)
        {
            return;
        }

        var id = treeItem.GetMetadata(0).AsUInt64();
        var recipeMeta = treeItem.GetMetadata(1).AsGodotArray();
        BuildTree(id, treeItem, [id], recipeMeta[0].AsUInt32(), (uint)quantity, (uint)quantity);
    }

    private void OnTreeItemEdited()
    {
        var treeItem = _recipeTree.GetEdited();
        var recipeMeta = treeItem.GetMetadata(1).AsGodotArray();
        var newRecipeIndex = (uint)treeItem.GetRange(1);
        if (recipeMeta[0].AsUInt32() == newRecipeIndex)
        {
            return;
        }

        treeItem.ClearButtons();
        var id = treeItem.GetMetadata(0).AsUInt64();
        var shownIdsArray = recipeMeta[1].AsGodotArray();
        var shownIds = new HashSet<ulong>();
        foreach (var shownId in shownIdsArray)
        {
            shownIds.Add(shownId.AsUInt64());
        }
        var quantityMeta = treeItem.GetMetadata(2).AsGodotArray();
        var collapsed = treeItem.Collapsed;
        BuildTree(id, treeItem, shownIds, newRecipeIndex, quantityMeta[0].AsUInt32(), quantityMeta[1].AsUInt32());
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
