using System;
using System.Collections.Generic;
using System.Numerics;
using System.Globalization;
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
        private int _selectedItem = 0;
        private int _selectedSubItem = 0;
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
        
        // Категории меню
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
                new KeybindMenuItem("Menu Key", () => _config.MenuToggleKey, v => { _config.MenuToggleKey = v; }),
                new ActionMenuItem("Save Config", () => _config.SaveCurrent()),
                new ActionMenuItem("Reload Config", () => _config.ReloadInPlace()),
                new ActionMenuItem("Reset Defaults", () => _config.ResetDefaults())
            }));

            _categories.Add(new MenuCategory("AimBot", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.AimBot, v => { _config.AimBot = v; }),
                new ToggleMenuItem("Auto Shoot", () => _config.AimBotAutoShoot, v => { _config.AimBotAutoShoot = v; }),
                new KeybindMenuItem("Key", () => _config.AimBotKey, v => { _config.AimBotKey = v; }),
                new ToggleMenuItem("Team Check", () => _config.TeamCheck, v => { _config.TeamCheck = v; })
            }));
            
            _categories.Add(new MenuCategory("TriggerBot", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.TriggerBot, v => { _config.TriggerBot = v; }),
                new KeybindMenuItem("Key", () => _config.TriggerBotKey, v => { _config.TriggerBotKey = v; })
            }));
            
            _categories.Add(new MenuCategory("ESP", new List<MenuItem>
            {
                new SubMenuItem("Box ESP", new List<MenuItem>
                {
                    new ToggleMenuItem("Enabled", () => _config.Esp.Box.Enabled, v => { _config.Esp.Box.Enabled = v; }),
                    new ToggleMenuItem("Show Name", () => _config.Esp.Box.ShowName, v => { _config.Esp.Box.ShowName = v; }),
                    new ToggleMenuItem("Show Health Bar", () => _config.Esp.Box.ShowHealthBar, v => { _config.Esp.Box.ShowHealthBar = v; }),
                    new ToggleMenuItem("Show Health Text", () => _config.Esp.Box.ShowHealthText, v => { _config.Esp.Box.ShowHealthText = v; }),
                    new ToggleMenuItem("Show Distance", () => _config.Esp.Box.ShowDistance, v => { _config.Esp.Box.ShowDistance = v; }),
                    new ToggleMenuItem("Show Weapon", () => _config.Esp.Box.ShowWeaponIcon, v => { _config.Esp.Box.ShowWeaponIcon = v; }),
                    new ToggleMenuItem("Show Armor", () => _config.Esp.Box.ShowArmor, v => { _config.Esp.Box.ShowArmor = v; }),
                    new ToggleMenuItem("Show Visibility", () => _config.Esp.Box.ShowVisibilityIndicator, v => { _config.Esp.Box.ShowVisibilityIndicator = v; }),
                    new ToggleMenuItem("Show Flags", () => _config.Esp.Box.ShowFlags, v => { _config.Esp.Box.ShowFlags = v; })
                }),
                new SubMenuItem("Radar", new List<MenuItem>
                {
                    new ToggleMenuItem("Enabled", () => _config.Esp.Radar.Enabled, v => { _config.Esp.Radar.Enabled = v; }),
                    new ToggleMenuItem("Show Local Player", () => _config.Esp.Radar.ShowLocalPlayer, v => { _config.Esp.Radar.ShowLocalPlayer = v; }),
                    new ToggleMenuItem("Show Direction Arrow", () => _config.Esp.Radar.ShowDirectionArrow, v => { _config.Esp.Radar.ShowDirectionArrow = v; }),
                    new SliderMenuItem("Size", () => _config.Esp.Radar.Size, v => { _config.Esp.Radar.Size = (int)Math.Round(v); }, 50, 300, 5),
                    new SliderMenuItem("X Position", () => _config.Esp.Radar.X, v => { _config.Esp.Radar.X = (int)Math.Round(v); }, 0, 500, 5),
                    new SliderMenuItem("Y Position", () => _config.Esp.Radar.Y, v => { _config.Esp.Radar.Y = (int)Math.Round(v); }, 0, 500, 5),
                    new SliderMenuItem("Max Distance", () => _config.Esp.Radar.MaxDistance, v => { _config.Esp.Radar.MaxDistance = (float)v; }, 50, 500, 10, "0")
                }),
                new SubMenuItem("Aim Crosshair", new List<MenuItem>
                {
                    new ToggleMenuItem("Enabled", () => _config.Esp.AimCrosshair.Enabled, v => { _config.Esp.AimCrosshair.Enabled = v; }),
                    new SliderMenuItem("Radius", () => _config.Esp.AimCrosshair.Radius, v => { _config.Esp.AimCrosshair.Radius = (int)Math.Round(v); }, 1, 20, 1, "0"),
                    new SliderMenuItem("Recoil Scale", () => _config.Esp.AimCrosshair.RecoilScale, v => { _config.Esp.AimCrosshair.RecoilScale = (float)v; }, 0.5, 5, 0.1, "0.0"),
                    new ToggleMenuItem("FOV Circle", () => _config.Esp.AimCrosshair.ShowFovCircle, v => { _config.Esp.AimCrosshair.ShowFovCircle = v; }),
                    new SliderMenuItem("FOV Radius", () => _config.Esp.AimCrosshair.FovCircleRadius, v => { _config.Esp.AimCrosshair.FovCircleRadius = (int)Math.Round(v); }, 20, 600, 10, "0")
                })
            }));
            
            _categories.Add(new MenuCategory("Visuals", new List<MenuItem>
            {
                new ToggleMenuItem("Skeleton ESP", () => _config.SkeletonEsp, v => { _config.SkeletonEsp = v; }),
                new ToggleMenuItem("Bomb Timer", () => _config.BombTimer, v => { _config.BombTimer = v; }),
                new ToggleMenuItem("Spectator List", () => _config.SpectatorList.Enabled, v => { _config.SpectatorList.Enabled = v; }),
                new SubMenuItem("Vote Teller", new List<MenuItem>
                {
                    new ToggleMenuItem("Enabled", () => _config.VoteTeller.Enabled, v => { _config.VoteTeller.Enabled = v; }),
                    new SliderMenuItem("X Position", () => _config.VoteTeller.X, v => { _config.VoteTeller.X = (int)Math.Round(v); }, 0, 1920, 5),
                    new SliderMenuItem("Y Position", () => _config.VoteTeller.Y, v => { _config.VoteTeller.Y = (int)Math.Round(v); }, 0, 1080, 5)
                })
            }));
            
            _categories.Add(new MenuCategory("Hit Sound", new List<MenuItem>
            {
                new ToggleMenuItem("Enabled", () => _config.HitSound.Enabled, v => { _config.HitSound.Enabled = v; }),
                new SliderMenuItem("Text Duration", () => _config.HitSound.TextDurationSeconds, v => { _config.HitSound.TextDurationSeconds = v; }, 0.5, 3.0, 0.1, "0.0"),
                new SliderMenuItem("Headshot Threshold", () => _config.HitSound.HeadshotDamageThreshold, v => { _config.HitSound.HeadshotDamageThreshold = (int)Math.Round(v); }, 50, 200, 5, "0")
            }));

            var registryItems = new List<MenuItem>();
            foreach (var feature in FeatureRegistry.Features)
            {
                registryItems.Add(new ToggleMenuItem(feature.DisplayName,
                    () => feature.Enabled,
                    v => {
                        if (v != feature.Enabled) ManagementList.ToggleFeature(feature);
                    }));
            }

            _categories.Add(new MenuCategory("Registry", registryItems));

            _categories.Add(new MenuCategory("Configs", new List<MenuItem>
            {
                new ActionMenuItem("Export to Clip", () => {
                    var str = ConfigManager.Export(_config);
                    // System.Windows.Forms.Clipboard.SetText(str);
                    ManagementList.AddLog("Config exported.");
                    return true;
                }),
                new ActionMenuItem("Import from Clip", () => {
                    ManagementList.AddLog("Import not supported in headless mode.");
                    return false;
                })
            }));

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

            HandleInput(now);
        }

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

        private void HandleInput(DateTime now)
        {
            if (now - _lastKeyPress < _keyRepeatDelay) return;
            
            if (_editingValue)
            {
                HandleValueEditing(now);
                return;
            }
            
            // Navigation
            if (_inputHandler.IsKeyDown(Keys.Up))
            {
                if (_isInSubMenu)
                    _selectedSubItem = Math.Max(0, _selectedSubItem - 1);
                else
                    _selectedItem = Math.Max(0, _selectedItem - 1);
                _lastKeyPress = now;
            }
            
            if (_inputHandler.IsKeyDown(Keys.Down))
            {
                if (_isInSubMenu)
                {
                    var currentItems = GetCurrentSubItems();
                    _selectedSubItem = Math.Min(currentItems.Count - 1, _selectedSubItem + 1);
                }
                else
                {
                    var currentCategory = _categories[_selectedCategory];
                    _selectedItem = Math.Min(currentCategory.Items.Count - 1, _selectedItem + 1);
                }
                _lastKeyPress = now;
            }
            
            if (_inputHandler.IsKeyDown(Keys.Left))
            {
                _selectedCategory = Math.Max(0, _selectedCategory - 1);
                _selectedItem = 0;
                _selectedSubItem = 0;
                _isInSubMenu = false;
                _lastKeyPress = now;
            }
            
            if (_inputHandler.IsKeyDown(Keys.Right))
            {
                _selectedCategory = Math.Min(_categories.Count - 1, _selectedCategory + 1);
                _selectedItem = 0;
                _selectedSubItem = 0;
                _isInSubMenu = false;
                _lastKeyPress = now;
            }
            
            if (_inputHandler.IsKeyDown(Keys.Return) || _inputHandler.IsKeyDown(Keys.Space))
            {
                var currentCategory = _categories[_selectedCategory];
                var currentItem = currentCategory.Items[_selectedItem];
                
                if (currentItem is SubMenuItem subItem && !_isInSubMenu)
                {
                    _isInSubMenu = true;
                    _selectedSubItem = 0;
                }
                else if (currentItem is ToggleMenuItem toggleItem)
                {
                    toggleItem.Toggle();
                    _lastKeyPress = now;
                }
                else if (currentItem is KeybindMenuItem keybindItem)
                {
                    _editingValue = true;
                    _editBuffer = "Press any key...";
                    _lastKeyPress = now;
                }
                else if (currentItem is SliderMenuItem sliderItem)
                {
                    sliderItem.Increment();
                    _lastKeyPress = now;
                }
                else if (currentItem is ActionMenuItem actionItem)
                {
                    actionItem.Invoke();
                    _lastKeyPress = now;
                }
            }
            
            if (_inputHandler.IsKeyDown(Keys.Escape))
            {
                if (_isInSubMenu)
                {
                    _isInSubMenu = false;
                    _selectedSubItem = 0;
                }
                else
                {
                    _isVisible = false;
                    SaveConfig();
                    _suppressToggleUntil = now.AddMilliseconds(250);
                    _toggleKeyLastState = true;
                }
                _lastKeyPress = now;
            }
            
            // Handle slider value changes with left/right when in submenu
            if (_isInSubMenu)
            {
                var subItems = GetCurrentSubItems();
                if (_selectedSubItem < subItems.Count)
                {
                    var subItem = subItems[_selectedSubItem];
                    if (subItem is SliderMenuItem slider)
                    {
                        if (_inputHandler.IsKeyDown(Keys.Left))
                        {
                            slider.Decrement();
                            _lastKeyPress = now;
                        }
                        else if (_inputHandler.IsKeyDown(Keys.Right))
                        {
                            slider.Increment();
                            _lastKeyPress = now;
                        }
                    }
                }
            }
        }
        
        private void HandleValueEditing(DateTime now)
        {
            // Check for any key press except modifiers
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key == Keys.Insert || key == Keys.Escape || key == Keys.Return || key == Keys.Space ||
                    key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right)
                    continue;
                    
                if (_inputHandler.IsKeyDown(key))
                {
                    var currentCategory = _categories[_selectedCategory];
                    var currentItem = currentCategory.Items[_selectedItem];
                    if (currentItem is KeybindMenuItem keybindItem)
                    {
                        keybindItem.SetValue(key);
                        _editingValue = false;
                        _editBuffer = "";
                        _lastKeyPress = now;
                        _suppressToggleUntil = now.AddMilliseconds(250);
                        _toggleKeyLastState = true;
                    }
                    break;
                }
            }
            
            if (_inputHandler.IsKeyDown(Keys.Escape))
            {
                _editingValue = false;
                _editBuffer = "";
                _lastKeyPress = now;
            }
        }
        
        private List<MenuItem> GetCurrentSubItems()
        {
            if (!_isInSubMenu) return new List<MenuItem>();
            
            var currentCategory = _categories[_selectedCategory];
            var currentItem = currentCategory.Items[_selectedItem];
            
            if (currentItem is SubMenuItem subItem)
                return subItem.SubItems;
                
            return new List<MenuItem>();
        }
        
        public void Render()
        {
            if (!_isVisible) return;
            
            // Colors (GitHub Dark Theme)
            uint colorBg = 0xFD0D1117;        // #0D1117 (99% Alpha)
            uint colorSidebar = 0xFF161B22;   // #161B22
            uint colorBorder = 0xFF30363D;    // #30363D
            uint colorAccent = 0xFF2EA043;    // #2EA043 (Green)
            uint colorPrimary = 0xFF58A6FF;   // #58A6FF (Blue)
            uint colorText = 0xFFC9D1D9;      // #C9D1D9
            uint colorTextDim = 0xFF8B949E;   // #8B949E
            uint colorHighlight = 0xFFFFFFFF; // White
            
            // Draw Main Background
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
            _graphics.DrawText("CS2 MANAGEMENT", _menuPosition.X + 15, _menuPosition.Y + 28, colorPrimary, 18, true);

            // Draw Sidebar Categories
            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                var yPos = _menuPosition.Y + _headerHeight + (i * _categoryHeight);
                bool isSelected = i == _selectedCategory;

                if (isSelected)
                {
                    _graphics.DrawRectangle(0x4058A6FF,
                        new Vector2(_menuPosition.X, yPos), _sidebarWidth, _categoryHeight);
                    _graphics.DrawRectangle(colorPrimary,
                        new Vector2(_menuPosition.X, yPos), 4, _categoryHeight);
                }

                _graphics.DrawText(category.Name.ToUpper(), _menuPosition.X + 20, yPos + 23,
                    isSelected ? colorHighlight : colorText, 13, isSelected);
            }
            
            // Draw Content Area (Professional Table Layout)
            var contentOrigin = new Vector2(_menuPosition.X + _sidebarWidth + 20, _menuPosition.Y + _headerHeight + 20);
            var currentCategory = _categories[_selectedCategory];

            if (currentCategory.Name == "Stats/Log")
            {
                _graphics.DrawText("RECENT ACTIVITY LOG", contentOrigin.X, contentOrigin.Y + 10, colorPrimary, 12, true);
                var logs = ManagementList.Logs;
                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    _graphics.DrawText($"[{log.Timestamp}]", contentOrigin.X, contentOrigin.Y + 40 + (i * 20), colorTextDim, 10);
                    _graphics.DrawText(log.Message, contentOrigin.X + 70, contentOrigin.Y + 40 + (i * 20), colorText, 10);
                }
                return;
            }

            // Table Header
            uint colorTableHeader = colorSidebar;
            _graphics.DrawRectangle(colorTableHeader, contentOrigin - new Vector2(5, 5), _menuSize.X - _sidebarWidth - 30, 25);
            _graphics.DrawText("FEATURE NAME", contentOrigin.X + 10, contentOrigin.Y + 12, colorPrimary, 10, true);
            _graphics.DrawText("VALUE", contentOrigin.X + 280, contentOrigin.Y + 12, colorPrimary, 10, true);
            _graphics.DrawText("KEYBIND", contentOrigin.X + 420, contentOrigin.Y + 12, colorPrimary, 10, true);

            for (int i = 0; i < currentCategory.Items.Count; i++)
            {
                var item = currentCategory.Items[i];
                var yOffset = 35 + (i * _itemHeight);
                bool isSelected = i == _selectedItem && !_isInSubMenu;

                if (isSelected)
                {
                    _graphics.DrawRectangle(ToUint(SKColors.Cyan.WithAlpha(30)),
                        new Vector2(contentOrigin.X - 5, contentOrigin.Y + yOffset - 20),
                        _menuSize.X - _sidebarWidth - 30, _itemHeight);
                    _graphics.DrawText(">", contentOrigin.X - 2, contentOrigin.Y + yOffset, colorHighlight, 11, true);
                }

                uint itemColor = isSelected ? colorHighlight : colorText;
                _graphics.DrawText(item.Name, contentOrigin.X + 10, contentOrigin.Y + yOffset, itemColor, 11);

                string val = item.GetValue();
                uint valColor = val == "ON" ? ToUint(SKColors.Lime) : (val == "OFF" ? ToUint(SKColors.Red) : (isSelected ? colorHighlight : colorAccent));
                _graphics.DrawText(val, contentOrigin.X + 280, contentOrigin.Y + yOffset, valColor, 11);

                // Show keybind if it's a KeybindMenuItem
                if (item is KeybindMenuItem)
                {
                    _graphics.DrawText(val, contentOrigin.X + 420, contentOrigin.Y + yOffset, colorTextDim, 10);
                }
                else if (item is ActionMenuItem)
                {
                    _graphics.DrawText("[EXECUTE]", contentOrigin.X + 420, contentOrigin.Y + yOffset, ToUint(SKColors.OrangeRed), 10);
                }
                else
                {
                    _graphics.DrawText("-", contentOrigin.X + 420, contentOrigin.Y + yOffset, colorTextDim, 10);
                }

                // Row Separator
                _graphics.DrawLine(ToUint(SKColors.Gray.WithAlpha(40)),
                    new Vector2(contentOrigin.X - 5, contentOrigin.Y + yOffset + 10),
                    new Vector2(contentOrigin.X + _menuSize.X - _sidebarWidth - 35, contentOrigin.Y + yOffset + 10));
            }
            
            // Draw Submenu if active
            if (_isInSubMenu)
            {
                var subItems = GetCurrentSubItems();
                var subMenuOrigin = contentOrigin + new Vector2(480, 0);

                // Submenu Background
                _graphics.DrawRectangle(ToUint(SKColors.Black.WithAlpha(180)),
                    subMenuOrigin - new Vector2(10, 10), 180, (subItems.Count * _itemHeight) + 40);
                _graphics.DrawRectangleOutline(colorBorder,
                    subMenuOrigin - new Vector2(10, 10), 180, (subItems.Count * _itemHeight) + 40);

                _graphics.DrawText("SUB-SETTINGS", subMenuOrigin.X, subMenuOrigin.Y + 5, colorAccent, 11, true);

                for (int i = 0; i < subItems.Count; i++)
                {
                    var item = subItems[i];
                    var yOffset = 30 + (i * 25);
                    bool isSubSelected = i == _selectedSubItem;

                    _graphics.DrawText(item.Name, subMenuOrigin.X, subMenuOrigin.Y + yOffset,
                        isSubSelected ? colorHighlight : colorText, 10);
                    _graphics.DrawText(item.GetValue(), subMenuOrigin.X + 120, subMenuOrigin.Y + yOffset,
                        isSubSelected ? colorHighlight : colorTextDim, 10, true);
                }
            }
            
            // Footer Info
            _graphics.DrawText($"Toggle: {MenuToggleKeyLabel}  |  Navigate: Arrows  |  Select: Enter",
                _menuPosition.X + _sidebarWidth + 20, _menuPosition.Y + _menuSize.Y - 15, colorTextDim, 10);
            
            // Editing prompt
            if (_editingValue)
            {
                var promptSize = new Vector2(250, 60);
                var promptPos = _menuPosition + (_menuSize / 2f) - (promptSize / 2f);

                _graphics.DrawRectangle(ToUint(SKColors.DarkRed.WithAlpha(220)), promptPos, promptSize.X, promptSize.Y);
                _graphics.DrawRectangleOutline(ToUint(SKColors.Red), promptPos, promptSize.X, promptSize.Y);
                _graphics.DrawText("WAITING FOR INPUT...", promptPos.X + 25, promptPos.Y + 25, colorText, 13, true);
                _graphics.DrawText("Press any key to bind", promptPos.X + 45, promptPos.Y + 45, colorTextDim, 10);
            }
        }
        
        private void SaveConfig()
        {
            ConfigManager.Save(_config);
        }
        
        public void Dispose()
        {
            SaveConfig();
        }

        private static uint ToUint(SKColor color)
        {
            return ((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;
        }

        internal static string FormatKey(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                var digit = (char)('0' + (key - Keys.D0));
                return digit.ToString();
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                var digit = (char)('0' + (key - Keys.NumPad0));
                return $"Num {digit}";
            }

            return key switch
            {
                Keys.None => "None",
                Keys.LButton => "Mouse 1",
                Keys.RButton => "Mouse 2",
                Keys.MButton => "Mouse 3",
                Keys.XButton1 => "Mouse 4",
                Keys.XButton2 => "Mouse 5",
                Keys.Return => "Enter",
                Keys.Menu => "Alt",
                Keys.LMenu => "Left Alt",
                Keys.RMenu => "Right Alt",
                Keys.LShiftKey => "Left Shift",
                Keys.RShiftKey => "Right Shift",
                Keys.LControlKey => "Left Ctrl",
                Keys.RControlKey => "Right Ctrl",
                Keys.LWin => "Left Win",
                Keys.RWin => "Right Win",
                Keys.Capital => "Caps Lock",
                Keys.Space => "Space",
                Keys.Tab => "Tab",
                Keys.Escape => "Escape",
                Keys.Back => "Backspace",
                _ => key.ToString()
            };
        }
    }
    
    // Menu item classes
    public abstract class MenuItem
    {
        public string Name { get; }
        protected MenuItem(string name) => Name = name;
        public abstract string GetValue();
    }
    
    public class ToggleMenuItem : MenuItem
    {
        private readonly Func<bool> _getter;
        private readonly Action<bool> _setter;
        
        public ToggleMenuItem(string name, Func<bool> getter, Action<bool> setter) : base(name)
        {
            _getter = getter;
            _setter = setter;
        }
        
        public void Toggle() => _setter(!_getter());
        public override string GetValue() => _getter() ? "ON" : "OFF";
    }
    
    public class KeybindMenuItem : MenuItem
    {
        private readonly Func<Keys> _getter;
        private readonly Action<Keys> _setter;
        
        public KeybindMenuItem(string name, Func<Keys> getter, Action<Keys> _setter) : base(name)
        {
            _getter = getter;
            this._setter = _setter;
        }
        
    public void SetValue(Keys key) => _setter(key);
    public override string GetValue() => OverlayMenu.FormatKey(_getter());
    }
    
    public class SliderMenuItem : MenuItem
    {
        private readonly Func<double> _getter;
        private readonly Action<double> _setter;
        private readonly double _min;
        private readonly double _max;
        private readonly double _step;
        private readonly string _displayFormat;
        
        public SliderMenuItem(string name, Func<double> getter, Action<double> setter, double min, double max, double step = 1.0, string? displayFormat = null) : base(name)
        {
            _getter = getter;
            _setter = setter;
            _min = min;
            _max = max;
            _step = step;
            _displayFormat = displayFormat ?? (Math.Abs(step - Math.Truncate(step)) < double.Epsilon ? "0" : "0.0");
        }
        
        public void Increment()
        {
            var value = _getter();
            value = Math.Min(_max, value + _step);
            _setter(value);
        }
        
        public void Decrement()
        {
            var value = _getter();
            value = Math.Max(_min, value - _step);
            _setter(value);
        }
        
        public override string GetValue()
        {
            var value = _getter();
            return value.ToString(_displayFormat, CultureInfo.InvariantCulture);
        }
    }
    
    public class SubMenuItem : MenuItem
    {
        public List<MenuItem> SubItems { get; }
        
        public SubMenuItem(string name, List<MenuItem> subItems) : base(name)
        {
            SubItems = subItems ?? new List<MenuItem>();
        }
        
        public override string GetValue() => "→";
    }

    /// <summary>v2.0: invokes an action when selected (Save / Reload / Reset).</summary>
    public class ActionMenuItem : MenuItem
    {
        private readonly Func<bool> _action;
        private string _lastStatus = string.Empty;
        private DateTime _statusUntil = DateTime.MinValue;

        public ActionMenuItem(string name, Func<bool> action) : base(name)
        {
            _action = action;
        }

        public void Invoke()
        {
            try
            {
                var ok = _action();
                _lastStatus = ok ? "OK" : "FAIL";
            }
            catch
            {
                _lastStatus = "ERR";
            }
            _statusUntil = DateTime.Now.AddSeconds(2);
        }

        public override string GetValue()
        {
            if (DateTime.Now < _statusUntil) return _lastStatus;
            return "▶";
        }
    }
    
    public class MenuCategory
    {
        public string Name { get; }
        public List<MenuItem> Items { get; }
        
        public MenuCategory(string name, List<MenuItem> items)
        {
            Name = name;
            Items = items ?? new List<MenuItem>();
        }
    }
}