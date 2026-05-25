using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CS2GameHelper.Utils;
using CS2GameHelper.Features;

namespace CS2GameHelper.Graphics
{
    public partial class ManagementWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly Rcs _rcs;
        private readonly Movement _movement;

        public ManagementWindow(ConfigManager config, Rcs rcs, Movement movement)
        {
            InitializeComponent();
            _config = config;
            _rcs = rcs;
            _movement = movement;

            this.Loaded += (s, e) =>
            {
                LoadConfigToUi();
                PopulateWeapons();
            };
        }

        private void LoadConfigToUi()
        {
            BhopCheck.IsChecked = _config.Movement.Bhop;
            StrafeCheck.IsChecked = _config.Movement.AutoStrafe;
            FollowRcsCheck.IsChecked = _config.FollowRcs.Enabled;
            GlowCheck.IsChecked = _config.AdvancedVisuals.GlowEsp;
            BacktrackCheck.IsChecked = _config.AdvancedVisuals.Backtracking;
            SensInput.Text = _config.Rcs.Sensitivity.ToString();
        }

        private void PopulateWeapons()
        {
            WeaponComboBox.Items.Clear();
            WeaponComboBox.Items.Add("Select a weapon...");

            // Example weapons that have patterns
            string[] weapons = { "ak47", "m4a4", "m4a1", "galil", "famas", "sg553", "aug", "p90", "mac10", "mp9" };
            foreach (var w in weapons)
            {
                WeaponComboBox.Items.Add(w);
            }
            WeaponComboBox.SelectedIndex = 0;
        }

        private void StartRcs_Click(object sender, RoutedEventArgs e)
        {
            _config.Rcs.Enabled = true;
            UpdateStatus(true);
        }

        private void StopRcs_Click(object sender, RoutedEventArgs e)
        {
            _config.Rcs.Enabled = false;
            UpdateStatus(false);
        }

        private void EnableAuto_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Automatic weapon detection enabled (GSI).", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DisableAuto_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Automatic weapon detection disabled.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateStatus(bool active)
        {
            RcsStatusIndicator.Fill = active ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            RcsStatusText.Text = active ? "Active" : "Inactive";
        }

        private void SaveParams_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (float.TryParse(SensInput.Text, out float sens)) _config.Rcs.Sensitivity = sens;

                string? weapon = WeaponComboBox.SelectedItem as string;
                if (weapon != null && weapon != "Select a weapon...")
                {
                    if (!_config.WeaponProfiles.ContainsKey(weapon))
                        _config.WeaponProfiles[weapon] = new ConfigManager.WeaponProfile();

                    var profile = _config.WeaponProfiles[weapon];
                    if (float.TryParse(MultInput.Text, out float mult)) profile.Multiple = mult;
                    if (float.TryParse(DivInput.Text, out float div)) profile.SleepDivider = div;
                    if (float.TryParse(AdjInput.Text, out float adj)) profile.SleepSuber = adj;
                }

                _config.Movement.Bhop = BhopCheck.IsChecked ?? false;
                _config.Movement.AutoStrafe = StrafeCheck.IsChecked ?? false;
                _config.FollowRcs.Enabled = FollowRcsCheck.IsChecked ?? false;
                _config.AdvancedVisuals.GlowEsp = GlowCheck.IsChecked ?? false;
                _config.AdvancedVisuals.Backtracking = BacktrackCheck.IsChecked ?? false;

                _config.SaveCurrent();

                // Refresh patterns in memory
                PatternManager.LoadPatterns();

                System.Windows.MessageBox.Show("Parameters saved and applied.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving parameters: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WeaponComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string weapon = WeaponComboBox.SelectedItem as string;
            if (weapon == null || weapon == "Select a weapon...") return;

            SelectedWeaponText.Text = $"Selected: {weapon}";

            if (_config.WeaponProfiles.TryGetValue(weapon, out var profile))
            {
                MultInput.Text = profile.Multiple.ToString();
                DivInput.Text = profile.SleepDivider.ToString();
                AdjInput.Text = profile.SleepSuber.ToString();
            }
            else
            {
                MultInput.Text = "1";
                DivInput.Text = "1";
                AdjInput.Text = "0";
            }

            DrawRecoilPattern(weapon);
        }

        private void DrawRecoilPattern(string weapon)
        {
            RecoilCanvas.Children.Clear();
            var pattern = PatternManager.GetPattern(weapon);
            if (pattern == null) return;

            double x = RecoilCanvas.ActualWidth / 2;
            double y = RecoilCanvas.ActualHeight / 2;

            Polyline polyline = new Polyline
            {
                Stroke = System.Windows.Media.Brushes.Cyan,
                StrokeThickness = 2
            };

            polyline.Points.Add(new System.Windows.Point(x, y));

            foreach (var p in pattern)
            {
                x += p.Dx * 2;
                y -= p.Dy * 2;
                polyline.Points.Add(new System.Windows.Point(x, y));

                Ellipse dot = new Ellipse { Width = 2, Height = 2, Fill = System.Windows.Media.Brushes.White };
                Canvas.SetLeft(dot, x - 1);
                Canvas.SetTop(dot, y - 1);
                RecoilCanvas.Children.Add(dot);
            }

            RecoilCanvas.Children.Add(polyline);
        }
    }
}
