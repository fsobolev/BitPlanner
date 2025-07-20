using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TasksPage : PanelContainer, IPage
{
    private readonly GameData _data = GameData.Instance;
    private PackedScene _travelerScene;
    private TabContainer _travelers;
    public Action BackButtonCallback => null;
    public Dictionary<string, Action> MenuActions => [];

    public event EventHandler<Traveler.CraftRequestedEventArgs> CraftRequested;

    public override void _Ready()
    {
        _travelerScene = GD.Load<PackedScene>("res://Scenes/Traveler.tscn");
        _travelers = GetNode<TabContainer>("MarginContainer/Travelers");
    }

    public void Load()
    {
        foreach (var travelerData in _data.Travelers)
        {
            var traveler = (Traveler)_travelerScene.Instantiate();
            _travelers.AddChild(traveler);
            traveler.Load(travelerData);
            traveler.CraftRequested += (s, e) => CraftRequested?.Invoke(s, e);
            traveler.FilterToggled += (toggled) =>
            {
                Config.FilterTasks = toggled;
                foreach (var tab in _travelers.GetChildren().Cast<Traveler>())
                {
                    tab.OnFilterToggled(toggled);
                }
            };
        }
    }
}
