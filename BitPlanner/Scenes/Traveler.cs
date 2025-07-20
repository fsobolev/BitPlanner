using Godot;
using System;

public partial class Traveler : VBoxContainer
{
    public class CraftRequestedEventArgs : EventArgs
    {
        public ulong Id;
        public uint Quantity;
    }

    private readonly GameData _data = GameData.Instance;
    private TextureRect _image;
    private Label _name;
    private TextureRect _skillIcon;
    private AtlasTexture _skillIconTexture;
    private Label _skillName;
    private CheckButton _showAvailableButton;
    private Label _levelLabel;
    private SpinBox _levelSpinBox;
    private Tree _tasks;
    private Texture2D _coinsIcon;
    private Texture2D _craftingIcon;

    public event EventHandler<CraftRequestedEventArgs> CraftRequested;
    public event Action<bool> FilterToggled;

    public override void _Ready()
    {
        _image = GetNode<TextureRect>("Header/MarginContainer/Image");
        _name = GetNode<Label>("Header/VBoxContainer/Name");
        _skillIcon = GetNode<TextureRect>("Header/VBoxContainer/HBoxContainer/SkillIcon");
        _skillIconTexture = _skillIcon.Texture as AtlasTexture;
        _skillIconTexture.ResourceLocalToScene = true;
        _skillName = GetNode<Label>("Header/VBoxContainer/HBoxContainer/SkillName");

        _showAvailableButton = GetNode<CheckButton>("Filter/ShowAvailableButton");
        _showAvailableButton.ButtonPressed = Config.FilterTasks;
        _levelLabel = GetNode<Label>("Filter/LevelLabel");
        _levelLabel.Visible = Config.FilterTasks;
        _levelSpinBox = GetNode<SpinBox>("Filter/LevelSpinBox");
        _levelSpinBox.Visible = Config.FilterTasks;
        _showAvailableButton.Toggled += (toggled) => FilterToggled?.Invoke(toggled);
        _levelSpinBox.ValueChanged += (value) =>
        {
            if (Config.FilterTasks)
            {
                FilterTasks((uint)value);
            }
        };

        _tasks = GetNode<Tree>("Tasks");
        _tasks.SetColumnExpandRatio(0, 8);
        _tasks.SetColumnCustomMinimumWidth(1, 98);
        _tasks.ButtonClicked += OnTasksTreeButtonClicked;

        _coinsIcon = GD.Load<Texture2D>("res://Assets/HexCoin.png");
        _craftingIcon = GD.Load<Texture2D>("res://Assets/CraftingSmall.png");

        ThemeChanged += OnThemeChanged;
    }

    public void Load(TravelerData data)
    {
        var iconsColor = Color.FromHtml(Config.Theme == Config.ThemeVariant.Dark ? "e9dfc4" : "15567e");

        Name = data.Name;
        _image.Texture = GD.Load<Texture2D>($"res://Assets/Travelers/{data.Name}.png");
        _name.Text = data.Name;
        _skillIcon.Modulate = iconsColor;
        _skillIconTexture.Region = Skill.GetAtlasRect(data.Skill);
        _skillName.Text = Skill.GetName(data.Skill);

        var root = _tasks.CreateItem();
        foreach (var task in data.Tasks)
        {
            var taskDescription = _tasks.CreateItem(root);

            if (task.Levels[1] < 120)
            {
                taskDescription.SetText(0, $"Level requirements: {task.Levels[0]}—{task.Levels[1]}");
            }
            else
            {
                taskDescription.SetText(0, $"Level requirements: {task.Levels[0]}+");
            }
            taskDescription.SetMetadata(0, task.Levels.ToArray());

            taskDescription.SetText(1, $"{task.Experience:N0} XP");
            taskDescription.SetTextAlignment(1, HorizontalAlignment.Right);

            taskDescription.SetText(2, $"{task.Reward:N0}");
            taskDescription.SetIcon(2, _coinsIcon);

            foreach (var item in task.RequiredItems)
            {
                var itemData = _data.CraftingItems[item.Key];
                var quantity = item.Value;
                var itemRow = _tasks.CreateItem(taskDescription);

                itemRow.SetText(0, $"{itemData.Name}{(quantity > 1 ? $" x{quantity}" : "")}");
                var resourcePath = $"res://Assets/{itemData.Icon}.png";
                if (ResourceLoader.Exists(resourcePath))
                {
                    itemRow.SetIcon(0, GD.Load<Texture2D>(resourcePath));
                }

                if (itemData.Craftable)
                {
                    itemRow.AddButton(0, _craftingIcon);
                    itemRow.SetButtonColor(0, 0, iconsColor);
                }

                var itemMeta = new Godot.Collections.Array
                {
                    item.Key,
                    quantity
                };
                itemRow.SetMetadata(0, itemMeta);
            }
        }

        Config.SkillLevels.TryGetValue(data.Skill, out uint skillLevel);
        _levelSpinBox.Value = skillLevel;
        _levelSpinBox.ValueChanged += (value) => Config.SetSkillLevel(data.Skill, (uint)value);
    }

    public void OnFilterToggled(bool toggled)
    {
        _showAvailableButton.SetPressedNoSignal(toggled);
        _levelLabel.Visible = toggled;
        _levelSpinBox.Visible = toggled;
        FilterTasks(toggled ? (uint)_levelSpinBox.Value : 0);
    }

    private void FilterTasks(uint level)
    {
        foreach (var task in _tasks.GetRoot().GetChildren())
        {
            if (level == 0)
            {
                task.Visible = true;
                continue;
            }
            var requirements = task.GetMetadata(0).AsInt32Array();
            task.Visible = level >= requirements[0] && level <= requirements[1];
        }
    }

    private void OnThemeChanged()
    {
        var iconsColor = Color.FromHtml(Config.Theme == Config.ThemeVariant.Dark ? "e9dfc4" : "15567e");
        _skillIcon.Modulate = iconsColor;

        var root = _tasks.GetRoot();
        foreach (var task in root.GetChildren())
        {
            foreach (var item in task.GetChildren())
            {
                if (item.GetButtonCount(0) > 0)
                {
                    item.SetButtonColor(0, 0, iconsColor);
                }
            }
        }
    }

    private void OnTasksTreeButtonClicked(TreeItem item, long column, long id, long mouseButtonIndex)
    {
        var data = item.GetMetadata((int)column).AsGodotArray();
        var e = new CraftRequestedEventArgs()
        {
            Id = data[0].AsUInt64(),
            Quantity = data[1].AsUInt32()
        };
        CraftRequested?.Invoke(this, e);
    }
}
