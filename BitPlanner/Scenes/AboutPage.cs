using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class AboutPage : PanelContainer, IPage
{
    private readonly GameData _data = GameData.Instance;
    private static readonly System.Net.Http.HttpClient _httpClient = new();
    private TabContainer _dataUpdateContainer;
    private Button _dataUpdateButton;
    private AnimationPlayer _dataUpdatePlayer;
    public Action BackButtonCallback => null;
    public Dictionary<string, Action> MenuActions => [];

    public override void _Ready()
    {
        var appVersion = ProjectSettings.GetSetting("application/config/version");
        GetNode<Label>("ScrollContainer/MarginContainer/VBoxContainer/AppVersion").Text = $"Version {appVersion}";
        VisibilityChanged += OnVisibilityChanged;

        _dataUpdateButton = GetNode<Button>("ScrollContainer/MarginContainer/VBoxContainer/DataUpdateButton");
        _dataUpdateButton.Pressed += OnDataUpdateButtonPressed;
        _dataUpdateContainer = GetNode<TabContainer>("ScrollContainer/MarginContainer/VBoxContainer/DataUpdate");
        _dataUpdateContainer.Visible = false;
        _dataUpdatePlayer = _dataUpdateContainer.GetNode<AnimationPlayer>("DataUpdateLabel/AnimationPlayer");
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            GetNode<Label>("ScrollContainer/MarginContainer/VBoxContainer/DataVersion").Text = $"Game data as of {_data.Version}";
        }
    }

    private void OnDataUpdateButtonPressed()
    {
        _dataUpdateButton.Disabled = true;
        _dataUpdateContainer.Visible = true;
        _dataUpdateContainer.CurrentTab = 0;
        _dataUpdatePlayer.Play("getting_version");
        Task.Run(CheckDataUpdates);
    }

    private async Task CheckDataUpdates()
    {
        try
        {
            var latestVersionString = await _httpClient.GetStringAsync("https://raw.githubusercontent.com/fsobolev/BitPlanner/refs/heads/main/BitPlanner/data_version.txt");
            var latestVersion = DateOnly.Parse(latestVersionString);
            if (latestVersion > _data.Version)
            {
                DirAccess.MakeDirRecursiveAbsolute("user://data");
                _dataUpdatePlayer.CallDeferred(AnimationPlayer.MethodName.Play, ["downloading"]);
                foreach (var filename in new string[] { "crafting_data", "travelers_data" })
                {
                    var data = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/fsobolev/BitPlanner/refs/heads/main/BitPlanner/{filename}.json");
                    using var file = FileAccess.Open($"user://data/{filename}.json", FileAccess.ModeFlags.Write);
                    file.StoreString(data);
                }
                using var versionFile = FileAccess.Open("user://data/data_version.txt", FileAccess.ModeFlags.Write);
                versionFile.StoreString(latestVersionString);
                _dataUpdateContainer.SetDeferred(TabContainer.PropertyName.CurrentTab, 2);
            }
            else
            {
                _dataUpdateButton.SetDeferred(BaseButton.PropertyName.Disabled, false);
                _dataUpdateContainer.SetDeferred(TabContainer.PropertyName.CurrentTab, 3);
            }
        }
        catch (Exception e)
        {
            GD.Print(e);
            _dataUpdateButton.SetDeferred(BaseButton.PropertyName.Disabled, false);
            _dataUpdateContainer.SetDeferred(TabContainer.PropertyName.CurrentTab, 1);
        }
        _dataUpdatePlayer.CallDeferred(AnimationPlayer.MethodName.Stop);
    }
}
