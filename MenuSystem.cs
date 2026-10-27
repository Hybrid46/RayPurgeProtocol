using Raylib_cs;
using static Settings;

public class MenuSystem
{
    private enum MenuPage { Root, Start, GameOptions, Options, Graphics, Gameplay }

    private MenuPage currentPage = MenuPage.Root;
    private int selectedIndex = 0;

    public bool wantsToStartGame { get; private set; } = false;
    public bool wantsToExit { get; private set; } = false;

    private class MenuItem
    {
        public string Label;
        public Func<string> GetValueString;
        public Action AdjustLeft;
        public Action AdjustRight;
        public Action Activate;
        public bool IsValue => GetValueString != null;
    }

    private static readonly (int w, int h)[] ResolutionPresets =
    {
        (320, 240), (400, 300), (512, 384), (640, 480), (800, 600)
    };

    public void Reset()
    {
        currentPage = MenuPage.Root;
        selectedIndex = 0;
        wantsToStartGame = false;
        wantsToExit = false;
    }

    public void Update()
    {
        var items = GetCurrentItems();
        if (items.Count == 0) return;

        bool up = Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up);
        bool down = Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down);
        bool left = Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.Left);
        bool right = Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.Right);
        bool enter = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
        bool esc = Raylib.IsKeyPressed(KeyboardKey.Escape);

        if (up) selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
        if (down) selectedIndex = (selectedIndex + 1) % items.Count;
        selectedIndex = Math.Clamp(selectedIndex, 0, items.Count - 1);

        var item = items[selectedIndex];

        if (item.IsValue)
        {
            if (left) item.AdjustLeft?.Invoke();
            if (right) item.AdjustRight?.Invoke();
        }

        if (enter) item.Activate?.Invoke();
        if (esc) GoBack();
    }

    public void Draw(int screenWidth, int screenHeight)
    {
        string pageTitle = currentPage switch
        {
            MenuPage.Root => "PURGE PROTOCOL",
            MenuPage.Start => "START",
            MenuPage.GameOptions => "GAME OPTIONS",
            MenuPage.Options => "OPTIONS",
            MenuPage.Graphics => "GRAPHICS",
            MenuPage.Gameplay => "GAMEPLAY",
            _ => ""
        };

        int titleSize = currentPage == MenuPage.Root ? 80 : 60;
        int titleWidth = Raylib.MeasureText(pageTitle, titleSize);
        Raylib.DrawText(pageTitle, (screenWidth - titleWidth) / 2, 80, titleSize, Color.White);

        var items = GetCurrentItems();
        int itemSize = 32;
        int itemSpacing = 50;
        int startY = screenHeight / 2 - (items.Count * itemSpacing) / 2 + 50;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            bool selected = i == selectedIndex;

            string display = item.Label;
            if (item.IsValue)
            {
                display += ": ";
                if (selected) display += "< ";
                display += item.GetValueString();
                if (selected) display += " >";
            }

            Color color = selected ? Color.Yellow : Color.LightGray;
            int w = Raylib.MeasureText(display, itemSize);
            int x = (screenWidth - w) / 2;
            int y = startY + i * itemSpacing;

            Raylib.DrawText(display, x, y, itemSize, color);
        }

        string inst = "W/S: Navigate  |  A/D: Adjust  |  Enter: Select  |  Esc: Back";
        int instSize = 16;
        int instWidth = Raylib.MeasureText(inst, instSize);
        Raylib.DrawText(inst, (screenWidth - instWidth) / 2, screenHeight - 40, instSize, Color.Gray);
    }

    private void NavigateTo(MenuPage page)
    {
        currentPage = page;
        selectedIndex = 0;
    }

    private void GoBack()
    {
        switch (currentPage)
        {
            case MenuPage.Root: wantsToExit = true; break;
            case MenuPage.Start: NavigateTo(MenuPage.Root); break;
            case MenuPage.GameOptions: NavigateTo(MenuPage.Start); break;
            case MenuPage.Options: NavigateTo(MenuPage.Root); break;
            case MenuPage.Graphics: NavigateTo(MenuPage.Options); break;
            case MenuPage.Gameplay: NavigateTo(MenuPage.Options); break;
        }
    }

    private static int GetResolutionIndex()
    {
        for (int i = 0; i < ResolutionPresets.Length; i++)
            if (ResolutionPresets[i].w == Settings.internalScreenWidth && ResolutionPresets[i].h == Settings.internalScreenHeight)
                return i;
        return 1;
    }

    private static void SetResolutionByIndex(int index)
    {
        int len = ResolutionPresets.Length;
        index = ((index % len) + len) % len;
        Settings.internalScreenWidth = ResolutionPresets[index].w;
        Settings.internalScreenHeight = ResolutionPresets[index].h;
    }

    private List<MenuItem> GetCurrentItems()
    {
        switch (currentPage)
        {
            case MenuPage.Root:
                return new List<MenuItem>
                {
                    new MenuItem { Label = "Start", Activate = () => NavigateTo(MenuPage.Start) },
                    new MenuItem { Label = "Options", Activate = () => NavigateTo(MenuPage.Options) },
                    new MenuItem { Label = "Exit", Activate = () => wantsToExit = true },
                };

            case MenuPage.Start:
                return new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "Difficulty",
                        GetValueString = () => Settings.difficulty switch { 0 => "Easy", 1 => "Normal", 2 => "Hard", _ => "Normal" },
                        AdjustLeft = () => Settings.difficulty = Math.Max(0, Settings.difficulty - 1),
                        AdjustRight = () => Settings.difficulty = Math.Min(2, Settings.difficulty + 1),
                    },
                    new MenuItem { Label = "Game Options", Activate = () => NavigateTo(MenuPage.GameOptions) },
                    new MenuItem { Label = "Start Game", Activate = () => wantsToStartGame = true },
                    new MenuItem { Label = "Back", Activate = GoBack },
                };

            case MenuPage.GameOptions:
                return new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "Enemy Spawn",
                        GetValueString = () => $"{Settings.enemySpawnChance * 100:F0}%",
                        AdjustLeft = () => Settings.enemySpawnChance = Math.Max(0f, Settings.enemySpawnChance - 0.05f),
                        AdjustRight = () => Settings.enemySpawnChance = Math.Min(1f, Settings.enemySpawnChance + 0.05f),
                    },
                    new MenuItem
                    {
                        Label = "Extra Doors",
                        GetValueString = () => $"{Settings.extraDoorChance * 100:F0}%",
                        AdjustLeft = () => Settings.extraDoorChance = Math.Max(0f, Settings.extraDoorChance - 0.1f),
                        AdjustRight = () => Settings.extraDoorChance = Math.Min(1f, Settings.extraDoorChance + 0.1f),
                    },
                    new MenuItem { Label = "Back", Activate = GoBack },
                };

            case MenuPage.Options:
                return new List<MenuItem>
                {
                    new MenuItem { Label = "Graphics", Activate = () => NavigateTo(MenuPage.Graphics) },
                    new MenuItem { Label = "Gameplay", Activate = () => NavigateTo(MenuPage.Gameplay) },
                    new MenuItem { Label = "Back", Activate = GoBack },
                };

            case MenuPage.Graphics:
                return new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "Resolution",
                        GetValueString = () => $"{Settings.internalScreenWidth}x{Settings.internalScreenHeight}",
                        AdjustLeft = () => SetResolutionByIndex(GetResolutionIndex() - 1),
                        AdjustRight = () => SetResolutionByIndex(GetResolutionIndex() + 1),
                    },
                    new MenuItem
                    {
                        Label = "Target FPS",
                        GetValueString = () => $"{Settings.targetFPS}",
                        AdjustLeft = () => Settings.targetFPS = Math.Max(30, Settings.targetFPS - 30),
                        AdjustRight = () => Settings.targetFPS = Math.Min(240, Settings.targetFPS + 30),
                    },
                    new MenuItem
                    {
                        Label = "Draw Distance",
                        GetValueString = () => $"{Settings.drawDistance:F0}",
                        AdjustLeft = () => Settings.drawDistance = Math.Max(5f, Settings.drawDistance - 5f),
                        AdjustRight = () => Settings.drawDistance = Math.Min(100f, Settings.drawDistance + 5f),
                    },
                    new MenuItem { Label = "Back", Activate = GoBack },
                };

            case MenuPage.Gameplay:
                return new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "FOV",
                        GetValueString = () => $"{Settings.FOV:F2}",
                        AdjustLeft = () => Settings.FOV = Math.Max(0.3f, Settings.FOV - 0.1f),
                        AdjustRight = () => Settings.FOV = Math.Min(2.0f, Settings.FOV + 0.1f),
                    },
                    new MenuItem
                    {
                        Label = "Move Speed",
                        GetValueString = () => $"{Settings.moveSpeed:F1}",
                        AdjustLeft = () => Settings.moveSpeed = Math.Max(0.5f, Settings.moveSpeed - 0.5f),
                        AdjustRight = () => Settings.moveSpeed = Math.Min(10f, Settings.moveSpeed + 0.5f),
                    },
                    new MenuItem
                    {
                        Label = "Rotation Speed",
                        GetValueString = () => $"{Settings.rotationSpeed:F1}",
                        AdjustLeft = () => Settings.rotationSpeed = Math.Max(0.5f, Settings.rotationSpeed - 0.5f),
                        AdjustRight = () => Settings.rotationSpeed = Math.Min(10f, Settings.rotationSpeed + 0.5f),
                    },
                    new MenuItem
                    {
                        Label = "Mouse Sensitivity",
                        GetValueString = () => $"{Settings.mouseRotationSpeed:F1}",
                        AdjustLeft = () => Settings.mouseRotationSpeed = Math.Max(0.5f, Settings.mouseRotationSpeed - 0.5f),
                        AdjustRight = () => Settings.mouseRotationSpeed = Math.Min(10f, Settings.mouseRotationSpeed + 0.5f),
                    },
                    new MenuItem { Label = "Back", Activate = GoBack },
                };

            default:
                return new List<MenuItem>();
        }
    }
}