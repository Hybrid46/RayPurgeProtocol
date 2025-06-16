using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

public static class Settings
{
    // Display settings
    public static int display = 0;
    public static int screenWidth = 800;
    public static int screenHeight = 600;
    public static int internalScreenWidth = 400;
    public static int internalScreenHeight = 300;
    public static int windowStartPositionX = 400;
    public static int windowStartPositionY = 300;
    public static bool fullscreen = false;
    public static bool borderlessFullScreen = false;

    // Performance settings
    public static int targetFPS = 60;
    public static int targetUPS = 60;
    public static float fixedDeltaTime = 1.0f / 60.0f;

    // Gameplay settings
    public static float moveSpeed = 3.0f;
    public static float rotationSpeed = 3.0f;
    public static float mouseRotationSpeed = 4.0f;

    private const string SETTINGS_FILE = "settings.cfg";

    private static void LoadDefaults()
    {
        display = 0;
        screenWidth = 800;
        screenHeight = 600;
        internalScreenWidth = 400;
        internalScreenHeight = 300;
        windowStartPositionX = 400;
        windowStartPositionY = 300;
        fullscreen = false;
        borderlessFullScreen = false;
        targetFPS = 60;
        targetUPS = 60;
        moveSpeed = 3.0f;
        rotationSpeed = 3.0f;
        mouseRotationSpeed = 4.0f;
        fixedDeltaTime = 1.0f / targetUPS;
    }

    public static void LoadSettings()
    {
        if (!File.Exists(SETTINGS_FILE))
        {
            Console.WriteLine("Settings file not found. Creating with default values.");
            LoadDefaults();
            SaveSettings();  // Create file with defaults
            return;
        }

        Dictionary<string, string> loadedSettings = new Dictionary<string, string>();

        try
        {
            foreach (string line in File.ReadAllLines(SETTINGS_FILE))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string[] parts = line.Split('=', 2);

                if (parts.Length == 2)
                {
                    loadedSettings[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Parse display settings
            if (loadedSettings.TryGetValue(nameof(display), out string displayStr) &&
                int.TryParse(displayStr, out int displayValue)) display = displayValue;

            if (loadedSettings.TryGetValue(nameof(screenWidth), out string widthStr) &&
                int.TryParse(widthStr, out int width)) screenWidth = width;

            if (loadedSettings.TryGetValue(nameof(screenHeight), out string heightStr) &&
                int.TryParse(heightStr, out int height)) screenHeight = height;

            if (loadedSettings.TryGetValue(nameof(internalScreenWidth), out string internalWidthStr) &&
                int.TryParse(internalWidthStr, out int internalWidth)) internalScreenWidth = internalWidth;

            if (loadedSettings.TryGetValue(nameof(internalScreenHeight), out string internalHeightStr) &&
                int.TryParse(internalHeightStr, out int internalHeight)) internalScreenHeight = internalHeight;

            if (loadedSettings.TryGetValue(nameof(windowStartPositionX), out string posXStr) &&
                int.TryParse(posXStr, out int posX)) windowStartPositionX = posX;

            if (loadedSettings.TryGetValue(nameof(windowStartPositionY), out string posYStr) &&
                int.TryParse(posYStr, out int posY)) windowStartPositionY = posY;

            if (loadedSettings.TryGetValue(nameof(fullscreen), out string fullscreenStr))
                fullscreen = Convert.ToBoolean(fullscreenStr);

            if (loadedSettings.TryGetValue(nameof(borderlessFullScreen), out string borderlessStr))
                borderlessFullScreen = Convert.ToBoolean(borderlessStr);

            // Parse performance settings
            if (loadedSettings.TryGetValue(nameof(targetFPS), out string fpsStr) &&
                int.TryParse(fpsStr, out int fps)) targetFPS = fps;

            if (loadedSettings.TryGetValue(nameof(targetUPS), out string upsStr) &&
                int.TryParse(upsStr, out int ups)) targetUPS = ups;

            // Parse gameplay settings - FIXED: use different variable names to avoid conflicts
            if (loadedSettings.TryGetValue(nameof(moveSpeed), out string moveSpeedStr) &&
                float.TryParse(moveSpeedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float moveSpeedVal))
                moveSpeed = moveSpeedVal;

            if (loadedSettings.TryGetValue(nameof(rotationSpeed), out string rotSpeedStr) &&
                float.TryParse(rotSpeedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float rotSpeedVal))
                rotationSpeed = rotSpeedVal;

            if (loadedSettings.TryGetValue(nameof(mouseRotationSpeed), out string mouseRotSpeedStr) &&
                float.TryParse(mouseRotSpeedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float mouseRotSpeedVal))
                mouseRotationSpeed = mouseRotSpeedVal;

            // Update derived values
            fixedDeltaTime = 1.0f / Math.Max(targetUPS, 1);

            Console.WriteLine("Settings loaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}. Loading defaults.");
            LoadDefaults();
        }
    }

    public static void SaveSettings()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(SETTINGS_FILE))
            {
                writer.WriteLine($"# Game settings configuration");
                writer.WriteLine();

                // Display settings
                writer.WriteLine($"# Display Settings");
                writer.WriteLine($"{nameof(display)}={display}");
                writer.WriteLine($"{nameof(screenWidth)}={screenWidth}");
                writer.WriteLine($"{nameof(screenHeight)}={screenHeight}");
                writer.WriteLine($"{nameof(internalScreenWidth)}={internalScreenWidth}");
                writer.WriteLine($"{nameof(internalScreenHeight)}={internalScreenHeight}");
                writer.WriteLine($"{nameof(windowStartPositionX)}={windowStartPositionX}");
                writer.WriteLine($"{nameof(windowStartPositionY)}={windowStartPositionY}");
                writer.WriteLine($"{nameof(fullscreen)}={fullscreen.ToString().ToLower()}");
                writer.WriteLine($"{nameof(borderlessFullScreen)}={borderlessFullScreen.ToString().ToLower()}");
                writer.WriteLine();

                // Performance settings
                writer.WriteLine($"# Performance Settings");
                writer.WriteLine($"{nameof(targetFPS)}={targetFPS}");
                writer.WriteLine($"{nameof(targetUPS)}={targetUPS}");
                writer.WriteLine();

                // Gameplay settings
                writer.WriteLine($"# Gameplay Settings");
                writer.WriteLine($"{nameof(moveSpeed)}={moveSpeed.ToString(CultureInfo.InvariantCulture)}");
                writer.WriteLine($"{nameof(rotationSpeed)}={rotationSpeed.ToString(CultureInfo.InvariantCulture)}");
                writer.WriteLine($"{nameof(mouseRotationSpeed)}={mouseRotationSpeed.ToString(CultureInfo.InvariantCulture)}");
            }
            Console.WriteLine("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}