using System;
using System.Collections.Generic;
using System.Numerics;
using System.Globalization;
using System.Linq;
using CS2GameHelper.Utils;
using CS2GameHelper.Utils.Registry;
using SkiaSharp;
using Keys = CS2GameHelper.Utils.Keys;

namespace CS2GameHelper.Graphics
{
    public class OverlayMenu : IDisposable
    {
        private readonly UserInputHandler _inputHandler;
        private readonly ModernGraphics _graphics;
        private readonly ConfigManager _config;

        private bool _isVisible;
        private int _selectedCategory = 0;
        private int _selectedItem = -1; // -1 means no item selected (maybe just hovering category)
        private int _selectedSubItem = -1;
        private bool _isInSubMenu = false;
        private bool _editingValue = false;
        private string _editBuffer = "";
        private bool _toggleKeyLastState;
        private DateTime _suppressToggleUntil = DateTime.MinValue;

        private readonly Vector2 _menuPosition = new(100, 100);
        private readonly Vector2 _menuSize = new(700, 450);
        private readonly float _sidebarWidth = 180f;
        private readonly float _headerHeight = 40f;
        private readonly float _categoryHeight = 35f;
        private readonly float _itemHeight = 30f;

        private DateTime _lastKeyPress = DateTime.MinValue;
        private readonly TimeSpan _keyRepeatDelay = TimeSpan.FromMilliseconds(150);

        // Animation states
        private readonly Dictionary<int, float> _categoryHoverFade = new();
        private readonly Dictionary<string, float> _itemHoverFade = new();
        private bool _lastInputWasMouse = false;

        // Categories
        private readonly List<MenuCategory> _categories = new();

        public bool IsVisible => _isVisible;
        public Keys MenuToggleKey => _config.MenuToggleKey;
        public string MenuToggleKeyLabel => FormatKey(_config.MenuToggleKey);

        public OverlayMenu(UserInputHandler inputHandler, ModernGraphics graphics, ConfigManager config)
        {
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            InitializeCategories();
        }

        private void InitializeCategories()
        {
            _categories.Add(new MenuCategory("General", new List<MenuItem>
            {
                new KeybindMenuItem("Menu Key", () => _config.MenuToggleKey, v => { _config.MenuToggleKey = v; }, "Interface"),
                new ActionMenuItem("Save Config", () => _config.SaveCurrent(), "Config Management"),
                new ActionMenuItem("Reload Config", () => _config.ReloadInPlace(), "Config Management"),
                new ActionMenuItem("Reset Defaults", () => _config.ResetDefaults(), "Config Management")
            }));

            _categories.Add(new MenuCategory("AimBot", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.AimBot, v => { _config.AimBot = v; }, "Core"),
                new KeybindMenuItem("Aim Key", () => _config.AimBotKey, v => { _config.AimBotKey = v; }, "Core"),
                new ToggleMenuItem("Auto Shoot", () => _config.AimBotAutoShoot, v => { _config.AimBotAutoShoot = v; }, "Settings"),
                new ToggleMenuItem("Team Check", () => _config.TeamCheck, v => { _config.TeamCheck = v; }, "Settings")
            }));

            _categories.Add(new MenuCategory("RCS", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.Rcs.Enabled, v => { _config.Rcs.Enabled = v; }, "Core"),
                new SliderMenuItem("Global Scale", () => _config.Rcs.GlobalScale, v => { _config.Rcs.GlobalScale = (float)v; }, 0.0, 4.0, 0.1, "0.0", "Settings"),
                new SliderMenuItem("AK-47 Scale",
                    () => _config.Rcs.WeaponScales.TryGetValue("Ak47", out var s) ? s : _config.Rcs.GlobalScale,
                    v => _config.Rcs.WeaponScales["Ak47"] = (float)v, 0.0, 4.0, 0.1, "0.0", "Weapons")
            }));

            _categories.Add(new MenuCategory("TriggerBot", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.TriggerBot, v => { _config.TriggerBot = v; }, "Core"),
                new KeybindMenuItem("Trigger Key", () => _config.TriggerBotKey, v => { _config.TriggerBotKey = v; }, "Core")
            }));

            _categories.Add(new MenuCategory("ESP", new List<MenuItem>
            {
                new ToggleMenuItem("Box Enabled", () => _config.Esp.Box.Enabled, v => { _config.Esp.Box.Enabled = v; }, "Box"),
                new ToggleMenuItem("Show Box", () => _config.Esp.Box.ShowBox, v => { _config.Esp.Box.ShowBox = v; }, "Box"),
                new ToggleMenuItem("Show Name", () => _config.Esp.Box.ShowName, v => { _config.Esp.Box.ShowName = v; }, "Box"),
                new ToggleMenuItem("Show Health Bar", () => _config.Esp.Box.ShowHealthBar, v => { _config.Esp.Box.ShowHealthBar = v; }, "Health"),
                new ToggleMenuItem("Show Health Text", () => _config.Esp.Box.ShowHealthText, v => { _config.Esp.Box.ShowHealthText = v; }, "Health"),
                new SliderMenuItem("Health Position", () => _config.Esp.Box.HealthPosition, v => { _config.Esp.Box.HealthPosition = (int)Math.Round(v); }, 0, 3, 1, "0", "Health"),
                new ToggleMenuItem("Show Distance", () => _config.Esp.Box.ShowDistance, v => { _config.Esp.Box.ShowDistance = v; }, "Details"),
                new ToggleMenuItem("Show Weapon Name", () => _config.Esp.Box.ShowWeaponName, v => { _config.Esp.Box.ShowWeaponName = v; }, "Details"),
                new ToggleMenuItem("Show Armor", () => _config.Esp.Box.ShowArmor, v => { _config.Esp.Box.ShowArmor = v; }, "Details"),
                new ToggleMenuItem("Radar Enabled", () => _config.Esp.Radar.Enabled, v => { _config.Esp.Radar.Enabled = v; }, "Radar"),
                new SliderMenuItem("Radar Size", () => _config.Esp.Radar.Size, v => { _config.Esp.Radar.Size = (int)Math.Round(v); }, 50, 300, 5, "0", "Radar"),
                new ToggleMenuItem("Crosshair Enabled", () => _config.Esp.AimCrosshair.Enabled, v => { _config.Esp.AimCrosshair.Enabled = v; }, "Crosshair")
            }));

            _categories.Add(new MenuCategory("Visuals", new List<MenuItem>
            {
                new ToggleMenuItem("Skeleton ESP", () => _config.SkeletonEsp, v => { _config.SkeletonEsp = v; }, "World"),
                new ToggleMenuItem("Bomb Timer", () => _config.BombTimer, v => { _config.BombTimer = v; }, "World"),
                new ToggleMenuItem("Spectator List", () => _config.SpectatorList.Enabled, v => { _config.SpectatorList.Enabled = v; }, "UI"),
                new ToggleMenuItem("Vote Teller", () => _config.VoteTeller.Enabled, v => { _config.VoteTeller.Enabled = v; }, "UI")
            }));

            _categories.Add(new MenuCategory("Hit Sound", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.HitSound.Enabled, v => { _config.HitSound.Enabled = v; }, "Core"),
                new SliderMenuItem("Text Duration", () => _config.HitSound.TextDurationSeconds, v => { _config.HitSound.TextDurationSeconds = v; }, 0.5, 3.0, 0.1, "0.0", "Settings"),
                new SliderMenuItem("HS Threshold", () => _config.HitSound.HeadshotDamageThreshold, v => { _config.HitSound.HeadshotDamageThreshold = (int)Math.Round(v); }, 50, 200, 5, "0", "Settings")
            }));

            var registryItems = new List<MenuItem>();
            foreach (var feature in FeatureRegistry.Features)
            {
                registryItems.Add(new ToggleMenuItem(feature.DisplayName,
                    () => feature.Enabled,
                    v => {
                        if (v != feature.Enabled) ManagementList.ToggleFeature(feature);
                    }, feature.Category));
            }
            _categories.Add(new MenuCategory("Registry", registryItems));

            _categories.Add(new MenuCategory("Stats/Log", new List<MenuItem>()));
        }

        public void Toggle()
        {
            Toggle(DateTime.Now);
        }

        private void Toggle(DateTime now)
        {
            _isVisible = !_isVisible;
            if (!_isVisible)
            {
                _isInSubMenu = false;
                _editingValue = false;
                _editBuffer = "";
                SaveConfig();
            }

            _suppressToggleUntil = now.AddMilliseconds(250);
            _toggleKeyLastState = true;
            _lastKeyPress = now;
        }

        public void Update()
        {
            var now = DateTime.Now;
            HandleToggle(now);
            if (!_isVisible) return;

            UpdateAnimations();
            HandleMouseInput(now);
            HandleKeyboardInput(now);
        }

        private void UpdateAnimations()
        {
            float delta = 0.05f; // Animation speed

            for (int i = 0; i < _categories.Count; i++)
            {
                bool target = i == _selectedCategory;
                _categoryHoverFade[i] = Lerp(_categoryHoverFade.GetValueOrDefault(i, 0), target ? 1f : 0f, delta);
            }

            var currentCategory = _categories[_selectedCategory];
            for (int i = 0; i < currentCategory.Items.Count; i++)
            {
                var item = currentCategory.Items[i];
                bool target = i == _selectedItem && !_isInSubMenu;
                _itemHoverFade[item.Name] = Lerp(_itemHoverFade.GetValueOrDefault(item.Name, 0), target ? 1f : 0f, delta);
            }
        }

        private float Lerp(float a, float b, float t) => a + (b - a) * t;

        private void HandleToggle(DateTime now)
        {
            var toggleKey = _config.MenuToggleKey;
            if (toggleKey == Keys.None)
            {
                _toggleKeyLastState = false;
                return;
            }

            var isDown = _inputHandler.IsKeyDown(toggleKey);

            if (_editingValue)
            {
                _toggleKeyLastState = isDown;
                return;
            }

            if (now < _suppressToggleUntil)
            {
                _toggleKeyLastState = isDown;
                return;
            }

            if (isDown && !_toggleKeyLastState)
            {
                Toggle(now);
            }

            _toggleKeyLastState = isDown;
        }

        private void HandleMouseInput(DateTime now)
        {
            var mousePos = _inputHandler.MousePosition;
            var windowRect = _graphics.GameProcess.WindowRectangleClient;
            var relativeMouse = new Vector2(mousePos.X - windowRect.X - _menuPosition.X, mousePos.Y - windowRect.Y - _menuPosition.Y);

            // Check if mouse moved
            if (_inputHandler.LastMouseDelta.X != 0 || _inputHandler.LastMouseDelta.Y != 0)
            {
                _lastInputWasMouse = true;
            }

            if (!_lastInputWasMouse && !_inputHandler.IsLeftMouseDown) return;

            // Handle Sidebar Hover
            if (relativeMouse.X >= 0 && relativeMouse.X <= _sidebarWidth &&
                relativeMouse.Y >= _headerHeight && relativeMouse.Y <= _menuSize.Y)
            {
                int catIdx = (int)((relativeMouse.Y - _headerHeight) / _categoryHeight);
                if (catIdx >= 0 && catIdx < _categories.Count)
                {
                    if (_selectedCategory != catIdx)
                    {
                        _selectedCategory = catIdx;
                        _selectedItem = -1;
                        _isInSubMenu = false;
                    }
                }
            }

            // Handle Content Hover
            float contentX = _sidebarWidth + 20;
            if (relativeMouse.X >= contentX && relativeMouse.X <= _menuSize.X - 20)
            {
                var currentCategory = _categories[_selectedCategory];
                float startY = _headerHeight + 20 + 35; // Origin + Header height

                int hoveredItem = -1;
                float currentY = startY;

                var itemsBySection = currentCategory.Items.GroupBy(i => i.Section).ToList();
                int totalIdx = 0;
                foreach (var section in itemsBySection)
                {
                    currentY += 20; // Section header
                    for (int i = 0; i < section.Count(); i++)
                    {
                        if (relativeMouse.Y >= currentY - 20 && relativeMouse.Y <= currentY + 10)
                        {
                            hoveredItem = totalIdx;
                        }
                        currentY += _itemHeight;
                        totalIdx++;
                    }
                }
                _selectedItem = hoveredItem;
            }

            // Handle Clicks
            if (_inputHandler.IsLeftMouseDown && now > _suppressToggleUntil)
            {
                if (_selectedItem != -1)
                {
                    var item = _categories[_selectedCategory].Items[_selectedItem];
                    if (item is ToggleMenuItem toggle) toggle.Toggle();
                    else if (item is ActionMenuItem action) action.Invoke();
                    else if (item is KeybindMenuItem) _editingValue = true;

                    _suppressToggleUntil = now.AddMilliseconds(300); // Proper debounce
                }
            }
        }

        private void HandleKeyboardInput(DateTime now)
        {
            if (now - _lastKeyPress < _keyRepeatDelay) return;

            bool keyPressed = false;

            if (_editingValue)
            {
                HandleValueEditing(now);
                return;
            }

            if (_inputHandler.IsKeyDown(Keys.Up))
            {
                _selectedItem = Math.Max(0, _selectedItem - 1);
                _lastInputWasMouse = false;
                keyPressed = true;
            }
            if (_inputHandler.IsKeyDown(Keys.Down))
            {
                var currentCategory = _categories[_selectedCategory];
                _selectedItem = Math.Min(currentCategory.Items.Count - 1, _selectedItem + 1);
                _lastInputWasMouse = false;
                keyPressed = true;
            }
            if (_inputHandler.IsKeyDown(Keys.Left))
            {
                // Only switch categories if not currently hovering a slider or if using CTRL+Arrows
                bool isSlider = _selectedItem != -1 && _categories[_selectedCategory].Items[_selectedItem] is SliderMenuItem;
                if (!isSlider)
                {
                    _selectedCategory = Math.Max(0, _selectedCategory - 1);
                    _selectedItem = 0;
                    _lastInputWasMouse = false;
                    keyPressed = true;
                }
            }
            if (_inputHandler.IsKeyDown(Keys.Right))
            {
                bool isSlider = _selectedItem != -1 && _categories[_selectedCategory].Items[_selectedItem] is SliderMenuItem;
                if (!isSlider)
                {
                    _selectedCategory = Math.Min(_categories.Count - 1, _selectedCategory + 1);
                    _selectedItem = 0;
                    _lastInputWasMouse = false;
                    keyPressed = true;
                }
            }

            if (_inputHandler.IsKeyDown(Keys.Return) || _inputHandler.IsKeyDown(Keys.Space))
            {
                if (_selectedItem != -1)
                {
                    var item = _categories[_selectedCategory].Items[_selectedItem];
                    if (item is ToggleMenuItem toggle) toggle.Toggle();
                    else if (item is ActionMenuItem action) action.Invoke();
                    else if (item is KeybindMenuItem) _editingValue = true;
                }
                keyPressed = true;
            }

            // Slider control with keyboard
            if (_selectedItem != -1)
            {
                var item = _categories[_selectedCategory].Items[_selectedItem];
                if (item is SliderMenuItem slider)
                {
                    if (_inputHandler.IsKeyDown(Keys.Left))
                    {
                        slider.Decrement();
                        keyPressed = true;
                    }
                    else if (_inputHandler.IsKeyDown(Keys.Right))
                    {
                        slider.Increment();
                        keyPressed = true;
                    }
                }
            }

            if (keyPressed) _lastKeyPress = now;
        }

        private void HandleValueEditing(DateTime now)
        {
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key == Keys.Insert || key == Keys.Escape || key == Keys.Return) continue;
                if (_inputHandler.IsKeyDown(key))
                {
                    var item = _categories[_selectedCategory].Items[_selectedItem];
                    if (item is KeybindMenuItem kb) kb.SetValue(key);
                    _editingValue = false;
                    _lastKeyPress = now;
                    break;
                }
            }
        }

        public void Render()
        {
            if (!_isVisible) return;

            // Colors (GitHub Dark Theme)
            uint colorBg = 0xFD0D1117;
            uint colorSidebar = 0xFF161B22;
            uint colorBorder = 0xFF30363D;
            uint colorPrimary = 0xFF58A6FF;
            uint colorText = 0xFFC9D1D9;
            uint colorTextDim = 0xFF8B949E;
            uint colorAccent = 0xFF2EA043;

            // Draw Background
            _graphics.DrawRectangle(colorBg, _menuPosition, _menuSize.X, _menuSize.Y);
            _graphics.DrawRectangleOutline(colorBorder, _menuPosition, _menuSize.X, _menuSize.Y);

            // Draw Sidebar
            _graphics.DrawRectangle(colorSidebar, _menuPosition, _sidebarWidth, _menuSize.Y);
            _graphics.DrawLine(colorBorder,
                new Vector2(_menuPosition.X + _sidebarWidth, _menuPosition.Y),
                new Vector2(_menuPosition.X + _sidebarWidth, _menuPosition.Y + _menuSize.Y));

            // Draw Header
            _graphics.DrawLine(colorBorder,
                new Vector2(_menuPosition.X, _menuPosition.Y + _headerHeight),
                new Vector2(_menuPosition.X + _menuSize.X, _menuPosition.Y + _headerHeight));
            _graphics.DrawText("CS2 MANAGEMENT", _menuPosition.X + 15, _menuPosition.Y + 28, colorPrimary, 18, false);

            // Draw Sidebar Categories
            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                float fade = _categoryHoverFade.GetValueOrDefault(i, 0f);
                var yPos = _menuPosition.Y + _headerHeight + (i * _categoryHeight);

                if (fade > 0.01f)
                {
                    uint alpha = (uint)(fade * 64);
                    _graphics.DrawRectangle((alpha << 24) | (colorPrimary & 0x00FFFFFF),
                        new Vector2(_menuPosition.X, yPos), _sidebarWidth, _categoryHeight);
                    _graphics.DrawRectangle(colorPrimary, new Vector2(_menuPosition.X, yPos), 4, _categoryHeight);
                }

                _graphics.DrawText(category.Name.ToUpper(), _menuPosition.X + 20, yPos + 23,
                    fade > 0.5f ? 0xFFFFFFFF : colorText, 13, false);
            }

            // Draw Content
            var contentOrigin = new Vector2(_menuPosition.X + _sidebarWidth + 20, _menuPosition.Y + _headerHeight + 20);
            var currentCategory = _categories[_selectedCategory];

            if (currentCategory.Name == "Stats/Log")
            {
                _graphics.DrawText("RECENT ACTIVITY LOG", contentOrigin.X, contentOrigin.Y + 10, colorPrimary, 12, false);
                var logs = ManagementList.Logs;
                for (int i = 0; i < Math.Min(logs.Count, 15); i++)
                {
                    _graphics.DrawText($"[{logs[i].Timestamp}] {logs[i].Message}", contentOrigin.X, contentOrigin.Y + 40 + (i * 20), colorText, 10);
                }
                return;
            }

            // Table Header
            _graphics.DrawText("FEATURE", contentOrigin.X + 10, contentOrigin.Y + 12, colorPrimary, 10, true);
            _graphics.DrawText("VALUE", contentOrigin.X + 280, contentOrigin.Y + 12, colorPrimary, 10, true);
            _graphics.DrawText("CONTROL", contentOrigin.X + 380, contentOrigin.Y + 12, colorPrimary, 10, true);
            _graphics.DrawLine(colorBorder, contentOrigin + new Vector2(0, 20), contentOrigin + new Vector2(_menuSize.X - _sidebarWidth - 40, 20));

            var itemsBySection = currentCategory.Items.GroupBy(i => i.Section).ToList();
            float currentY = contentOrigin.Y + 35;
            int totalIdx = 0;

            foreach (var section in itemsBySection)
            {
                _graphics.DrawText(section.Key.ToUpper(), contentOrigin.X + 5, currentY, colorTextDim, 9, true);
                currentY += 20;

                foreach (var item in section)
                {
                    bool isSelected = totalIdx == _selectedItem;
                    float fade = _itemHoverFade.GetValueOrDefault(item.Name, 0f);

                    if (fade > 0.01f)
                    {
                        uint alpha = (uint)(fade * 30);
                        _graphics.DrawRectangle((alpha << 24) | (colorPrimary & 0x00FFFFFF),
                            new Vector2(contentOrigin.X - 5, currentY - 18), _menuSize.X - _sidebarWidth - 40, _itemHeight);
                    }

                    _graphics.DrawText(item.Name, contentOrigin.X + 10, currentY, isSelected ? 0xFFFFFFFF : colorText, 11);

                    string val = item.GetValue();
                    uint valColor = val == "ON" ? 0xFF00FF00 : (val == "OFF" ? 0xFFFF0000 : colorPrimary);
                    _graphics.DrawText(val, contentOrigin.X + 280, currentY, valColor, 11);

                    // Render control hint
                    if (item is SliderMenuItem)
                        _graphics.DrawText("[ < / > ]", contentOrigin.X + 380, currentY, colorTextDim, 9);
                    else if (item is KeybindMenuItem)
                        _graphics.DrawText("[ PRESS ]", contentOrigin.X + 380, currentY, colorTextDim, 9);
                    else if (item is ActionMenuItem)
                        _graphics.DrawText("[ ENTER ]", contentOrigin.X + 380, currentY, 0xFFFFA500, 9);

                    currentY += _itemHeight;
                    totalIdx++;
                }
                currentY += 10;
            }

            if (_editingValue)
            {
                _graphics.DrawRectangle(0xCC000000, _menuPosition, _menuSize.X, _menuSize.Y);
                _graphics.DrawText("PRESS ANY KEY TO BIND...", _menuPosition.X + (_menuSize.X / 2) - 100, _menuPosition.Y + (_menuSize.Y / 2), 0xFFFFFFFF, 14, true);
            }
        }

        private void SaveConfig() => ConfigManager.Save(_config);
        public void Dispose() => SaveConfig();

        internal static string FormatKey(Keys key)
        {
            return key switch
            {
                Keys.LButton => "Mouse 1",
                Keys.RButton => "Mouse 2",
                Keys.XButton1 => "Mouse 4",
                Keys.XButton2 => "Mouse 5",
                _ => key.ToString()
            };
        }
    }

    public abstract class MenuItem
    {
        public string Name { get; }
        public string Section { get; }
        protected MenuItem(string name, string section) { Name = name; Section = section; }
        public abstract string GetValue();
    }

    public class ToggleMenuItem : MenuItem
    {
        private readonly Func<bool> _getter;
        private readonly Action<bool> _setter;
        public ToggleMenuItem(string n, Func<bool> g, Action<bool> s, string sec) : base(n, sec) { _getter = g; _setter = s; }
        public void Toggle() => _setter(!_getter());
        public override string GetValue() => _getter() ? "ON" : "OFF";
    }

    public class KeybindMenuItem : MenuItem
    {
        private readonly Func<Keys> _getter;
        private readonly Action<Keys> _setter;
        public KeybindMenuItem(string n, Func<Keys> g, Action<Keys> s, string sec) : base(n, sec) { _getter = g; _setter = s; }
        public void SetValue(Keys k) => _setter(k);
        public override string GetValue() => OverlayMenu.FormatKey(_getter());
    }

    public class SliderMenuItem : MenuItem
    {
        private readonly Func<double> _getter;
        private readonly Action<double> _setter;
        private readonly double _min, _max, _step;
        private readonly string _fmt;
        public SliderMenuItem(string n, Func<double> g, Action<double> s, double min, double max, double step, string fmt, string sec) : base(n, sec)
        { _getter = g; _setter = s; _min = min; _max = max; _step = step; _fmt = fmt; }
        public void Increment() { _setter(Math.Min(_max, _getter() + _step)); }
        public void Decrement() { _setter(Math.Max(_min, _getter() - _step)); }
        public override string GetValue() => _getter().ToString(_fmt);
    }

    public class ActionMenuItem : MenuItem
    {
        private readonly Func<bool> _action;
        public ActionMenuItem(string n, Func<bool> a, string sec) : base(n, sec) { _action = a; }
        public void Invoke() => _action();
        public override string GetValue() => "▶";
    }

    public class MenuCategory
    {
        public string Name { get; }
        public List<MenuItem> Items { get; }
        public MenuCategory(string name, List<MenuItem> items) { Name = name; Items = items; }
    }
}
