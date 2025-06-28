using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Main : Control
{
    /// <summary>
    /// A minimum window content width (without scaling applied) at which the sidebar is shown unfolded
    /// </summary>
    private const int SIDEBAR_THRESHOLD = 850;
    private Theme _lightTheme;
    private Theme _darkTheme;
    private Action _backButtonCallback;
    private Action[] _menuActions;
    private PanelContainer _headerbar;
    private Button _sidebarButton;
    private Button _backButton;
    private MenuButton _actionsMenuButton;
    private Button _settingsButton;
    private HBoxContainer _windowControlsLeft;
    private HBoxContainer _windowControlsRight;
    private Control _sidebarPlaceholder;
    private PanelContainer _sidebar;
    private PanelContainer _sidebarContent;
    private Button _craftPageButton;
    private Button _tasksPageButton;
    private Button _aboutPageButton;
    private ColorRect _sidebarOverlay;
    private CraftPage _craftPage;
    private TasksPage _tasksPage;
    private AboutPage _aboutPage;
    private PopupPanel _settingsPopup;
    private OptionButton _themeSelection;
    private CheckButton _csdCheck;
    private SpinBox _uiScaleSpin;
    private CheckButton _nonGuaranteedAsBaseCheck;
    private Button _settingsSaveButton;

    public override void _Ready()
    {
        var data = GameData.Instance;
        var dataLoaded = data.Load();
        if (!dataLoaded)
        {
            GD.Print("FATAL: Failed to load game data");
            GetTree().Quit();
        }
        Config.Load();

        _lightTheme = GD.Load<Theme>("res://LightTheme.tres");
        _darkTheme = GD.Load<Theme>("res://DarkTheme.tres");

        _headerbar = GetNode<PanelContainer>("Content/HeaderBar");
        _headerbar.GuiInput += (inputEvent) =>
        {
            if (inputEvent is InputEventMouseButton && inputEvent.IsPressed())
            {
                GetWindow().StartDrag();
            }
        };

        var csdBorders = GetNode<GridContainer>("CSDBorders");
        foreach (var border in csdBorders.GetChildren())
        {
            if (border is not ColorRect)
            {
                continue;
            }
            var rect = (ColorRect)border;
            var edge = DisplayServer.WindowResizeEdge.TopLeft;
            var cursor = CursorShape.Arrow;
            switch (rect.Name.ToString())
            {
                case "TopLeft":
                    edge = DisplayServer.WindowResizeEdge.TopLeft;
                    cursor = CursorShape.Fdiagsize;
                    break;
                case "Top":
                    edge = DisplayServer.WindowResizeEdge.Top;
                    cursor = CursorShape.Vsize;
                    break;
                case "TopRight":
                    edge = DisplayServer.WindowResizeEdge.TopRight;
                    cursor = CursorShape.Bdiagsize;
                    break;
                case "Left":
                    edge = DisplayServer.WindowResizeEdge.Left;
                    cursor = CursorShape.Hsize;
                    break;
                case "Right":
                    edge = DisplayServer.WindowResizeEdge.Right;
                    cursor = CursorShape.Hsize;
                    break;
                case "BottomLeft":
                    edge = DisplayServer.WindowResizeEdge.BottomLeft;
                    cursor = CursorShape.Bdiagsize;
                    break;
                case "Bottom":
                    edge = DisplayServer.WindowResizeEdge.Bottom;
                    cursor = CursorShape.Vsize;
                    break;
                case "BottomRight":
                    edge = DisplayServer.WindowResizeEdge.BottomRight;
                    cursor = CursorShape.Fdiagsize;
                    break;
            }
            rect.GuiInput += (inputEvent) =>
            {
                if (inputEvent is InputEventMouseButton && inputEvent.IsPressed())
                {
                    GetWindow().StartResize(edge);
                }
            };
            rect.MouseDefaultCursorShape = cursor;
        }

        _sidebarButton = _headerbar.GetNode<Button>("MarginContainer/HBoxContainer/SidebarButton");
        _sidebarPlaceholder = GetNode<Control>("Content/HBoxContainer/SidebarPlaceholder");
        _sidebar = GetNode<PanelContainer>("Sidebar");
        _sidebarContent = _sidebar.GetNode<PanelContainer>("HBoxContainer/Content");
        _sidebarButton.Toggled += (toggled) =>
        {
            _sidebar.Visible = toggled;
            _sidebarPlaceholder.Visible = toggled;
        };
        _sidebarOverlay = _sidebar.GetNode<ColorRect>("HBoxContainer/Overlay");
        _sidebarOverlay.GuiInput += (inputEvent) =>
        {
            if (inputEvent is InputEventMouseButton && inputEvent.IsPressed())
            {
                _sidebarButton.ButtonPressed = false;
            }
        };
        _craftPageButton = _sidebarContent.GetNode<Button>("MarginContainer/VBoxContainer/CraftPageButton");
        _craftPageButton.Toggled += (toggled) => ActivatePage(_craftPage, toggled);
        _tasksPageButton = _sidebarContent.GetNode<Button>("MarginContainer/VBoxContainer/TasksPageButton");
        _tasksPageButton.Toggled += (toggled) => ActivatePage(_tasksPage, toggled);
        _aboutPageButton = _sidebarContent.GetNode<Button>("MarginContainer/VBoxContainer/AboutPageButton");
        _aboutPageButton.Toggled += (toggled) => ActivatePage(_aboutPage, toggled);

        _backButton = _headerbar.GetNode<Button>("MarginContainer/HBoxContainer/BackButton");
        _backButton.Pressed += () => _backButtonCallback?.Invoke();

        _actionsMenuButton = _headerbar.GetNode<MenuButton>("MarginContainer/HBoxContainer/ActionsMenuButton");
        _actionsMenuButton.GetPopup().IndexPressed += (index) => _menuActions[index]?.Invoke();

        _settingsButton = _headerbar.GetNode<Button>("MarginContainer/HBoxContainer/SettingsButton");
        _settingsPopup = GetNode<PopupPanel>("SettingsPopup");
        _settingsPopup.Visible = false;
        _settingsPopup.PopupHide += Config.Save;
        _settingsButton.Pressed += () => _settingsPopup.PopupCentered();

        _windowControlsLeft = _headerbar.GetNode<HBoxContainer>("MarginContainer/HBoxContainer/WindowControlsLeft");
        _windowControlsRight = _headerbar.GetNode<HBoxContainer>("MarginContainer/HBoxContainer/WindowControlsRight");

        _craftPage = GetNode<CraftPage>("Content/HBoxContainer/Pages/CraftPage");
        _craftPage.BackButtonCallbackChanged += (_, callback) => SetBackButtonCallback(callback);
        _craftPage.MenuActionsChanged += (_, actions) => SetMenuActions(actions);
        _craftPage.Visible = false;

        _tasksPage = GetNode<TasksPage>("Content/HBoxContainer/Pages/TasksPage");
        _tasksPage.CraftRequested += (_, e) =>
        {
            _craftPageButton.ButtonPressed = true;
            _craftPage.ShowRecipe(e.Id, e.Quantity);
        };
        _tasksPage.Visible = false;

        _aboutPage = GetNode<AboutPage>("Content/HBoxContainer/Pages/AboutPage");
        _aboutPage.Visible = false;

        _themeSelection = _settingsPopup.GetNode<OptionButton>("MarginContainer/VBoxContainer/HBoxContainer/ThemeSelection");
        _themeSelection.Select((int)Config.Theme);
        _themeSelection.ItemSelected += (selected) =>
        {
            Config.Theme = (Config.ThemeVariant)selected;
            ChangeTheme();
        };

        _csdCheck = _settingsPopup.GetNode<CheckButton>("MarginContainer/VBoxContainer/CSDCheck");
        if (OS.GetName().Contains("Android"))
        {
            _csdCheck.Visible = false;
        }
        else
        {
            _csdCheck.SetPressedNoSignal(Config.ClientSideDecorations);
            _csdCheck.Toggled += (toggled) =>
            {
                Config.ClientSideDecorations = toggled;
                ChangeDecorations(toggled);
            };
            ChangeDecorations(Config.ClientSideDecorations);
        }

        _uiScaleSpin = _settingsPopup.GetNode<SpinBox>("MarginContainer/VBoxContainer/HBoxContainer2/ScaleSpin");
        _uiScaleSpin.SetValueNoSignal(Config.Scale);
        _uiScaleSpin.ValueChanged += UpdateScale;

        _nonGuaranteedAsBaseCheck = _settingsPopup.GetNode<CheckButton>("MarginContainer/VBoxContainer/NonGuaranteedAsBaseCheck");
        _nonGuaranteedAsBaseCheck.SetPressedNoSignal(Config.TreatNonGuaranteedItemsAsBase);
        _nonGuaranteedAsBaseCheck.Toggled += (toggled) => Config.TreatNonGuaranteedItemsAsBase = toggled;

        ChangeTheme();
        _craftPage.Load();
        _tasksPage.Load();

        var window = GetWindow();
        window.CloseRequested += OnCloseRequested;
        window.GoBackRequested += OnGoBackRequested;
        window.MinSize = new Vector2I(600, 400);
        var configWindowSize = Config.WindowSize;
        if (configWindowSize.X >= window.MinSize.X && configWindowSize.Y >= window.MinSize.Y)
        {
            DisplayServer.WindowSetSize(configWindowSize);
        }
        Resized += AdjustLayout;
        UpdateScale(Config.Scale);

        _craftPageButton.ButtonPressed = true;
        _craftPage.ShowOverview();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev.IsActionReleased("fullscreen"))
        {
            if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            }
            else
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            }
        }
    }

    private void ChangeTheme()
    {
        var isDark = Config.Theme == Config.ThemeVariant.Dark;
        Theme = isDark ? _darkTheme : _lightTheme;
        var normalIconColor = Color.FromHtml(isDark ? "e9dfc4" : "15567e");
        var pressedIconColor = Color.FromHtml(isDark ? "221f2c" : "e9dfc4");
        foreach (var button in new[] { _craftPageButton, _tasksPageButton, _aboutPageButton })
        {
            foreach (var style in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color" })
            {
                button.RemoveThemeColorOverride(style);
            }
            button.AddThemeColorOverride("icon_normal_color", normalIconColor);
            button.AddThemeColorOverride("icon_hover_color", normalIconColor);
            button.AddThemeColorOverride("icon_pressed_color", pressedIconColor);
            button.AddThemeColorOverride("icon_hover_pressed_color", pressedIconColor);
        }
    }

    private void ChangeDecorations(bool clientSide)
    {
        GetWindow().Borderless = clientSide;

        foreach (var child in _windowControlsLeft.GetChildren())
        {
            _windowControlsLeft.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var child in _windowControlsRight.GetChildren())
        {
            _windowControlsRight.RemoveChild(child);
            child.QueueFree();
        }

        var content = GetNode<VBoxContainer>("Content");
        var offset = clientSide ? 4 : 0;
        content.OffsetTop = offset;
        content.OffsetLeft = offset;
        content.OffsetRight = -offset;
        content.OffsetBottom = -offset;
        _headerbar.GetNode<MarginContainer>("MarginContainer").AddThemeConstantOverride("margin_bottom", 4 + offset);
        _headerbar.ResetSize();
        _sidebar.OffsetTop = _headerbar.Size.Y + offset;
        _sidebar.OffsetLeft = offset;
        _sidebar.OffsetRight = -offset;
        _sidebar.OffsetBottom = -offset;

        if (clientSide)
        {
            var controls = WindowManager.GetControlsLayout();
            AddTitlebarControls(controls.Item1, Side.Left);
            AddTitlebarControls(controls.Item2, Side.Right);
        }
    }

    public void AddTitlebarControls(List<WindowManager.WindowControl> controls, Side side)
    {
        var container = side switch
        {
            Side.Left => _windowControlsLeft,
            Side.Right => _windowControlsRight,
            _ => null
        };
        if (container == null)
        {
            return;
        }

        var modulateColor = Color.FromHtml("d3cab1");
        foreach (var control in controls)
        {
            var texture = control switch
            {
                WindowManager.WindowControl.Close => GD.Load<Texture2D>("res://Assets/Close.png"),
                WindowManager.WindowControl.Maximize => GD.Load<Texture2D>("res://Assets/Maximize.png"),
                WindowManager.WindowControl.Minimize => GD.Load<Texture2D>("res://Assets/Minimize.png"),
                _ => null
            };
            if (texture == null)
            {
                continue;
            }

            BaseButton node;
            if (control == WindowManager.WindowControl.Close)
            {
                if (side == Side.Right)
                {
                    var separator = new Control
                    {
                        CustomMinimumSize = new Vector2(6, 0)
                    };
                    container.AddChild(separator);
                }
                node = new TextureButton();
                var textureButton = (TextureButton)node;
                textureButton.TextureNormal = texture;
                textureButton.TexturePressed = texture;
                textureButton.TextureFocused = texture;
                textureButton.TextureHover = texture;
            }
            else
            {
                node = new Button();
                var button = (Button)node;
                button.Flat = true;
                button.Icon = texture;
                button.Modulate = modulateColor;
            }

            switch (control)
            {
                case WindowManager.WindowControl.Minimize:
                    node.Pressed += () => DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized);
                    break;
                case WindowManager.WindowControl.Maximize:
                    node.Pressed += () =>
                    {
                        var maximized = DisplayServer.WindowGetMode().HasFlag(DisplayServer.WindowMode.Maximized);
                        var newMode = maximized ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Maximized;
                        DisplayServer.WindowSetMode(newMode);
                    };
                    break;
                case WindowManager.WindowControl.Close:
                    node.Pressed += () => GetWindow().EmitSignal(Window.SignalName.CloseRequested);
                    break;
            }

            container.AddChild(node);
            if (control == WindowManager.WindowControl.Close && side == Side.Left)
            {
                var separator = new Control
                {
                    CustomMinimumSize = new Vector2(6, 0)
                };
                container.AddChild(separator);
            }
        }
    }

    private void UpdateScale(double scale)
    {
        GetWindow().ContentScaleFactor = (float)scale;
        if (_settingsPopup.Visible)
        {
            _settingsPopup.PopupCentered();
        }
        Config.Scale = scale;
        AdjustLayout();
    }

    private void AdjustLayout()
    {
        var width = Size.X;
        if (width < SIDEBAR_THRESHOLD * Config.Scale)
        {
            _sidebar.AnchorRight = 1.0f;
            _sidebarPlaceholder.CustomMinimumSize = new Vector2(0, 0);
        }
        else
        {
            _sidebar.AnchorRight = 0.0f;
            _sidebarPlaceholder.CustomMinimumSize = new Vector2(_sidebarContent.Size.X, 0);
        }
    }

    private void SetBackButtonCallback(Action callback)
    {
        _backButtonCallback = callback;
        _backButton.Visible = (callback != null);
    }

    private void SetMenuActions(Dictionary<string, Action> actions)
    {
        var menu = _actionsMenuButton.GetPopup();
        menu.Clear();
        foreach (var item in actions.Keys)
        {
            menu.AddItem(item);
        }
        _menuActions = actions.Values.ToArray();
        _actionsMenuButton.Visible = actions.Count > 0;
    }

    private void ActivatePage(IPage page, bool activated)
    {
        page.Visible = activated;
        if (activated)
        {
            SetBackButtonCallback(page.BackButtonCallback);
        }
    }

    private void OnCloseRequested()
    {
        Config.WindowSize = GetWindow().Size;
        Config.Save();
    }

    private void OnGoBackRequested()
    {
        if (_backButton.Visible)
        {
            _backButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
        else
        {
            GetTree().Quit();
        }
    }
}
