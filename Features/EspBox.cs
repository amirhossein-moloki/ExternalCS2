using System.Numerics;
using System.Collections.Generic;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

/// <summary>
/// Вспомогательные цвета в формате ARGB (0xAARRGGBB).
/// </summary>
internal static class EspColor
{
    public const uint White = 0xFFFFFFFF;
    public const uint Green = 0xFF00FF00;
    public const uint Black = 0xFF000000;
    public const uint Red = 0xFFFF0000;
    public const uint Yellow = 0xFFFFFF00;
    public const uint Orange = 0xFFFFA500; // Цвет для C4
    public const uint Gray = 0xFF808080;
}

/// <summary>
/// Отображает ESP-боксы вокруг игроков с полной кастомизацией через конфиг.
/// </summary>
public static class EspBox
{
    private const int OutlineThickness = 1;
    private const float UnitsToMeters = 0.0254f;

    private static readonly Dictionary<string, string> GunIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ножи
        ["knife"] = "[", ["knife_t"] = "[", ["knife_ct"] = "]", ["bayonet"] = "p",
        ["flipknife"] = "q", ["gutknife"] = "r", ["karambit"] = "s", ["m9bayonet"] = "t",
        ["tacticalknife"] = "u", ["butterflyknife"] = "v", ["falchionknife"] = "w",
        ["shadowdaggers"] = "x", ["paracordknife"] = "y", ["survivalknife"] = "z",
        ["ursusknife"] = "{", ["navajaknife"] = "|", ["nomadknife"] = "}",
        ["stilettoknife"] = "~", ["talonknife"] = "⌂", ["classicknife"] = "Ç",

        // Пистолеты
        ["deagle"] = "A", ["elite"] = "B", ["fiveseven"] = "C", ["glock"] = "D",
        ["hkp2000"] = "E", ["p250"] = "F", ["usp_silencer"] = "G", ["tec9"] = "H",
        ["cz75a"] = "I", ["revolver"] = "J",

        // SMG
        ["mac10"] = "K", ["mp9"] = "L", ["mp7"] = "M", ["ump45"] = "N",
        ["p90"] = "O", ["bizon"] = "P",

        // Штурмовые
        ["ak47"] = "Q", ["aug"] = "R", ["famas"] = "S", ["galilar"] = "T",
        ["m4a1"] = "U", ["m4a1_silencer"] = "V", ["sg556"] = "W",

        // Снайперские
        ["awp"] = "X", ["g3sg1"] = "Y", ["scar20"] = "Z", ["ssg08"] = "a",

        // Дробовики
        ["mag7"] = "b", ["nova"] = "c", ["sawedoff"] = "d", ["xm1014"] = "e",

        // Пулемёты
        ["m249"] = "f", ["negev"] = "g",

        // Прочее
        ["taser"] = "h", ["c4"] = "o",

        // Гранаты
        ["flashbang"] = "i", ["hegrenade"] = "j", ["smokegrenade"] = "k",
        ["molotov"] = "l", ["decoy"] = "m", ["incgrenade"] = "n"
    };

    // <<< НОВОЕ: Иконки для статусов в формате Unicode
    private static readonly Dictionary<string, string> StatusIcons = new()
    {
        ["Flashed"] = "💡",
        ["Scoped"] = "🔎",
        ["Defusing"] = "💣",
        ["Air"] = "🪂",
        ["Running"] = "🏃",
        ["Walking"] = "🚶"
    };

    // <<< НОВЫЙ ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ ЦЕНТРИРОВАНИЯ ТЕКСТА
    private static void DrawCenteredText(ModernGraphics graphics, string text, float centerX, float y, uint color, float fontSize = 12, bool useCustomFont = false)
    {
        var textSize = graphics.MeasureText(text, fontSize, useCustomFont);
        float textWidth = textSize.X;
        float textX = centerX - textWidth / 2f;
        graphics.DrawText(text, textX, y, color, fontSize, useCustomFont);
    }

    public static void Draw(ModernGraphics graphics, ConfigManager fullConfig)
    {
        var espConfig = fullConfig.Esp.Box;
        if (!espConfig.Enabled) return;

        var player = graphics.GameData.Player;
        var entities = graphics.GameData.Entities;
        if (player == null || entities == null) return;

        foreach (var entity in entities)
        {
            if (!entity.IsAlive() || entity.AddressBase == player.AddressBase) continue;
            if (fullConfig.TeamCheck && entity.Team == player.Team) continue;

            float distance = Vector3.Distance(player.Position, entity.Position) * UnitsToMeters;
            if (distance > 1500) continue; // Distance Culling

            var bbox = GetEntityBoundingBox(player, entity);
            if (bbox == null) continue;

            DrawEntityEsp(graphics, player, entity, bbox.Value, espConfig);
        }
    }

    private static void DrawEntityEsp(
        ModernGraphics graphics,
        Player localPlayer,
        Entity entity,
        (Vector2 TopLeft, Vector2 BottomRight) bbox,
        ConfigManager.EspConfig.BoxConfig config)
    {
        var (topLeft, bottomRight) = bbox;
        if (topLeft.X >= bottomRight.X || topLeft.Y >= bottomRight.Y) return;

        float distance = Vector3.Distance(localPlayer.Position, entity.Position) * UnitsToMeters;
        bool isClose = distance < 800;

        // === Цвет с учётом видимости и команды ===
        bool isVisible = entity.IsVisible;
        string colorHex = entity.Team == Team.Terrorists 
            ? config.EnemyColor 
            : config.TeamColor;

        byte alpha = isVisible
            ? Convert.ToByte(config.VisibleAlpha, 16)
            : Convert.ToByte(config.InvisibleAlpha, 16);

        uint baseColor = Convert.ToUInt32(colorHex, 16);
        uint boxColor = SetAlpha(baseColor, alpha);

        // === Бокс ===
        float width = bottomRight.X - topLeft.X;
        float height = bottomRight.Y - topLeft.Y;
        if (config.ShowBox)
        {
            graphics.DrawRectOutline(topLeft.X, topLeft.Y, width, height, boxColor);
        }

        float textY = topLeft.Y - 16;
        float centerX = (topLeft.X + bottomRight.X) / 2f;

        // LOD: Only draw detailed info for close entities
        if (!isClose) return;

        // === Имя ===
        if (config.ShowName)
        {
            string rawName = entity.Name ?? "UNKNOWN";
            string name = rawName.Length > 0 ? rawName.Substring(0, rawName.Length - 1) : "UNKNOWN";
            DrawCenteredText(graphics, name, centerX, textY, EspColor.White);
            textY += 14;
        }

        // <<< ИЗМЕНЕНО: Проверяем, несет ли игрок бомбу по имени оружия
        bool hasBomb = !string.IsNullOrEmpty(entity.CurrentWeaponName) && entity.CurrentWeaponName.Contains("c4", StringComparison.OrdinalIgnoreCase);
        if (hasBomb)
        {
            string bombText = "💣 C4";
            DrawCenteredText(graphics, bombText, centerX, textY, EspColor.Orange);
            textY += 14;
        }

        // === Дистанция ===
        if (config.ShowDistance)
        {
            string distText = $"{distance:0}m";
            DrawCenteredText(graphics, distText, centerX, textY, EspColor.White);
            textY += 14;
        }

        // === Полоска здоровья ===
        if (config.ShowHealthBar)
        {
            float healthPercentage = Math.Clamp(entity.Health / 100f, 0f, 1f);
            uint healthBarColor = entity.Health > 60 ? EspColor.Green : 
                                   entity.Health > 30 ? EspColor.Yellow : EspColor.Red;

            float hbX = 0, hbY = 0, hbW = 0, hbH = 0;
            float hbFilledX = 0, hbFilledY = 0, hbFilledW = 0, hbFilledH = 0;

            switch (config.HealthPosition)
            {
                case 0: // Left
                    hbX = topLeft.X - 8f;
                    hbY = topLeft.Y;
                    hbW = 4f;
                    hbH = height;
                    hbFilledX = hbX;
                    hbFilledY = hbY + (hbH * (1f - healthPercentage));
                    hbFilledW = hbW;
                    hbFilledH = hbH * healthPercentage;
                    break;
                case 1: // Top
                    hbX = topLeft.X;
                    hbY = topLeft.Y - 8f;
                    hbW = width;
                    hbH = 4f;
                    hbFilledX = hbX;
                    hbFilledY = hbY;
                    hbFilledW = hbW * healthPercentage;
                    hbFilledH = hbH;
                    break;
                case 2: // Right
                    hbX = bottomRight.X + 4f;
                    hbY = topLeft.Y;
                    hbW = 4f;
                    hbH = height;
                    hbFilledX = hbX;
                    hbFilledY = hbY + (hbH * (1f - healthPercentage));
                    hbFilledW = hbW;
                    hbFilledH = hbH * healthPercentage;
                    break;
                case 3: // Bottom
                    hbX = topLeft.X;
                    hbY = bottomRight.Y + 4f;
                    hbW = width;
                    hbH = 4f;
                    hbFilledX = hbX;
                    hbFilledY = hbY;
                    hbFilledW = hbW * healthPercentage;
                    hbFilledH = hbH;
                    break;
            }

            graphics.DrawRect(hbX, hbY, hbW, hbH, 0x80000000);
            graphics.DrawRect(hbFilledX, hbFilledY, hbFilledW, hbFilledH, healthBarColor);
            graphics.DrawRectOutline(hbX - 1, hbY - 1, hbW + 2, hbH + 2, EspColor.Black);
        }

        // === Текст здоровья ===
        if (config.ShowHealthText)
        {
            string healthText = entity.Health.ToString();
            float tx = 0, ty = 0;
            var textSize = graphics.MeasureText(healthText);

            switch (config.HealthPosition)
            {
                case 0: // Left
                    tx = topLeft.X - 12f - textSize.X;
                    ty = topLeft.Y + (height / 2f) - (textSize.Y / 2f);
                    break;
                case 1: // Top
                    tx = centerX - (textSize.X / 2f);
                    ty = topLeft.Y - 12f - textSize.Y;
                    break;
                case 2: // Right
                    tx = bottomRight.X + 12f;
                    ty = topLeft.Y + (height / 2f) - (textSize.Y / 2f);
                    break;
                case 3: // Bottom
                    tx = centerX - (textSize.X / 2f);
                    ty = bottomRight.Y + 12f;
                    break;
            }
            graphics.DrawText(healthText, tx, ty, EspColor.White);
        }

        // === Броня / Шлем ===
        if (config.ShowArmor && entity.Armor > 0)
        {
            string armorText = entity.HasHelmet ? $"🛡{entity.Armor}" : $"🥚{entity.Armor}";
            int armorX = (int)(topLeft.X - 12);
            int armorY = (int)(bottomRight.Y - 12);
            graphics.DrawText(armorText, armorX, armorY, EspColor.White);
        }

        // === Иконка оружия ===
        if (config.ShowWeaponIcon && !string.IsNullOrEmpty(entity.CurrentWeaponName))
        {
            string icon = GetWeaponIcon(entity.CurrentWeaponName);
            if (!string.IsNullOrEmpty(icon))
            {
                int weaponY = (int)(bottomRight.Y + 2);
                bool useCustom = graphics is ModernGraphics mg && mg.IsUndefeatedFontLoaded;
                DrawCenteredText(graphics, icon, centerX, weaponY, EspColor.White, 14, useCustom);
            }
        }

        // === Статусы (с иконками) ===
        if (config.ShowFlags)
        {
            int flagX = (int)(bottomRight.X + 5);
            int flagY = (int)topLeft.Y;
            int spacing = 16;
            int line = 0;

            if (entity.FlashAlpha > 7)
            {
                graphics.DrawText($"{StatusIcons["Flashed"]} Flashed", flagX, flagY + line * spacing, EspColor.Yellow);
                line++;
            }

            if (entity.IsInScope == 1)
            {
                graphics.DrawText($"{StatusIcons["Scoped"]} Scoped", flagX, flagY + line * spacing, EspColor.White);
                line++;
            }

            if (entity.IsDefusing)
            {
                graphics.DrawText($"{StatusIcons["Defusing"]} Defusing", flagX, flagY + line * spacing, EspColor.Red);
                line++;
            }

            if (!entity.Flags.HasFlag(EntityFlags.OnGround))
            {
                graphics.DrawText($"{StatusIcons["Air"]} Air", flagX, flagY + line * spacing, EspColor.Gray);
                line++;
            }

            float speed = entity.Velocity.Length();
            if (speed > 200f)
            {
                graphics.DrawText($"{StatusIcons["Running"]} Running", flagX, flagY + line * spacing, EspColor.White);
                line++;
            }
            else if (speed > 10f)
            {
                graphics.DrawText($"{StatusIcons["Walking"]} Walking", flagX, flagY + line * spacing, EspColor.Gray);
                line++;
            }
        }
    }

    private static string GetWeaponIcon(string? weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return string.Empty;
        string cleanName = weaponName.Replace("weapon_", "", StringComparison.OrdinalIgnoreCase);
        return GunIcons.GetValueOrDefault(cleanName, "?");
    }

    private static uint SetAlpha(uint color, byte alpha)
    {
        return ((uint)alpha << 24) | (color & 0x00FFFFFFu);
    }

    private static (Vector2, Vector2)? GetEntityBoundingBox(Player player, Entity entity)
    {
        var matrix = player.MatrixViewProjectionViewport;
        if (entity.BonePos == null || !entity.BonePos.TryGetValue("head", out var headPos))
            return null;

        var origin = entity.Position;
        var head = headPos + new Vector3(0, 0, 10); // Add slight padding above head
        var bottom = origin - new Vector3(0, 0, 5); // Add slight padding below feet

        var headProj = matrix.Transform(head);
        var bottomProj = matrix.Transform(bottom);

        if (headProj.Z >= 1 || bottomProj.Z >= 1)
            return null;

        float height = Math.Abs(headProj.Y - bottomProj.Y);
        float width = height / 2f;

        var topLeft = new Vector2(headProj.X - width / 2f, headProj.Y);
        var bottomRight = new Vector2(headProj.X + width / 2f, bottomProj.Y);

        return (topLeft, bottomRight);
    }
}