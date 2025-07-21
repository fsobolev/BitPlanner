using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class CraftPage : PanelContainer, IPage
{
    private readonly GameData _data = GameData.Instance;
    private Action _backButtonCallback;
    private Dictionary<string, Action> _menuActions;
    private CraftOverview _overview;
    private VBoxContainer _recipeView;
    private TabContainer _recipeTabs;
    private PackedScene _recipeTabScene;
    private PopupPanel _baseIngredientsPopup;
    private Tree _baseIngredientsTree;
    private Button _baseIngredientsCopyPlain;
    private Button _baseIngredientsCopyCsv;
    private RecipeTab _currentRecipeTab => _recipeTabs.GetChild<RecipeTab>(_recipeTabs.CurrentTab);

    public Action BackButtonCallback
    {
        get => _backButtonCallback;

        private set
        {
            _backButtonCallback = value;
            BackButtonCallbackChanged?.Invoke(this, value);
        }
    }

    public Dictionary<string, Action> MenuActions
    {
        get => _menuActions;

        private set
        {
            _menuActions = value;
            MenuActionsChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<Action> BackButtonCallbackChanged;
    public event EventHandler<Dictionary<string, Action>> MenuActionsChanged;

    public override void _Ready()
    {
        _overview = GetNode<CraftOverview>("Overview");
        _overview.RecipeRequested += (_, id) => ShowRecipe(id, 1);
        _recipeView = GetNode<VBoxContainer>("RecipeView");
        _recipeTabs = _recipeView.GetNode<TabContainer>("RecipeTabs");
        _recipeTabs.GetTabBar().TabCloseDisplayPolicy = TabBar.CloseButtonDisplayPolicy.ShowActiveOnly;
        _recipeTabs.GetTabBar().TabClosePressed += (index) =>
        {
            var tab = _recipeTabs.GetChild((int)index);
            _recipeTabs.RemoveChild(tab);
            tab.QueueFree();
            if (_recipeTabs.GetChildCount() == 0)
            {
                ShowOverview();
            }
        };
        _recipeTabs.TabChanged += (_) => MenuActions = BuildMenu();
        _recipeTabScene = GD.Load<PackedScene>("res://Scenes/RecipeTab.tscn");

        _baseIngredientsPopup = GetNode<PopupPanel>("BaseIngredientsPopup");
        _baseIngredientsPopup.Visible = false;
        _baseIngredientsTree = _baseIngredientsPopup.GetNode<Tree>("VBoxContainer/BaseIngredientsTree");
        _baseIngredientsTree.SetColumnExpandRatio(0, 2);
        _baseIngredientsCopyPlain = _baseIngredientsPopup.GetNode<Button>("VBoxContainer/MarginContainer/HBoxContainer/CopyPlain");
        _baseIngredientsCopyPlain.Pressed += OnBaseIngredientsCopyPlainRequested;
        _baseIngredientsCopyCsv = _baseIngredientsPopup.GetNode<Button>("VBoxContainer/MarginContainer/HBoxContainer/CopyCSV");
        _baseIngredientsCopyCsv.Pressed += OnBaseIngredientsCopyCsvRequested;
    }

    public void Load()
    {
        _overview.Load();
    }

    public void ShowOverview()
    {
        _overview.Visible = true;
        _recipeView.Visible = false;
        BackButtonCallback = null;
        MenuActions = [];
    }

    public void ShowRecipe(ulong id, uint quantity)
    {
        _overview.Visible = false;
        _recipeView.Visible = true;
        BackButtonCallback = ShowOverview;
        var targetTab = -1;
        foreach (var tab in _recipeTabs.GetChildren())
        {
            if (tab.GetMeta("id").AsUInt64() == id)
            {
                targetTab = tab.GetIndex();
                break;
            }
        }
        if (targetTab > -1)
        {
            _recipeTabs.CurrentTab = targetTab;
            var tab = _recipeTabs.GetTabControl(targetTab) as RecipeTab;
            tab.SetQuantity(quantity);
        }
        else
        {
            var tab = (RecipeTab)_recipeTabScene.Instantiate();
            tab.SetMeta("id", id);
            _recipeTabs.AddChild(tab);
            _recipeTabs.CurrentTab = _recipeTabs.GetChildCount() - 1;
            tab.ShowRecipe(id, quantity);
        }
        MenuActions = BuildMenu();
    }

    private Dictionary<string, Action> BuildMenu() => new()
    {
        { _currentRecipeTab.AllCollapsed ? "Expand Tree" : "Collapse Tree", ToggleCollapse },
        { "Copy Trees As Text", CopyTreesAsText },
        { "Copy Trees As CSV", CopyTreesAsCSV },
        { "Show Base Ingredients", ShowBaseIngredients }
    };

    public void ToggleCollapse()
    {
        _currentRecipeTab.AllCollapsed = !_currentRecipeTab.AllCollapsed;
        MenuActions = BuildMenu();
    }

    private void CopyTreesAsText()
    {
        var result = new StringBuilder();
        var separator = new string('-', 64);
        foreach (var tab in _recipeTabs.GetChildren().Cast<RecipeTab>())
        {
            result.Append(tab.GetTreeAsText());
            result.Append($"\n\n{separator}\n\n");
        }
        DisplayServer.ClipboardSet(result.ToString().Trim());
    }

    public void CopyTreesAsCSV()
    {
        var result = new StringBuilder();
        foreach (var tab in _recipeTabs.GetChildren().Cast<RecipeTab>())
        {
            result.Append(tab.GetTreeAsCSV());
            result.Append("\n\n");
        }
        DisplayServer.ClipboardSet(result.ToString().Trim());
    }

    private void ShowBaseIngredients()
    {
        var unsortedData = new Dictionary<ulong, (int, int)>();
        foreach (var tab in _recipeTabs.GetChildren().Cast<RecipeTab>())
        {
            tab.GetBaseIngredients(ref unsortedData);
        }
        var data = unsortedData.OrderBy(pair => $"{_data.CraftingItems[pair.Key].GenericName}{_data.CraftingItems[pair.Key].Tier}");

        _baseIngredientsTree.Clear();
        var ingredientsRoot = _baseIngredientsTree.CreateItem();

        var skillTreeItems = new Dictionary<int, TreeItem>();
        foreach (var skill in Skill.All)
        {
            var skillTreeItem = _baseIngredientsTree.CreateItem(ingredientsRoot);
            skillTreeItem.SetText(0, Skill.GetName(skill));
            skillTreeItem.SetExpandRight(0, true);
            skillTreeItems.Add(skill, skillTreeItem);
        }
        var unknown = _baseIngredientsTree.CreateItem(ingredientsRoot);
        unknown.SetText(0, Skill.GetName(-1));
        unknown.SetExpandRight(0, true);
        skillTreeItems.Add(-1, unknown);

        var dataForCopying = new Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>();
        foreach (var item in data)
        {
            var craftingItem = _data.CraftingItems[item.Key];
            var skill = -1;
            if (craftingItem.ExtractionSkill > -1)
            {
                skill = craftingItem.ExtractionSkill;
            }
            else if (craftingItem.Recipes.Count > 0)
            {
                skill = craftingItem.Recipes[0].LevelRequirements[0];
            }
            var treeItem = _baseIngredientsTree.CreateItem(skillTreeItems[skill]);

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

            treeItem.SetTextAlignment(1, HorizontalAlignment.Right);
            var minQuantity = (uint)item.Value.Item1;
            // Here maxQuantity can be -1, see RecipeTab.TraverseAndCountBaseIngredients()
            var maxQuantity = item.Value.Item2 < 0 ? 0u : (uint)item.Value.Item2;
            var quantityString = RecipeTab.GetQuantityString(minQuantity, maxQuantity);
            treeItem.SetText(1, quantityString);
            if (quantityString.Length > 9)
            {
                treeItem.SetTooltipText(1, quantityString);
            }

            dataForCopying.Add(craftingItem.Name, [
                craftingItem.Tier > -1 ? $"{craftingItem.GenericName} — T{craftingItem.Tier}" : craftingItem.Name,
                Skill.GetName(skill),
                quantityString
            ]);
        }
        foreach (var skillTreeItem in skillTreeItems.Values)
        {
            if (skillTreeItem.GetChildCount() == 0)
            {
                skillTreeItem.Visible = false;
            }
        }
        ingredientsRoot.SetMetadata(0, dataForCopying);
        _baseIngredientsPopup.PopupCentered();
    }

    private void OnBaseIngredientsCopyPlainRequested()
    {
        var unsortedData = _baseIngredientsTree.GetRoot().GetMetadata(0).AsGodotDictionary<string, Godot.Collections.Array<string>>();
        var data = unsortedData.OrderBy(pair => pair.Value[1]);
        var text = new StringBuilder();
        var skill = "";
        foreach (var item in data)
        {
            if (skill != item.Value[1])
            {
                skill = item.Value[1];
                text.Append($"\n{skill}\n");
            }
            text.Append($"{item.Key}: {item.Value[2]}\n");
        }
        DisplayServer.ClipboardSet(text.ToString().Trim());
    }

    private void OnBaseIngredientsCopyCsvRequested()
    {
        var unsortedData = _baseIngredientsTree.GetRoot().GetMetadata(0).AsGodotDictionary<string, Godot.Collections.Array<string>>();
        var data = unsortedData.OrderBy(pair => pair.Value[1]);
        var text = new StringBuilder();
        text.Append("Item Name,Generic Name,Profession/Skill,Quantity,In Stock\n");
        foreach (var item in data)
        {
            text.Append($"{item.Key},{item.Value[0]},{item.Value[1]},{item.Value[2]},0\n");
        }
        DisplayServer.ClipboardSet(text.ToString());
    }
}
