using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;
using static Settings;
using static RoomGenerator;

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

    //Z depth
    static Texture2D depthTexture;
    static float[] zBuffer = new float[internalScreenWidth];
    static float maxZ = 0f;

    static RoomGenerator roomGenerator;

    static float interactionDistance = 1.0f;

    static void LoadTextures()
    {
        renderTarget = Raylib.LoadRenderTexture(internalScreenWidth, internalScreenHeight);
        depthTexture = CreateDepthTexture();

        textures.Add("checkerBoard", LoadTexture("Assets/CheckerBoard.png"));
        textures.Add("crosshairs", LoadTexture("Assets/crosshairs_128.png"));

        textures.Add("wall", LoadTexture("Assets/wall.png"));
        textures.Add("door", LoadTexture("Assets/door.png"));
        textures.Add("ceiling", LoadTexture("Assets/ceiling.png", true));
        textures.Add("floor", LoadTexture("Assets/floor.png", true));

        textures.Add("roach_red", LoadTexture("Assets/roach_red.png"));
        textures.Add("roach_blue", LoadTexture("Assets/roach_blue.png"));
        textures.Add("roach_green", LoadTexture("Assets/roach_green.png"));

        textures.Add("electro_bullet", LoadTexture("Assets/electro_bullet.png"));

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

        depthTexLoc = Raylib.GetShaderLocation(spriteShader, "depthTexture");
    }

    static void LoadMap()
    {
        roomGenerator = new RoomGenerator(50, 50, 2, 10, 2, 10, 0f);
        roomGenerator.Generate();

        //Debug the starting room
        roomGenerator.PrintRoom(0);
        foreach (Room neighbour in roomGenerator.rooms[0].neighbourRooms) roomGenerator.PrintRoom(neighbour);

        pathfindingSystem = new HPAStar(roomGenerator);
    }

    private static void LoadEnemys()
    {
        float spawnChance = 0.25f;
        Room playerRoom = roomGenerator.GetRoomAtPosition(playerEntity.transform.Position);

        foreach (Room room in roomGenerator.rooms)
        {
            if (room == playerRoom) continue; // Skip the player's room

            foreach (Vector2IntR pos in room.coords)
            {
                if (RandomR.value < spawnChance)
                {
                    int randomRoachTextureIndex = RandomR.Range(0, 3);

                    Texture2D roachTexture = randomRoachTextureIndex switch
                    {
                        0 => textures["roach_green"],
                        1 => textures["roach_blue"],
                        2 => textures["roach_red"],
                        _ => textures["roach_red"]
                    };

                    if (roomGenerator.objectGrid[pos.x, pos.y] is not Floor) continue; // Only spawn on empty tiles

                    Vector2 startPosition = new Vector2(pos.x + 0.5f, pos.y + 0.5f);
                    entities.Add(CreateRoach(roachTexture, startPosition, randomRoachTextureIndex));
                }
            }
        }

        Console.WriteLine($"Loaded {entities.Count} roaches");

        Entity CreateRoach(Texture2D sprite, Vector2 startPosition, int difficulty)
        {
            Entity roach = new Entity();

            roach.AddComponent(new Transform { Position = startPosition });
            roach.AddComponent(new HealthComponent(difficulty + 1));
            roach.AddComponent(new RaySpriteRenderer { Texture = sprite });
            roach.AddComponent(new RoachAI());
            roach.GetComponent<RoachAI>().Initialize(pathfindingSystem, roomGenerator);

            return roach;
        }
    }

    static void CreatePlayer()
    {
        playerEntity = new Entity();
        playerEntity.AddComponent(new Transform
        {
            Position = new Vector2(1.5f, 1.5f)
        });

        playerEntity.AddComponent(new PlayerController
        {
            MoveSpeed = 5.0f,
            RotationSpeed = 3.0f,
            MouseRotationSpeed = 0.1f
        });

        playerEntity.AddComponent(new HealthComponent(10));
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
        float healthPercent = (float)playerEntity.healthComponent.CurrentHP / playerEntity.healthComponent.MaxHP;
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
        string healthText = $"HP: {playerEntity.healthComponent.CurrentHP}/{playerEntity.healthComponent.MaxHP}";
        int textX = posX + 5;  // Left-aligned
        int textY = posY + (barHeight - 20) / 2;

        Raylib.DrawText(healthText, textX + 1, textY + 1, 20, Color.Black);
        Raylib.DrawText(healthText, textX, textY, 20, Color.White);
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

        // Set textures
        Raylib.SetShaderValueTexture(floorCeilingShader, Raylib.GetShaderLocation(floorCeilingShader, "ceilingTexture"), textures["ceiling"]);
        Raylib.SetShaderValueTexture(floorCeilingShader, Raylib.GetShaderLocation(floorCeilingShader, "floorTexture"), textures["floor"]);

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
        int MAP_SIZE = roomGenerator.objectGrid.GetLength(0);
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
                    roomGenerator.objectGrid[x, y].minimapColor
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

    public static RayHit CastDDA(Vector2 rayDir, Vector2 position, GridObject[,] map) => CastDDA(rayDir.X, rayDir.Y, position.X, position.Y, map);
    public static RayHit CastDDA(float rayDirX, float rayDirY, float posX, float posY, GridObject[,] map)
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
                    if (mapX < 0 || mapX >= roomGenerator.objectGrid.GetLength(0) || mapY < 0 || mapY >= roomGenerator.objectGrid.GetLength(1)) break;
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
                    float shade = Math.Clamp(1.0f - perpWallDist * 0.03f, 0.3f, 1.0f);
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

                    Raylib.DrawTexturePro(
                        textures[roomGenerator.objectGrid[mapX, mapY].textureName],
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
        hitDistance = CastDDA(playerEntity.playerController.Direction, playerEntity.transform.Position, roomGenerator.objectGrid).distance; // Distance to wall hit

        Entity hitEnemy = null;
        float closestT = float.MaxValue;
        const float spriteRadius = 0.4f; // Sprite collision radius

        foreach (Entity entity in entities)
        {
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
        RayHit hit = CastDDA(playerEntity.playerController.Direction, playerEntity.transform.Position, roomGenerator.objectGrid);

        if (!hit.IsHit() || hit.distance > interactionDistance) return;

        if (roomGenerator.objectGrid[hit.mapX, hit.mapY] is Door door)
        {
            door.isOpen = !door.isOpen;
            Console.WriteLine($"Interacting with Door {door.GetHashCode()} -> " + (door.isOpen ? "opened" : "closed"));
        }
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
        LoadMap();
        CreatePlayer();
        LoadEnemys();

        while (!Raylib.WindowShouldClose())
        {
            // Calculate frame time
            currentTime = gameTimer.Elapsed.TotalSeconds;
            double frameTime = currentTime - lastTime;
            lastTime = currentTime;

            // Convert to milliseconds for metrics
            totalFrameTimeMs = frameTime * 1000.0;

            // Update performance stats
            if (totalFrameTimeMs < minFrameTime) minFrameTime = totalFrameTimeMs;
            if (totalFrameTimeMs > maxFrameTime) maxFrameTime = totalFrameTimeMs;

            frameCount++;
            if (frameCount >= 60)
            {
                avgFrameTime = (avgFrameTime * 0.9) + (totalFrameTimeMs * 0.1);
                frameCount = 0;
            }

            // Accumulate time for UPS
            accumulator += (float)frameTime;
            double updateStartTime = gameTimer.Elapsed.TotalSeconds;

            // Process input outside fixed update for responsiveness
            if (ProcessInput()) break;

            // Fixed timestep updates for game logic
            while (accumulator >= fixedDeltaTime)
            {
                using (PerformanceMonitor.Measure(t => PerformanceMonitor.fixedUpdateTime = t))
                {
                    FixedUpdate();
                }

                accumulator -= fixedDeltaTime;
                upsCount++;
            }

            // Update UPS counter every second
            upsTimer += frameTime;
            if (upsTimer >= 1.0)
            {
                ups = upsCount;
                upsCount = 0;
                upsTimer -= 1.0;
            }

            // Only render if we're not running behind on updates
            if (accumulator < fixedDeltaTime * 3)
            {
                // Update the 3D view - this is rendering, not logic
                CastRays();

                // Drawing
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                // Draw upscaled texture to screen
                Rectangle source = new Rectangle(0, 0, internalScreenWidth, -internalScreenHeight);
                Rectangle dest = new Rectangle(0, 0, screenWidth, screenHeight);
                Raylib.DrawTexturePro(
                    renderTarget.Texture,
                    source,
                    dest,
                    Vector2.Zero,
                    0f,
                    Color.White
                );

                // Draw minimap and performance metrics
                DrawPerformanceMetrics();
                DrawMinimap();
                Raylib.DrawFPS(10, 10);
                DrawPlayerHealth();
                DrawMouse();

                Raylib.EndDrawing();
            }

            frameTimer.Restart();
        }

        // Cleanup
        Raylib.UnloadRenderTexture(renderTarget);
        Raylib.UnloadTexture(depthTexture);
        Raylib.UnloadShader(spriteShader);
        Raylib.UnloadShader(floorCeilingShader);

        foreach (KeyValuePair<string, Texture2D> texture in textures)
        {
            Raylib.UnloadTexture(texture.Value);
        }

        Raylib.CloseWindow();
    }

    static void FixedUpdate()
    {
        HandleKeyboard();
        HandleMouse();
        HandleEntites();
        PlayerRegeneration();
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

        if (Raylib.IsKeyDown(KeyboardKey.D))
        {
            // Rotate left using matrix multiplication
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(rotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(rotationStep));
        }

        if (Raylib.IsKeyDown(KeyboardKey.A))
        {
            // Rotate right using matrix multiplication
            playerEntity.playerController.Direction = Vector2.Transform(playerEntity.playerController.Direction, Matrix3x2.CreateRotation(-rotationStep));
            playerEntity.playerController.CameraPlane = Vector2.Transform(playerEntity.playerController.CameraPlane, Matrix3x2.CreateRotation(-rotationStep));
        }

        if (Raylib.IsKeyDown(KeyboardKey.W))
        {
            Vector2 newPos = playerEntity.transform.Position + playerEntity.playerController.Direction * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.objectGrid.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.objectGrid.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.S))
        {
            Vector2 newPos = playerEntity.transform.Position - playerEntity.playerController.Direction * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.objectGrid.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.objectGrid.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.Q))
        {
            Vector2 newPos = playerEntity.transform.Position + new Vector2(playerEntity.playerController.Direction.Y, -playerEntity.playerController.Direction.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.objectGrid.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.objectGrid.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.E))
        {
            Vector2 newPos = playerEntity.transform.Position - new Vector2(playerEntity.playerController.Direction.Y, -playerEntity.playerController.Direction.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < roomGenerator.objectGrid.GetLength(0) && newPos.Y >= 0 && newPos.Y < roomGenerator.objectGrid.GetLength(1) &&
                roomGenerator.IsWalkable(newPos))
            {
                playerEntity.transform.Position = newPos;
            }
        }

        //Interact (closest door, other interactables)
        if (Raylib.IsKeyDown(KeyboardKey.F))
        {
            Interact();
            //other interactions
        }
    }

    private static void HandleMouse()
    {
        Vector2 mouseDelta = Raylib.GetMouseDelta();
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

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            float wallDist;
            Entity hitEnemy = CastShootingRay(out wallDist);

            //instantiate a bullet with lifetime as a health component
            Entity bullet = new Entity();
            bullet.AddComponent(new Transform
            {
                Position = playerEntity.transform.Position
            });
            bullet.AddComponent(new RaySpriteRenderer
            {
                Texture = textures["electro_bullet"],
            });
            bullet.AddComponent(new BulletHealthComponent(10));
            bullet.AddComponent(new MovementComponent(playerEntity.playerController.Direction, 10f));

            entities.Add(bullet);

            if (hitEnemy != null)
            {
                hitEnemy.healthComponent?.TakeDamage(1);
                Console.WriteLine("Hit enemy!");

                // Optional: Play hit sound
                // Raylib.PlaySound(hitSound);
            }
        }
    }

    // Regenerate health over time, TODO -> out of combat regeneration
    private static void PlayerRegeneration()
    {
        if (playerEntity.healthComponent.CurrentHP < playerEntity.healthComponent.MaxHP)
        {
            if (RandomR.Range(0f, 1f) > 0.999f) playerEntity.healthComponent.TakeDamage(-1); // Heal 1 HP
        }
    }

    private static bool ProcessInput()
    {
        // Exit the game
        if (Raylib.IsKeyDown(KeyboardKey.Escape))
        {
            Raylib.CloseWindow();
            return true;
        }

        return false;
    }
}