using Godot;
using System;
using System.Linq;

public partial class CraftOverview : MarginContainer
{
    private readonly GameData _data = GameData.Instance;
    private LineEdit _searchEntry;
    private OptionButton _skillSelection;
    private OptionButton _tierSelection;
    private FlowContainer _items;

    public event EventHandler<ulong> RecipeRequested;

    public override void _Ready()
    {
        _searchEntry = GetNode<LineEdit>("VBoxContainer/HBoxContainer/SearchEntry");
        _searchEntry.TextChanged += (_) => OnOverviewFilterChanged();
        _skillSelection = GetNode<OptionButton>("VBoxContainer/HBoxContainer/SkillSelection");
        foreach (var skill in Skill.All.Where(s => s < 15))
        {
            _skillSelection.AddItem(Skill.GetName(skill));
        }
        _skillSelection.ItemSelected += (_) => OnOverviewFilterChanged();
        _tierSelection = GetNode<OptionButton>("VBoxContainer/HBoxContainer/TierSelection");
        _tierSelection.ItemSelected += (_) => OnOverviewFilterChanged();
        _items = GetNode<FlowContainer>("VBoxContainer/ScrollContainer/Items");

        ThemeChanged += OnThemeChanged;
    }

    public void Load()
    {
        foreach (var item in _data.CraftingItems.Where((i) => i.Value.Craftable))
        {
            var data = item.Value;
            var button = new Button();
            _items.AddChild(button);
            button.Text = data.Name;
            button.SetMeta("Skill", data.Recipes[0].LevelRequirements[0]);
            button.SetMeta("Tier", data.Tier);
            button.TooltipText = $"{data.Name} ({Rarity.GetName(data.Rarity)})";
            if (!string.IsNullOrEmpty(data.Icon))
            {
                var resourcePath = $"res://Assets/{data.Icon}.png";
                if (ResourceLoader.Exists(resourcePath))
                {
                    button.Icon = GD.Load<Texture2D>(resourcePath);
                }
            }

            button.SetMeta("Rarity", data.Rarity);
            foreach (var styleName in new[] { "normal", "pressed", "hover", "hover_pressed" })
            {
                var defaultStyle = button.GetThemeStylebox(styleName) as StyleBoxFlat;
                var customStyle = defaultStyle.Duplicate() as StyleBoxFlat;
                customStyle.SetBorderWidthAll(1);
                customStyle.BorderColor = Rarity.GetColor(data.Rarity);
                button.AddThemeStyleboxOverride(styleName, customStyle);
            }
            button.CustomMinimumSize = new Vector2(0, 48);

            button.MouseFilter = MouseFilterEnum.Pass;
            button.Pressed += () => RecipeRequested?.Invoke(button, item.Key);
        }
    }

    private void OnOverviewFilterChanged()
    {
        var text = _searchEntry.Text;
        var skill = _skillSelection.Selected > 0 ? Skill.All[_skillSelection.Selected - 1] : -1;
        var tier = _tierSelection.Selected;
        foreach (var node in _items.GetChildren())
        {
            var item = node as Button;
            item.Visible = item.Text.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) > -1 && (skill < 0 || item.GetMeta("Skill", 0).AsInt32() == skill) && (tier < 1 || item.GetMeta("Tier", 1).AsInt32() == tier);
        }
    }

    private void OnThemeChanged()
    {
        foreach (var button in _items.GetChildren().Cast<Button>())
        {
            foreach (var styleName in new[] { "normal", "pressed", "hover", "hover_pressed" })
            {
                button.RemoveThemeStyleboxOverride(styleName);
                var defaultStyle = button.GetThemeStylebox(styleName) as StyleBoxFlat;
                var customStyle = defaultStyle.Duplicate() as StyleBoxFlat;
                customStyle.SetBorderWidthAll(1);
                customStyle.BorderColor = Rarity.GetColor(button.GetMeta("Rarity").AsInt32());
                button.AddThemeStyleboxOverride(styleName, customStyle);
            }
        }
    }
}
