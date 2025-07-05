using Godot;
using System;
using System.Collections.Generic;

public partial class AboutPage : PanelContainer, IPage
{
    private readonly GameData _data = GameData.Instance;
    public Action BackButtonCallback => null;
    public Dictionary<string, Action> MenuActions => [];

    public override void _Ready()
    {
        var appVersion = ProjectSettings.GetSetting("application/config/version");
        GetNode<Label>("ScrollContainer/MarginContainer/VBoxContainer/AppVersion").Text = $"Version {appVersion}";
        VisibilityChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            GetNode<Label>("ScrollContainer/MarginContainer/VBoxContainer/DataVersion").Text = $"Game data as of {_data.Version}";
        }
    }
}
