using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;
using static Settings;
using static RoomGenerator;
using PurgeProtocol.ComponentSystem;
using PurgeProtocol;

class Raycaster
{
    public struct RayHit
    {
        public int mapX;
        public int mapY;
        public float distance;
        public int side; // -1: not hit, 0: x-side, 1: y-side

        public RayHit(int mapX, int mapY, float distance, int side)
        {
            this.mapX = mapX;
            this.mapY = mapY;
            this.distance = distance;
            this.side = side;
        }

        public bool IsHit() => side >= 0;
    }

    static float accumulator = 0f;
    static int ups = 0;
    static int upsCount = 0;
    static double upsTimer = 0;

    // Performance metrics
    static double totalFrameTimeMs = 0;
    static double minFrameTime = double.MaxValue;
    static double maxFrameTime = 0;
    static double avgFrameTime = 0;
    static int frameCount = 0;
    static Stopwatch frameTimer = new Stopwatch();

    const int TEXTURE_SIZE = 64;

    static List<Entity> entities = new List<Entity>();

    // Player
    public static Entity playerEntity { get; private set; } = null;

    static HPAStar pathfindingSystem;

    // Colors
    static Color PlayerColor = new Color(0, 255, 0, 255);
    static Color EnemyColor = new Color(255, 0, 0, 255);

    // Textures
    static Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    static RenderTexture2D renderTarget;

    static Shader spriteShader;
    static Shader floorCeilingShader;

    static int playerPosLoc, playerDirLoc, cameraPlaneLoc, screenWidthLoc, screenHeightLoc, depthTexLoc;
    static int floorAtlasTexLoc, ceilingAtlasxTexLoc, floorIndexTexLoc, ceilingIndexTexLoc, camHeightLoc, horizonLoc;

    //Z depth
    static Texture2D depthTexture;
    static float[] zBuffer = new float[internalScreenWidth];
    static float maxZ = 0f;

    static RoomGenerator roomGenerator;
    static MenuSystem menu = new MenuSystem();

    static float interactionDistance = 1.0f;
    static float shootCooldown = 0f;
    const float SHOOT_COOLDOWN_TIME = 0.5f;

    // Ceiling and Floor texture arrays
    public static Texture2D floorAtlasTextures;
    public static Texture2D ceilingAtlasTextures;

    static void LoadTextures()
    {
        textures.Add("checkerBoard", LoadTexture("Assets/CheckerBoard.png"));
        textures.Add("crosshairs", LoadTexture("Assets/crosshairs_128.png"));

        //original
        //textures.Add("wall", LoadTexture("Assets/wall.png"));
        //textures.Add("door", LoadTexture("Assets/door.png"));
        //textures.Add("ceiling", LoadTexture("Assets/ceiling.png", true));
        //textures.Add("ceiling2", LoadTexture("Assets/ceiling2.png", true));
        //textures.Add("floor", LoadTexture("Assets/floor.png", true));
        //textures.Add("floor2", LoadTexture("Assets/floor2.png", true));

        //textures.Add("roach_red", LoadTexture("Assets/roach_red.png"));
        //textures.Add("roach_blue", LoadTexture("Assets/roach_blue.png"));
        //textures.Add("roach_green", LoadTexture("Assets/roach_green.png"));

        //textures.Add("electro_bullet", LoadTexture("Assets/electro_bullet.png"));

        //halloween mod
        textures.Add("scarecrow", LoadTexture("Assets/dungeon/scarecrow.png"));
        textures.Add("wall", LoadTexture("Assets/dungeon/dungeon_wall1.png"));
        textures.Add("door", LoadTexture("Assets/dungeon/dungeon_door2.png"));
        textures.Add("ceiling", LoadTexture("Assets/dungeon/dungeon_ceiling1.png", true));
        textures.Add("ceiling2", LoadTexture("Assets/dungeon/dungeon_ceiling2.png", true));
        textures.Add("floor", LoadTexture("Assets/dungeon/dungeon_floor1.png", true));
        textures.Add("floor2", LoadTexture("Assets/dungeon/dungeon_floor2.png", true));
        textures.Add("fireball", LoadTexture("Assets/dungeon/fireball.png"));

        textures.Add("bread", LoadTexture("Assets/dungeon/bread.png"));
        textures.Add("mushroom", LoadTexture("Assets/dungeon/mushroom.png"));
        textures.Add("pumpkin", LoadTexture("Assets/dungeon/pumpkin.png"));

        Texture2D LoadTexture(string path, bool repeat = false)
        {
            Texture2D tex = Raylib.LoadTexture(path);
            Raylib.SetTextureFilter(tex, TextureFilter.Point);
            Raylib.SetTextureWrap(tex, repeat ? TextureWrap.Repeat : TextureWrap.Clamp);
            //Raylib.GenTextureMipmaps(ref tex);
            return tex;
        }
    }

    static void LoadShaders()
    {
        //Billboard sprite shader
        spriteShader = Raylib.LoadShader(null, "Shaders/sprite.fs");

        //Floor and ceiling
        floorCeilingShader = Raylib.LoadShader(null, "Shaders/floor_ceiling.fs");

        playerPosLoc = Raylib.GetShaderLocation(floorCeilingShader, "playerPos");
        playerDirLoc = Raylib.GetShaderLocation(floorCeilingShader, "playerDir");
        cameraPlaneLoc = Raylib.GetShaderLocation(floorCeilingShader, "cameraPlane");
        screenWidthLoc = Raylib.GetShaderLocation(floorCeilingShader, "screenWidth");
        screenHeightLoc = Raylib.GetShaderLocation(floorCeilingShader, "screenHeight");

        floorAtlasTexLoc = Raylib.GetShaderLocation(floorCeilingShader, "floorTexture");
        ceilingAtlasxTexLoc = Raylib.GetShaderLocation(floorCeilingShader, "ceilingTexture");

        floorIndexTexLoc = Raylib.GetShaderLocation(floorCeilingShader, "floorIdTex");
        ceilingIndexTexLoc = Raylib.GetShaderLocation(floorCeilingShader, "ceilingIdTex");

        camHeightLoc = Raylib.GetShaderLocation(floorCeilingShader, "camHeight");
        horizonLoc = Raylib.GetShaderLocation(floorCeilingShader, "horizon");

        depthTexLoc = Raylib.GetShaderLocation(spriteShader, "depthTexture");
    }

    static void LoadMap()
    {
        if (floorAtlasTextures.Id != 0) Raylib.UnloadTexture(floorAtlasTextures);
        if (ceilingAtlasTextures.Id != 0) Raylib.UnloadTexture(ceilingAtlasTextures);
        floorAtlasTextures = default;
        ceilingAtlasTextures = default;

        roomGenerator = new RoomGenerator(50, 50, 2, 10, 2, 10, Settings.extraDoorChance);
        roomGenerator.Generate();
        GenerateFloorCeilingAtlasTextures();

        roomGenerator.PrintRoom(0);
        foreach (Room neighbour in roomGenerator.rooms[0].neighbourRooms) roomGenerator.PrintRoom(neighbour);

        pathfindingSystem = new HPAStar(roomGenerator);

        void GenerateFloorCeilingAtlasTextures()
        {
            const int TilesPerRow = 16;

            Image floorAtlasImage = Raylib.GenImageColor(TEXTURE_SIZE * TilesPerRow, TEXTURE_SIZE * TilesPerRow, Color.Blank);
            foreach (var kv in roomGenerator.floorTextureIds)
            {
                int index = kv.Value;
                Image img = Raylib.LoadImage($"Assets/dungeon/dungeon_{kv.Key}.png");
                int x = (index % TilesPerRow) * TEXTURE_SIZE;
                int y = (index / TilesPerRow) * TEXTURE_SIZE;
                Raylib.ImageDraw(ref floorAtlasImage, img, new Rectangle(0, 0, TEXTURE_SIZE, TEXTURE_SIZE), new Rectangle(x, y, TEXTURE_SIZE, TEXTURE_SIZE), Color.White);
                Raylib.UnloadImage(img);
            }
            floorAtlasTextures = Raylib.LoadTextureFromImage(floorAtlasImage);
            Raylib.UnloadImage(floorAtlasImage);

            Image ceilingAtlasImage = Raylib.GenImageColor(TEXTURE_SIZE * TilesPerRow, TEXTURE_SIZE * TilesPerRow, Color.Blank);
            foreach (var kv in roomGenerator.ceilingTextureIds)
            {
                int index = kv.Value;
                Image img = Raylib.LoadImage($"Assets/dungeon/dungeon_{kv.Key}.png");
                int x = (index % TilesPerRow) * TEXTURE_SIZE;
                int y = (index / TilesPerRow) * TEXTURE_SIZE;
                Raylib.ImageDraw(ref ceilingAtlasImage, img, new Rectangle(0, 0, TEXTURE_SIZE, TEXTURE_SIZE), new Rectangle(x, y, TEXTURE_SIZE, TEXTURE_SIZE), Color.White);
                Raylib.UnloadImage(img);
            }
            ceilingAtlasTextures = Raylib.LoadTextureFromImage(ceilingAtlasImage);
            Raylib.UnloadImage(ceilingAtlasImage);
        }

        Raylib.SetTextureFilter(floorAtlasTextures, TextureFilter.Point);
        Raylib.SetTextureWrap(floorAtlasTextures, TextureWrap.Clamp);
        Raylib.SetTextureFilter(ceilingAtlasTextures, TextureFilter.Point);
        Raylib.SetTextureWrap(ceilingAtlasTextures, TextureWrap.Clamp);
    }

    private static void LoadEnemys()
    {
        float spawnChance = Settings.enemySpawnChance;
        Room playerRoom = roomGenerator.GetRoomAtPosition(playerEntity.transform.Position);

        foreach (Room room in roomGenerator.rooms)
        {
            if (room == playerRoom) continue;

            foreach (Vector2IntR pos in room.coords)
            {
                if (RandomR.value < spawnChance)
                {
                    if (!roomGenerator.tileMap[pos.x, pos.y].isWalkable) continue;

                    int baseHP = 1 + Settings.difficulty;
                    int hp = baseHP + RandomR.Range(0, 2);

                    Texture2D texture = textures["scarecrow"];
                    Vector2 startPosition = new Vector2(pos.x + 0.5f, pos.y + 0.5f);
                    entities.Add(CreateRoach(texture, startPosition, hp));
                }
            }
        }

        Console.WriteLine($"Loaded {entities.Count} scarecrows");

        Entity CreateRoach(Texture2D sprite, Vector2 startPosition, int hp)
        {
            Entity roach = new Entity();
            roach.AddComponent(new Transform { Position = startPosition });
            roach.AddComponent(new HealthComponent(hp));
            roach.AddComponent(new RaySpriteRenderer { Texture = sprite });
            roach.AddComponent(new RoachAI());
            roach.GetComponent<RoachAI>().Initialize(pathfindingSystem, roomGenerator);
            return roach;
        }
    }

    static void CreatePlayer()
    {
        playerEntity = new Entity();
        playerEntity.AddComponent(new Transform { Position = new Vector2(1.5f, 1.5f) });

        playerEntity.AddComponent(new PlayerController
        {
            MoveSpeed = 5.0f,
            RotationSpeed = 3.0f,
            MouseRotationSpeed = 0.1f
        });

        int playerHP = Settings.difficulty switch { 0 => 15, 1 => 10, 2 => 5, _ => 10 };
        playerEntity.AddComponent(new HealthComponent(playerHP));

        playerEntity.AddComponent(new ManaComponent(50));
        playerEntity.manaComponent.TakeMana(20);
    }

    private static void CreateItems()
    {
        int spawnedMushroom = 0;
        int spawnedBread = 0;

        foreach (Room room in roomGenerator.rooms)
        {
            int randomItems = RandomR.Range(0, 4);

            if (randomItems == 0) continue;

            List<Vector2> itemCoords = new List<Vector2>(randomItems);
            List<Vector2IntR> roomCoords = new List<Vector2IntR>(room.coords);

            if (roomCoords.Count < itemCoords.Count) continue;

            for (int i = 0; i < randomItems; i++)
            {
                itemCoords.Add(roomCoords[i] + new Vector2(0.5f, 0.5f));
            }

            room.items = new List<Entity>(randomItems);

            foreach (Vector2 itemCoord in itemCoords)
            {
                int effectType = Math.Min(RandomR.Range(0, 2), 1); //0 hp 1 mana

                Entity item = new Entity();

                Transform transform = item.AddComponent<Transform>(new Transform()
                {
                    Position = itemCoord
                });

                RaySpriteRenderer renderer = item.AddComponent<RaySpriteRenderer>(new RaySpriteRenderer());

                switch (effectType)
                {
                    case 0:
                        spawnedBread++;
                        renderer.Texture = textures["bread"];
                        break;
                    case 1:
                        spawnedMushroom++;
                        renderer.Texture = textures["mushroom"];
                        break;
                    default:
                        break;
                }

                renderer.spriteScale = 0.2f;

                PowerUpComponent powerUp = item.AddComponent<PowerUpComponent>(new PowerUpComponent
                {
                    effectType = effectType
                });

                entities.Add(item);
                room.items.Add(item);
            }

            Console.WriteLine($"Loaded {itemCoords.Count} item for room {room.GetHashCode()}");
            Console.WriteLine($"Spawned {spawnedMushroom} mushrooms");
            Console.WriteLine($"Spawned {spawnedBread} breads");
        }
    }

    //Create Depth texture with the internalScreenWidth of the screen and 1 pixel height storing normalized 32 bit float ZBuffer values in red channel
    static Texture2D CreateDepthTexture()
    {
        Image depthImage = Raylib.GenImageColor(internalScreenWidth, 1, Color.Black);
        // Use 32-bit float format for depth
        Raylib.ImageFormat(ref depthImage, PixelFormat.UncompressedR32);
        Texture2D tex = Raylib.LoadTextureFromImage(depthImage);
        Raylib.UnloadImage(depthImage);

        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        Raylib.TraceLog(TraceLogLevel.Info, $"Created depth texture: {tex.Width}x{tex.Height}");

        return tex;
    }

    //Write Zbuffer array into depth texture
    static void UpdateDepthTexture()
    {
        // Convert float array to byte array
        byte[] depthBytes = new byte[zBuffer.Length * sizeof(float)];
        Buffer.BlockCopy(zBuffer, 0, depthBytes, 0, depthBytes.Length);

        // Update texture
        Raylib.UpdateTexture(depthTexture, depthBytes);
    }

    static void DrawPlayerHealth()
    {
        if (playerEntity == null || playerEntity.healthComponent == null) return;

        int barWidth = 200;
        int barHeight = 20;
        int posX = 10;
        int posY = screenHeight - barHeight - 10;  // Positioned at bottom-left

        // Draw background
        Raylib.DrawRectangle(posX, posY, barWidth, barHeight, new Color(50, 50, 50, 200));

        // Calculate health percentage
        float healthPercent = (float)playerEntity.healthComponent.current / playerEntity.healthComponent.max;
        int fillWidth = (int)(barWidth * healthPercent);

        // Gradient color based on health
        Color fillColor;
        if (healthPercent > 0.6f)
            fillColor = new Color(40, 180, 40, 255);  // Healthy green
        else if (healthPercent > 0.3f)
            fillColor = new Color(220, 150, 40, 255); // Warning orange
        else
            fillColor = new Color(200, 40, 40, 255);  // Danger red

        // Draw health bar with border
        Raylib.DrawRectangle(posX, posY, fillWidth, barHeight, fillColor);
        Raylib.DrawRectangleLines(posX, posY, barWidth, barHeight, Color.Black);

        // Draw health text with shadow
        string healthText = $"HP: {playerEntity.healthComponent.current}/{playerEntity.healthComponent.max}";
        int textX = posX + 5;  // Left-aligned
        int textY = posY + (barHeight - 20) / 2;

        Raylib.DrawText(healthText, textX + 1, textY + 1, 20, Color.Black);
        Raylib.DrawText(healthText, textX, textY, 20, Color.White);
    }

    static void DrawPlayerMana()
    {
        if (playerEntity == null || playerEntity.manaComponent == null) return;

        int barWidth = 200;
        int barHeight = 20;
        int posX = 10 + barWidth + 40; // barWidth is healthbar width!
        int posY = screenHeight - barHeight - 10;  // Positioned at bottom-left

        // Draw background
        Raylib.DrawRectangle(posX, posY, barWidth, barHeight, new Color(50, 50, 50, 200));

        // Calculate mana percentage
        float manaPercent = (float)playerEntity.manaComponent.current / playerEntity.manaComponent.max;
        int fillWidth = (int)(barWidth * manaPercent);

        // Gradient color based on mana
        Color fillColor;
        if (manaPercent > 0.6f)
            fillColor = new Color(40, 180, 40, 255);  // Healthy green
        else if (manaPercent > 0.3f)
            fillColor = new Color(220, 150, 40, 255); // Warning orange
        else
            fillColor = new Color(200, 40, 40, 255);  // Danger red

        // Draw mana bar with border
        Raylib.DrawRectangle(posX, posY, fillWidth, barHeight, fillColor);
        Raylib.DrawRectangleLines(posX, posY, barWidth, barHeight, Color.Black);

        // Draw mana text with shadow
        string manaText = $"Mana: {playerEntity.manaComponent.current}/{playerEntity.manaComponent.max}";
        int textX = posX + 5;  // Left-aligned
        int textY = posY + (barHeight - 20) / 2;

        Raylib.DrawText(manaText, textX + 1, textY + 1, 20, Color.Black);
        Raylib.DrawText(manaText, textX, textY, 20, Color.White);
    }

    static void DrawPerformanceMetrics()
    {
        int startX = 10;
        int startY = 40;
        int lineHeight = 20;

        // Draw background panel
        Raylib.DrawRectangle(startX - 5, startY - 5, 320, 230, new Color(0, 0, 0, 180));
        Raylib.DrawRectangleLines(startX - 5, startY - 5, 320, 230, Color.DarkGray);

        // Draw metrics
        Raylib.DrawText($"Ray Loop: {PerformanceMonitor.RayLoopTime:F2} ms", startX, startY, lineHeight, Color.Green);
        Raylib.DrawText($"Sprite draw: {PerformanceMonitor.SpriteDrawTime:F2} ms", startX, startY + lineHeight, lineHeight, Color.Green);
        Raylib.DrawText($"Z-Buffer draw: {PerformanceMonitor.zBufferDrawTime:F2} ms", startX, startY + lineHeight * 2, lineHeight, Color.Green);
        Raylib.DrawText($"Depth Texture: {PerformanceMonitor.depthTextureTime:F2} ms", startX, startY + lineHeight * 3, lineHeight, Color.Green);
        Raylib.DrawText($"Fixed Update time: {PerformanceMonitor.fixedUpdateTime:F2} ms", startX, startY + lineHeight * 4, lineHeight, Color.Lime);
        Raylib.DrawText($"Total Frame time: {totalFrameTimeMs:F2} ms", startX, startY + lineHeight * 5, lineHeight, Color.Yellow);
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", startX, startY + lineHeight * 6, lineHeight, Color.Yellow);
        Raylib.DrawText($"UPS: {ups}", startX, startY + lineHeight * 7, lineHeight, Color.SkyBlue);

        // Min/Max/Avg
        Raylib.DrawText($"Min: {minFrameTime:F2} ms", startX, startY + lineHeight * 8, lineHeight, Color.Orange);
        Raylib.DrawText($"Max: {maxFrameTime:F2} ms", startX, startY + lineHeight * 9, lineHeight, Color.Orange);
        Raylib.DrawText($"Avg: {avgFrameTime:F2} ms", startX, startY + lineHeight * 10, lineHeight, Color.Orange);

        //Raylib.DrawTexture(floorAtlasTextures, startX + 500, 10, Color.White);
        //Raylib.DrawTexture(ceilingAtlasTextures, startX + 500, 250, Color.White);
    }

    static void DrawFloorAndCeiling()
    {
        Raylib.BeginShaderMode(floorCeilingShader);

        // Set shader uniforms
        Raylib.SetShaderValue(floorCeilingShader, playerPosLoc, playerEntity.transform.Position, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(floorCeilingShader, playerDirLoc, playerEntity.playerController.Direction, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(floorCeilingShader, cameraPlaneLoc, playerEntity.playerController.CameraPlane, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(floorCeilingShader, screenWidthLoc, (float)internalScreenWidth, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(floorCeilingShader, screenHeightLoc, (float)internalScreenHeight, ShaderUniformDataType.Float);

        Raylib.SetShaderValue(floorCeilingShader, camHeightLoc, (float)0.5f, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(floorCeilingShader, horizonLoc, (float)(internalScreenHeight / 2f), ShaderUniformDataType.Float);

        // Set textures
        Raylib.SetShaderValueTexture(floorCeilingShader, floorAtlasTexLoc, floorAtlasTextures);
        Raylib.SetShaderValueTexture(floorCeilingShader, ceilingAtlasxTexLoc, ceilingAtlasTextures);
        Raylib.SetShaderValueTexture(floorCeilingShader, floorIndexTexLoc, roomGenerator.floorIdTex);
        Raylib.SetShaderValueTexture(floorCeilingShader, ceilingIndexTexLoc, roomGenerator.ceilingIdTex);

        // Draw full-screen quad
        Raylib.DrawRectangle(0, 0, internalScreenWidth, internalScreenHeight, Color.White);

        Raylib.EndShaderMode();
    }

    static void DrawEntitys()
    {
        //Draw only visible entities in their room neighbour rooms and their neighbour rooms -> 2 room distance
        // Player room Door -> N1 -> Door -> N2
        List<Entity> visibleEntities = new List<Entity>(entities.Count);
        Room playerRoom = roomGenerator.GetRoomAtPosition(playerEntity.transform.Position);

        foreach (Entity entity in entities)
        {
            Room entityRoom = roomGenerator.GetRoomAtPosition(entity.transform.Position);
            float entityDistance = Vector2.Distance(playerEntity.transform.Position, entity.transform.Position); //TODO distance squared

            // Entity is in the same room as player
            if (playerRoom == entityRoom)
            {
                if (entityDistance <= drawDistance) visibleEntities.Add(entity);
                continue;
            }

            // Entity is in a neighbour room of the player
            foreach (Room neighbour in playerRoom.neighbourRooms)
            {
                if (neighbour == entityRoom)
                {
                    if (entityDistance <= drawDistance) visibleEntities.Add(entity);
                    goto nextEntity; // Skip to next entity
                }

                // Entity is in a neighbour room of the neighbour -> 2 room distance
                foreach (Room neighbourOfNeighbour in neighbour.neighbourRooms)
                {
                    if (neighbourOfNeighbour == entityRoom)
                    {
                        if (entityDistance <= drawDistance) visibleEntities.Add(entity);
                        goto nextEntity; // Skip to next entity
                    }
                }
            }

        nextEntity:;
        }

        // Sort Entites by distance (far to near)
        visibleEntities.Sort((a, b) =>
        {
            float distA = Vector2.DistanceSquared(a.transform.Position, playerEntity.transform.Position);
            float distB = Vector2.DistanceSquared(b.transform.Position, playerEntity.transform.Position);
            return distB.CompareTo(distA);
        });

        Raylib.BeginBlendMode(BlendMode.Alpha);

        //Set Depth texture for Sprite shader
        Raylib.SetShaderValueTexture(spriteShader, depthTexLoc, depthTexture);

        foreach (Entity entity in visibleEntities)
        {
            entity.raySpriteRenderer.Draw(playerEntity, spriteShader, internalScreenWidth, internalScreenHeight);
        }

        Raylib.EndBlendMode();
    }

    static void DrawZBuffer()
    {
        if (maxZ <= 0) return;

        // Draw depth buffer as a gradient at the bottom
        int debugHeight = 10;

        for (int y = 0; y < debugHeight; y++)
        {
            for (int x = 0; x < internalScreenWidth; x++)
            {
                if (zBuffer[x] == float.MaxValue) continue;

                float normalized = zBuffer[x] / maxZ;
                float brightness = 1.0f - normalized;

                // Create gradient effect vertically
                float verticalFactor = 1.0f - (float)y / debugHeight;
                brightness *= verticalFactor;

                // Apply exponential falloff for better visual range
                brightness = MathF.Pow(brightness, 0.5f);

                int intensity = (int)(brightness * 255);
                Color color = new Color(intensity, intensity, intensity, 255);

                Raylib.DrawPixel(x, internalScreenHeight - debugHeight + y, color);
            }
        }

        // Draw depth scale markers
        for (int i = 1; i <= 10; i++)
        {
            float depth = zBuffer[internalScreenWidth / 10 * i - 1];
            string text = $"{depth:F1}";
            int yPos = internalScreenHeight - 20;
            Raylib.DrawText(text, i * internalScreenWidth / 10, yPos, 4, Color.Red);
        }
    }

    static void DrawMinimap()
    {
        const int MAP_SCALE = 5;
        int MAP_SIZE = roomGenerator.tileMap.GetLength(0);
        Vector2 MAP_POS = new Vector2(screenWidth - MAP_SIZE * MAP_SCALE - 10, 10);

        // Draw map tiles
        for (int y = 0; y < MAP_SIZE; y++)
        {
            for (int x = 0; x < MAP_SIZE; x++)
            {
                Raylib.DrawRectangle(
                    (int)MAP_POS.X + x * MAP_SCALE,
                    (int)MAP_POS.Y + y * MAP_SCALE,
                    MAP_SCALE,
                    MAP_SCALE,
                    roomGenerator.tileMap[x, y].GetMinimapColor()
                );
            }
        }

        // Draw player
        int playerMapX = (int)(MAP_POS.X + playerEntity.transform.Position.X * MAP_SCALE);
        int playerMapY = (int)(MAP_POS.Y + playerEntity.transform.Position.Y * MAP_SCALE);
        Raylib.DrawCircle(playerMapX, playerMapY, 3, PlayerColor);

        // Draw direction line
        Vector2 endPos = new Vector2(
            playerMapX + playerEntity.playerController.Direction.X * 15,
            playerMapY + playerEntity.playerController.Direction.Y * 15
        );
        Raylib.DrawLine(playerMapX, playerMapY, (int)endPos.X, (int)endPos.Y, Color.White);

        // Draw Enemys
        foreach (Entity entity in entities)
        {
            int posX = (int)(MAP_POS.X + entity.transform.Position.X * MAP_SCALE);
            int posY = (int)(MAP_POS.Y + entity.transform.Position.Y * MAP_SCALE);
            Raylib.DrawCircle(posX, posY, 3, EnemyColor);
        }
    }

    // Draw crosshairs at the center of the screen
    private static void DrawMouse()
    {
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;
        Raylib.DrawTexture(textures["crosshairs"], centerX - textures["crosshairs"].Width / 2, centerY - textures["crosshairs"].Height / 2, Color.White);
    }

    public static RayHit CastDDA(Vector2 rayDir, Vector2 position, TileCell[,] map) => CastDDA(rayDir.X, rayDir.Y, position.X, position.Y, map);
    public static RayHit CastDDA(float rayDirX, float rayDirY, float posX, float posY, TileCell[,] map)
    {
        Vector2 rayDir = new Vector2(rayDirX, rayDirY);

        // DDA setup
        int mapX = (int)posX;
        int mapY = (int)posY;

        // Avoid division by zero
        Vector2 deltaDist = new Vector2(
            (rayDir.X == 0) ? float.MaxValue : MathF.Abs(1 / rayDir.X),
            (rayDir.Y == 0) ? float.MaxValue : MathF.Abs(1 / rayDir.Y)
        );

        Vector2 sideDist;
        int stepX = rayDir.X < 0 ? -1 : 1;
        int stepY = rayDir.Y < 0 ? -1 : 1;

        // Initial side distances
        sideDist.X = rayDir.X < 0 ?
            (posX - mapX) * deltaDist.X :
            (mapX + 1.0f - posX) * deltaDist.X;

        sideDist.Y = rayDir.Y < 0 ?
            (posY - mapY) * deltaDist.Y :
            (mapY + 1.0f - posY) * deltaDist.Y;

        // DDA
        int side = 0;
        bool hit = false;

        while (!hit)
        {
            if (sideDist.X < sideDist.Y)
            {
                sideDist.X += deltaDist.X;
                mapX += stepX;
                side = 0;
            }
            else
            {
                sideDist.Y += deltaDist.Y;
                mapY += stepY;
                side = 1;
            }

            // Check boundaries
            if (mapX < 0 || mapX >= map.GetLength(0) || mapY < 0 || mapY >= map.GetLength(1)) break;

            if (!roomGenerator.IsWalkable(new Vector2IntR(mapX, mapY))) hit = true;
        }

        if (hit)
        {
            // Calculate distance
            float perpWallDist = side == 0 ?
                (mapX - posX + (1 - stepX) * 0.5f) / rayDir.X :
                (mapY - posY + (1 - stepY) * 0.5f) / rayDir.Y;

            perpWallDist = MathF.Abs(perpWallDist);
            perpWallDist = MathF.Max(perpWallDist, 0.01f);

            return new RayHit(mapX, mapY, perpWallDist, side);
        }
        else // No hit
        {
            return new RayHit(-1, -1, float.MaxValue, -1);
        }
    }

    static void CastRays()
    {
        Raylib.BeginTextureMode(renderTarget);

        DrawFloorAndCeiling();

        // Reset z-buffer
        Array.Fill(zBuffer, 10000f);
        maxZ = 0f;

        using (PerformanceMonitor.Measure(t => PerformanceMonitor.RayLoopTime = t))
        {
            for (int x = 0; x < internalScreenWidth; x++)
            {
                // Calculate ray direction with internal width
                float cameraX = 2 * x / (float)internalScreenWidth - 1;
                Vector2 rayDir = new Vector2(
                    playerEntity.playerController.Direction.X + playerEntity.playerController.CameraPlane.X * cameraX,
                    playerEntity.playerController.Direction.Y + playerEntity.playerController.CameraPlane.Y * cameraX
                );

                // DDA setup
                int mapX = (int)playerEntity.transform.Position.X;
                int mapY = (int)playerEntity.transform.Position.Y;

                // Avoid division by zero
                Vector2 deltaDist = new Vector2(
                    (rayDir.X == 0) ? float.MaxValue : MathF.Abs(1 / rayDir.X),
                    (rayDir.Y == 0) ? float.MaxValue : MathF.Abs(1 / rayDir.Y)
                );

                Vector2 sideDist;
                int stepX = rayDir.X < 0 ? -1 : 1;
                int stepY = rayDir.Y < 0 ? -1 : 1;

                // Initial side distances
                sideDist.X = rayDir.X < 0 ?
                    (playerEntity.transform.Position.X - mapX) * deltaDist.X :
                    (mapX + 1.0f - playerEntity.transform.Position.X) * deltaDist.X;

                sideDist.Y = rayDir.Y < 0 ?
                    (playerEntity.transform.Position.Y - mapY) * deltaDist.Y :
                    (mapY + 1.0f - playerEntity.transform.Position.Y) * deltaDist.Y;

                // DDA
                int side = 0;
                bool hit = false;

                while (!hit)
                {
                    if (sideDist.X < sideDist.Y)
                    {
                        sideDist.X += deltaDist.X;
                        mapX += stepX;
                        side = 0;
                    }
                    else
                    {
                        sideDist.Y += deltaDist.Y;
                        mapY += stepY;
                        side = 1;
                    }

                    // Check boundaries
                    if (mapX < 0 || mapX >= roomGenerator.tileMap.GetLength(0) || mapY < 0 || mapY >= roomGenerator.tileMap.GetLength(1)) break;
                    if (!roomGenerator.IsWalkable(new Vector2IntR(mapX, mapY))) hit = true;
                }

                if (hit)
                {
                    // Calculate distance
                    float perpWallDist = side == 0 ?
                        (mapX - playerEntity.transform.Position.X + (1 - stepX) * 0.5f) / rayDir.X :
                        (mapY - playerEntity.transform.Position.Y + (1 - stepY) * 0.5f) / rayDir.Y;

                    perpWallDist = MathF.Abs(perpWallDist);
                    perpWallDist = MathF.Max(perpWallDist, 0.01f);

                    //ZBuffer
                    zBuffer[x] = perpWallDist;
                    if (zBuffer[x] > maxZ && zBuffer[x] < 10000f) maxZ = zBuffer[x];

                    // Calculate wall height with maximum cap
                    int lineHeight = (int)(internalScreenHeight / perpWallDist);
                    int maxWallHeight = internalScreenHeight;
                    lineHeight = Math.Min(lineHeight, maxWallHeight);

                    int drawStart = Math.Clamp(-lineHeight / 2 + internalScreenHeight / 2, 0, internalScreenHeight);
                    int drawEnd = Math.Clamp(lineHeight / 2 + internalScreenHeight / 2, 0, internalScreenHeight);

                    // Calculate texture coordinates with offset based on clamping
                    float texOffsetY = 0f;
                    if (lineHeight >= maxWallHeight)
                    {
                        // When capped, calculate vertical offset to show center of texture
                        float overflow = (internalScreenHeight / perpWallDist) - maxWallHeight;
                        texOffsetY = (overflow / 2) / (internalScreenHeight / perpWallDist);
                    }

                    float sampleHeight = 1f - texOffsetY * 2; // Portion of texture to show
                    int texYStart = (int)(TEXTURE_SIZE * texOffsetY);
                    int texYEnd = (int)(TEXTURE_SIZE * (1 - texOffsetY));

                    // Calculate texture x-coordinate
                    float wallX = side == 0 ?
                        playerEntity.transform.Position.Y + perpWallDist * rayDir.Y :
                        playerEntity.transform.Position.X + perpWallDist * rayDir.X;
                    wallX -= MathF.Floor(wallX);

                    int texX = (int)(wallX * TEXTURE_SIZE);
                    if ((side == 0 && rayDir.X > 0) || (side == 1 && rayDir.Y < 0))
                        texX = TEXTURE_SIZE - texX - 1;

                    // Shading factors
                    float shade = Math.Clamp(1.0f - perpWallDist * 0.1f, 0.2f, 1.0f);
                    float darken = side == 1 ? 0.6f : 1.0f;
                    Color tint = new Color(
                        (byte)(255 * shade * darken),
                        (byte)(255 * shade * darken),
                        (byte)(255 * shade * darken),
                        (byte)255
                    );

                    // Draw vertical strip using texture
                    Rectangle srcRect = new Rectangle(
                        texX,
                        texYStart,
                        1,
                        Math.Max(1, texYEnd - texYStart) // Ensure at least 1 pixel height
                    );

                    Rectangle destRect = new Rectangle(
                        x,
                        drawStart,
                        1,
                        drawEnd - drawStart
                    );

                    TileCell cell = roomGenerator.tileMap[mapX, mapY];
                    string textureName;

                    // Try to get a Wall tile first
                    Wall wallTile = cell.GetTile<Wall>();
                    if (wallTile != null)
                    {
                        textureName = wallTile.textureName;
                    }
                    else
                    {
                        // If the cell contains a Door, use its texture
                        Door doorTile = cell.GetTile<Door>();
                        if (doorTile != null)
                        {
                            textureName = doorTile.textureName;      // e.g. "door"
                        }
                        else
                        {
                            // Fallback – should never happen, but protects against bugs
                            textureName = "wall";
                        }
                    }

                    Raylib.DrawTexturePro(
                        textures[textureName],
                        srcRect,
                        destRect,
                        Vector2.Zero,
                        0f,
                        tint
                    );
                }
            }
        }

        using (PerformanceMonitor.Measure(t => PerformanceMonitor.depthTextureTime = t))
        {
            // Update depth texture with z-buffer
            UpdateDepthTexture();
        }

        using (PerformanceMonitor.Measure(t => PerformanceMonitor.SpriteDrawTime = t))
        {
            DrawEntitys();
        }

        //using (PerformanceMonitor.Measure(t => PerformanceMonitor.zBufferDrawTime = t))
        //{
        //    DrawZBuffer();
        //}

        Raylib.EndTextureMode();
    }

    private static Entity CastShootingRay(out float hitDistance)
    {
        hitDistance = CastDDA(playerEntity.playerController.Direction, playerEntity.transform.Position, roomGenerator.tileMap).distance; // Distance to wall hit

        Entity hitEnemy = null;
        float closestT = float.MaxValue;
        const float spriteRadius = 0.4f; // Sprite collision radius

        foreach (Entity entity in entities)
        {
            //skip powerups
            if (entity.powerUpComponent != null) continue;

            Vector2 toSprite = entity.transform.Position - playerEntity.transform.Position;
            float t = Vector2.Dot(toSprite, playerEntity.playerController.Direction);

            // Skip if behind player or too close
            if (t < 0.1f) continue;

            Vector2 closestPoint = playerEntity.transform.Position + t * playerEntity.playerController.Direction;
            float distanceSq = Vector2.DistanceSquared(closestPoint, entity.transform.Position);

            // Check if within sprite radius and closer than wall
            if (distanceSq < spriteRadius * spriteRadius &&
                t < hitDistance &&
                t < closestT)
            {
                closestT = t;
                hitEnemy = entity;
            }
        }

        return hitEnemy;
    }

    private static void Interact()
    {
        RayHit hit = CastDDA(playerEntity.playerController.Direction, playerEntity.transform.Position, roomGenerator.tileMap);

        if (!hit.IsHit() || hit.distance > interactionDistance) return;

        Door door = roomGenerator.tileMap[hit.mapX, hit.mapY].GetTile<Door>();
        if (door != null)
        {
            roomGenerator.tileMap[hit.mapX, hit.mapY].ToggleDoor();
            Console.WriteLine($"Interacting with Door {door.GetHashCode()} -> " + (door.isOpen ? "opened" : "closed"));
        }
    }

    private static int CountEnemies()
    {
        int count = 0;

        foreach (Entity e in entities)
        {
            if (e.GetComponent<RoachAI>() != null && e.healthComponent != null && e.healthComponent.current > 0) count++;
        }

        return count;
    }

    // setup everything needed for a fresh run
    static void InitializeGame()
    {
        // Recreate GPU/CPU resources if resolution changed
        if (zBuffer.Length != internalScreenWidth) zBuffer = new float[internalScreenWidth];

        if (renderTarget.Id != 0) Raylib.UnloadRenderTexture(renderTarget);
        renderTarget = Raylib.LoadRenderTexture(internalScreenWidth, internalScreenHeight);

        if (depthTexture.Id != 0) Raylib.UnloadTexture(depthTexture);
        depthTexture = CreateDepthTexture();

        Raylib.SetTargetFPS(targetFPS);

        // Reset accumulators
        accumulator = 0f;
        upsCount = 0;
        upsTimer = 0;

        minFrameTime = double.MaxValue;
        maxFrameTime = 0;
        avgFrameTime = 0;
        frameCount = 0;
        totalFrameTimeMs = 0;

        InputSystem.Clear();

        entities.Clear();
        Debug.Print("Loading map...");
        LoadMap();
        Debug.Print("Creating player...");
        CreatePlayer();
        Debug.Print("Loading enemies...");
        LoadEnemys();
        Debug.Print("Creating items...");
        CreateItems();

        Raylib.SetMousePosition(screenWidth / 2, screenHeight / 2);

        GameSession.StartGame();
    }

    // release everything tied to a single run
    static void CleanupGame()
    {
        entities.Clear();
        playerEntity = null;
        pathfindingSystem = null;

        if (roomGenerator != null)
        {
            roomGenerator.Cleanup();
            roomGenerator = null;
        }

        if (floorAtlasTextures.Id != 0) Raylib.UnloadTexture(floorAtlasTextures);
        if (ceilingAtlasTextures.Id != 0) Raylib.UnloadTexture(ceilingAtlasTextures);
        floorAtlasTextures = default;
        ceilingAtlasTextures = default;
    }

    // render the game view + HUD
    static void RenderGame()
    {
        CastRays();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        Rectangle source = new Rectangle(0, 0, internalScreenWidth, -internalScreenHeight);
        Rectangle dest = new Rectangle(0, 0, screenWidth, screenHeight);
        Raylib.DrawTexturePro(renderTarget.Texture, source, dest, Vector2.Zero, 0f, Color.White);

        DrawPerformanceMetrics();
        DrawMinimap();
        Raylib.DrawFPS(10, 10);
        DrawPlayerHealth();
        DrawPlayerMana();
        DrawMouse();

        Raylib.EndDrawing();
    }

    // victory/defeat screen showing elapsed time
    static void DrawEndScreen()
    {
        string title = GameSession.didWin ? "VICTORY!" : "GAME OVER";
        Color titleColor = GameSession.didWin ? new Color(100, 255, 100, 255) : new Color(255, 100, 100, 255);

        int titleSize = 64;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (screenWidth - titleWidth) / 2, screenHeight / 2 - 150, titleSize, titleColor);

        string subtitle = GameSession.didWin ? "You found the artifact!" : "You have been defeated.";
        int subSize = 26;
        int subWidth = Raylib.MeasureText(subtitle, subSize);
        Raylib.DrawText(subtitle, (screenWidth - subWidth) / 2, screenHeight / 2 - 60, subSize, Color.LightGray);

        string timeText = $"Time: {GameSession.FormatElapsed()}";
        int timeSize = 36;
        int timeWidth = Raylib.MeasureText(timeText, timeSize);
        Raylib.DrawText(timeText, (screenWidth - timeWidth) / 2, screenHeight / 2 + 20, timeSize, Color.Yellow);

        string inst = "Press ENTER to return to Main Menu";
        int instSize = 20;
        int instWidth = Raylib.MeasureText(inst, instSize);
        Raylib.DrawText(inst, (screenWidth - instWidth) / 2, screenHeight / 2 + 130, instSize, Color.Gray);
    }

    static void Main()
    {
        Console.WriteLine("Initializing ...");
        LoadSettings();
        Console.WriteLine("Loading ...");
        Raylib.InitWindow(screenWidth, screenHeight, "Purge Protocol");
        Raylib.HideCursor();
        Raylib.SetWindowFocused();
        Raylib.SetWindowPosition(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
        if (borderlessWindowed) Raylib.ToggleBorderlessWindowed();
        Raylib.SetTargetFPS(targetFPS);

        frameTimer.Start();
        Stopwatch gameTimer = Stopwatch.StartNew();
        double lastTime = gameTimer.Elapsed.TotalSeconds;
        double currentTime = lastTime;

        LoadTextures();
        LoadShaders();

        // state machine 
        GameState gameState = GameState.MainMenu;
        bool shouldExit = false;

        while (!shouldExit && !Raylib.WindowShouldClose())
        {
            currentTime = gameTimer.Elapsed.TotalSeconds;
            double rawFrameTime = currentTime - lastTime;
            lastTime = currentTime;
            double frameTime = Math.Min(rawFrameTime, 0.25);
            totalFrameTimeMs = rawFrameTime * 1000.0;

            switch (gameState)
            {
                // MAIN MENU
                case GameState.MainMenu:
                    menu.Update();

                    if (menu.wantsToStartGame)
                    {
                        Settings.SaveSettings();
                        InitializeGame();
                        gameState = GameState.Playing;
                        break;
                    }
                    if (menu.wantsToExit)
                    {
                        Settings.SaveSettings();
                        shouldExit = true;
                        break;
                    }

                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(new Color(15, 15, 25, 255));
                    menu.Draw(screenWidth, screenHeight);
                    Raylib.EndDrawing();
                    break;

                // PLAYING
                case GameState.Playing:
                    InputSystem.Poll();

                    if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                    {
                        CleanupGame();
                        menu.Reset();
                        gameState = GameState.MainMenu;
                        break;
                    }

                    // Perf stats
                    if (totalFrameTimeMs < minFrameTime) minFrameTime = totalFrameTimeMs;
                    if (totalFrameTimeMs > maxFrameTime) maxFrameTime = totalFrameTimeMs;
                    frameCount++;
                    if (frameCount >= 60)
                    {
                        avgFrameTime = (avgFrameTime * 0.9) + (totalFrameTimeMs * 0.1);
                        frameCount = 0;
                    }

                    accumulator += (float)frameTime;
                    while (accumulator >= fixedDeltaTime)
                    {
                        using (PerformanceMonitor.Measure(t => PerformanceMonitor.fixedUpdateTime = t))
                        {
                            FixedUpdate();
                        }
                        accumulator -= fixedDeltaTime;
                        upsCount++;
                    }

                    upsTimer += frameTime;
                    if (upsTimer >= 1.0)
                    {
                        ups = upsCount;
                        upsCount = 0;
                        upsTimer -= 1.0;
                    }

                    if (accumulator < fixedDeltaTime * 3)
                    {
                        RenderGame();
                    }

                    // detect defeat
                    if (!GameSession.isGameOver && playerEntity != null && playerEntity.healthComponent != null && playerEntity.healthComponent.current <= 0)
                    {
                        GameSession.EndGame(false);
                    }

                    // transition to end screen (win via PowerUpComponent)
                    if (GameSession.isGameOver)
                    {
                        gameState = GameState.EndScreen;
                    }
                    break;

                // END SCREEN
                case GameState.EndScreen:
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(new Color(15, 15, 25, 255));
                    DrawEndScreen();
                    Raylib.EndDrawing();

                    if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
                    {
                        CleanupGame();
                        menu.Reset();
                        gameState = GameState.MainMenu;
                    }
                    break;
            }

            frameTimer.Restart();
        }

        // Cleanup
        CleanupGame();

        if (renderTarget.Id != 0) Raylib.UnloadRenderTexture(renderTarget);
        if (depthTexture.Id != 0) Raylib.UnloadTexture(depthTexture);

        Raylib.UnloadShader(spriteShader);
        Raylib.UnloadShader(floorCeilingShader);

        foreach (KeyValuePair<string, Texture2D> texture in textures)
            Raylib.UnloadTexture(texture.Value);

        Raylib.CloseWindow();
    }

    static void FixedUpdate()
    {
        if (shootCooldown > 0f) shootCooldown -= fixedDeltaTime;

        HandleKeyboard();
        HandleMouse();
        HandleEntites();
        PlayerRegeneration();
        LookForPowerUps();
    }

    private static void HandleEntites()
    {
        Stack<Entity> entitiesToRemove = new Stack<Entity>();

        foreach (Entity entity in entities)
        {
            entity.Update();
            if (entity.destroy) entitiesToRemove.Push(entity);
        }

        //Remove dead entites
        while (entitiesToRemove.Count > 0)
        {
            Entity entity = entitiesToRemove.Pop();
            entities.Remove(entity);
            Console.WriteLine($"Removed entity: {entity}");
        }
    }

    private static void HandleKeyboard()
    {
        // Convert movement to be time-based
        float moveStep = moveSpeed * fixedDeltaTime;
        float rotationStep = rotationSpeed * fixedDeltaTime;

        if (InputSystem.ConsumeKey(KeyboardKey.E))
        {
            // Rotate left using matrix multiplication
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(rotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(rotationStep));
        }

        if (InputSystem.ConsumeKey(KeyboardKey.Q))
        {
            // Rotate right using matrix multiplication
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(-rotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(-rotationStep));
        }

        if (InputSystem.ConsumeKey(KeyboardKey.W))
        {
            Vector2 newPos = playerEntity.transform.Position + playerEntity.playerController.Direction * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.tileMap.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.tileMap.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (InputSystem.ConsumeKey(KeyboardKey.S))
        {
            Vector2 newPos = playerEntity.transform.Position - playerEntity.playerController.Direction * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.tileMap.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.tileMap.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (InputSystem.ConsumeKey(KeyboardKey.A))
        {
            Vector2 newPos = playerEntity.transform.Position + new Vector2(playerEntity.playerController.Direction.Y, -playerEntity.playerController.Direction.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.tileMap.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.tileMap.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (InputSystem.ConsumeKey(KeyboardKey.D))
        {
            Vector2 newPos = playerEntity.transform.Position - new Vector2(playerEntity.playerController.Direction.Y, -playerEntity.playerController.Direction.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.tileMap.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.tileMap.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        //Interact (closest door, other interactables)
        if (InputSystem.ConsumeKey(KeyboardKey.F))
        {
            Interact();
            //other interactions
        }
    }

    private static void HandleMouse()
    {
        if (!Raylib.IsWindowFocused())
        {
            InputSystem.ConsumeMouseDelta(); // drain so it doesn't build up
            return;
        }

        Vector2 mouseDelta = InputSystem.ConsumeMouseDelta();
        float mouseRotationStep = mouseRotationSpeed * fixedDeltaTime;
        Raylib.SetMousePosition(screenWidth / 2, screenHeight / 2); // Reset mouse position to center

        if (mouseDelta.X > 0)
        {
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(mouseRotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(mouseRotationStep));
        }

        if (mouseDelta.X < 0)
        {
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(-mouseRotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(-mouseRotationStep));
        }

        if ((InputSystem.ConsumeMouse(MouseButton.Left) || InputSystem.ConsumeKey(KeyboardKey.Space)) && shootCooldown <= 0f)
        {
            float wallDist;
            Entity hitEnemy = CastShootingRay(out wallDist);

            ManaComponent manaComponent = playerEntity.GetComponent<ManaComponent>();

            manaComponent.TakeMana(1);
            shootCooldown = manaComponent.current > 0 ? SHOOT_COOLDOWN_TIME : SHOOT_COOLDOWN_TIME * 4f;

            //instantiate a bullet with lifetime as a health component
            Entity bullet = new Entity();
            bullet.AddComponent(new Transform
            {
                Position = playerEntity.transform.Position
            });
            bullet.AddComponent(new RaySpriteRenderer
            {
                Texture = textures["fireball"],
                spriteScale = 0.1f
            });
            bullet.AddComponent(new BulletHealthComponent(50));
            bullet.AddComponent(new MovementComponent(playerEntity.playerController.Direction, 20f));

            entities.Add(bullet);

            if (hitEnemy != null)
            {
                hitEnemy.healthComponent?.TakeDamage(1);
                Console.WriteLine("Hit enemy!");

                if (hitEnemy.healthComponent.current <= 0 && CountEnemies() == 0) // TODO count enemies is iterative, maybe i should keep a number for enemy count
                {
                    Entity item = new Entity();

                    Transform transform = item.AddComponent<Transform>(new Transform()
                    {
                        Position = hitEnemy.transform.Position
                    });

                    RaySpriteRenderer renderer = item.AddComponent<RaySpriteRenderer>(new RaySpriteRenderer());
                    renderer.Texture = textures["pumpkin"];
                    renderer.spriteScale = 0.2f;

                    PowerUpComponent powerUp = item.AddComponent<PowerUpComponent>(new PowerUpComponent
                    {
                        effectType = 2 // endgame pumpkin item
                    });

                    entities.Add(item);

                    // Create endgame item
                    Room currentRoom = roomGenerator.GetRoomAtPosition(hitEnemy.transform.Position);
                    List<Entity> roomItems = currentRoom.items;
                    if (roomItems == null) roomItems = new List<Entity>(1);
                    roomItems.Add(item);
                }

                // Optional: Play hit sound
                // Raylib.PlaySound(hitSound);
            }
        }
    }

    // Regenerate health over time, TODO -> out of combat regeneration
    private static void PlayerRegeneration()
    {
        if (playerEntity.healthComponent.current < playerEntity.healthComponent.max)
        {
            if (RandomR.Range(0f, 1f) > 0.999f) playerEntity.healthComponent.TakeDamage(-1); // Heal 1 HP
        }

        if (playerEntity.manaComponent.current < playerEntity.manaComponent.max)
        {
            if (RandomR.Range(0f, 1f) > 0.999f) playerEntity.manaComponent.TakeMana(-1); // 1 mana regen
        }
    }

    private static void LookForPowerUps()
    {
        List<Entity> roomItems = roomGenerator.GetRoomAtPosition(playerEntity.transform.Position).items;

        if (roomItems == null || roomItems.Count == 0) return;

        foreach (Entity item in roomItems)
        {
            if (Vector2.Distance(item.transform.Position, playerEntity.transform.Position) < 0.5f)
            {
                item.powerUpComponent.PickUp(playerEntity);
            }
        }
    }
}