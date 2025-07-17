using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;
using static Settings;

class Raycaster
{
    private struct RayHit
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
    static double updateTimeMs = 0;

    // Performance metrics
    static double rayLoopTimeMs = 0;
    static double spriteDrawTimeMs = 0;
    static double zBufferDrawTimeMs = 0;
    static double depthTextureTimeMs = 0;
    static double totalFrameTimeMs = 0;
    static double minFrameTime = double.MaxValue;
    static double maxFrameTime = 0;
    static double avgFrameTime = 0;
    static int frameCount = 0;
    static Stopwatch frameTimer = new Stopwatch();

    const int TEXTURE_SIZE = 64;

    static int[,] MAP;
    static List<Entity> entities = new List<Entity>();

    // Player
    static Vector2 playerPos = new Vector2(1.5f, 1.5f);
    static Vector2 playerDir = new Vector2(1, 0);
    static Vector2 cameraPlane = new Vector2(0, FOV);

    // Colors
    static Color MapWallColor = new Color(100, 100, 100, 255);
    static Color MapDoorColor = new Color(0, 0, 255, 255);
    static Color MapBgColor = new Color(30, 30, 30, 255);
    static Color PlayerColor = new Color(0, 255, 0, 255);
    static Color EnemyColor = new Color(255, 0, 0, 255);

    // Textures
    static Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    static RenderTexture2D renderTarget;

    static Shader spriteShader;
    static Shader floorCeilingShader;

    static int playerPosLoc, playerDirLoc, cameraPlaneLoc, screenWidthLoc, screenHeightLoc;

    //Z depth
    static Texture2D depthTexture;
    static float[] zBuffer = new float[internalScreenWidth];
    static float maxZ = 0f;

    static RoomGenerator roomGenerator;

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
    }

    static void LoadMap()
    {
        roomGenerator = new RoomGenerator(50, 50, 2, 10, 2, 10, 0f);
        roomGenerator.Generate();
        MAP = roomGenerator.intgrid;
        roomGenerator.PrintIntGrid();
    }

    private static void LoadEnemys()
    {
        float spawnChance = 0.25f;

        for (int i = 0; i < roomGenerator.rooms.Count; i++)
        {
            foreach (Vector2IntR pos in roomGenerator.rooms[i].coords)
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

                    if (MAP[pos.y, pos.x] != 0) continue; // Only spawn on empty tiles

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
            roach.AddComponent(new HealthComponent(2 * difficulty));
            roach.AddComponent(new RaySpriteRenderer { Texture = sprite });
            roach.AddComponent(new RoachAI());

            return roach;
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

    static void DrawPerformanceMetrics()
    {
        int startX = 10;
        int startY = 40;
        int lineHeight = 20;

        // Draw background panel
        Raylib.DrawRectangle(startX - 5, startY - 5, 320, 230, new Color(0, 0, 0, 180));
        Raylib.DrawRectangleLines(startX - 5, startY - 5, 320, 230, Color.DarkGray);

        // Draw metrics
        Raylib.DrawText($"Ray Loop: {rayLoopTimeMs:F2} ms", startX, startY, lineHeight, Color.Green);
        Raylib.DrawText($"Sprite draw: {spriteDrawTimeMs:F2} ms", startX, startY + lineHeight, lineHeight, Color.Green);
        Raylib.DrawText($"Z-Buffer draw: {zBufferDrawTimeMs:F2} ms", startX, startY + lineHeight * 2, lineHeight, Color.Green);
        Raylib.DrawText($"Depth Texture: {depthTextureTimeMs:F2} ms", startX, startY + lineHeight * 3, lineHeight, Color.Green);
        Raylib.DrawText($"Update time: {updateTimeMs:F2} ms", startX, startY + lineHeight * 4, lineHeight, Color.Lime);
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
        Raylib.SetShaderValue(floorCeilingShader, playerPosLoc, playerPos, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(floorCeilingShader, playerDirLoc, playerDir, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(floorCeilingShader, cameraPlaneLoc, cameraPlane, ShaderUniformDataType.Vec2);
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
        //TODO Draw only visible entities -> spatial hashing -> map[x,y]

        //TODO Sort only visibles
        // Sort Entites by distance (far to near)
        entities.Sort((a, b) =>
        {
            float distA = Vector2.DistanceSquared(a.transform.Position, playerPos);
            float distB = Vector2.DistanceSquared(b.transform.Position, playerPos);
            return distB.CompareTo(distA);
        });

        Raylib.BeginBlendMode(BlendMode.Alpha);

        //Set Depth texture for Sprite shader
        int depthTexLoc = Raylib.GetShaderLocation(spriteShader, "depthTexture");
        Raylib.SetShaderValueTexture(spriteShader, depthTexLoc, depthTexture);

        foreach (Entity entity in entities)
        {
            if (Vector2.Distance(playerPos, entity.transform.Position) > drawDistance) continue;
            
            RaySpriteRenderer sprite = entity.GetComponent<RaySpriteRenderer>();

            // Transform sprite position relative to camera
            Vector2 spritePos = sprite.Position - playerPos;
            float invDet = 1.0f / (cameraPlane.X * playerDir.Y - playerDir.X * cameraPlane.Y);

            float transformX = invDet * (playerDir.Y * spritePos.X - playerDir.X * spritePos.Y);
            float transformY = invDet * (-cameraPlane.Y * spritePos.X + cameraPlane.X * spritePos.Y);

            if (transformY <= 0) continue;  // Behind camera

            // Calculate sprite screen position and size
            int spriteScreenX = (int)((internalScreenWidth / 2) * (1 + transformX / transformY));
            int spriteHeight = Math.Abs((int)(internalScreenHeight / transformY));
            if (spriteHeight < 2) continue;

            float aspectRatio = sprite.Texture.Width / (float)sprite.Texture.Height;
            int spriteWidth = (int)(spriteHeight * aspectRatio);

            // Calculate drawing coordinates with clamping
            int drawStartX = Math.Clamp(spriteScreenX - spriteWidth / 2, 0, internalScreenWidth);
            int drawEndX = Math.Clamp(spriteScreenX + spriteWidth / 2, 0, internalScreenWidth);
            int drawStartY = Math.Clamp(internalScreenHeight / 2 - spriteHeight / 2, 0, internalScreenHeight);
            int drawEndY = Math.Clamp(internalScreenHeight / 2 + spriteHeight / 2, 0, internalScreenHeight);

            // Set shader uniforms          
            int depthLoc = Raylib.GetShaderLocation(spriteShader, "spriteDepth");
            Raylib.SetShaderValue(spriteShader, depthLoc, transformY, ShaderUniformDataType.Float);

            //Sprite lighting
            float lightingFactor = Math.Clamp(1.0f - transformY * 0.03f, 0.3f, 1.0f);
            int lightingColorLoc = Raylib.GetShaderLocation(spriteShader, "lightingFactor");
            Raylib.SetShaderValue(spriteShader, lightingColorLoc, lightingFactor, ShaderUniformDataType.Float);

            // Calculate texture coordinates with proper clipping
            float texOffsetX = (float)(drawStartX - (spriteScreenX - spriteWidth / 2)) / spriteWidth;
            float texWidth = (float)(drawEndX - drawStartX) / spriteWidth;

            float texOffsetY = (float)(drawStartY - (internalScreenHeight / 2 - spriteHeight / 2)) / spriteHeight;
            float texHeight = (float)(drawEndY - drawStartY) / spriteHeight;

            Rectangle srcRect = new Rectangle(
                texOffsetX * sprite.Texture.Width,
                texOffsetY * sprite.Texture.Height,
                texWidth * sprite.Texture.Width,
                texHeight * sprite.Texture.Height
            );

            Rectangle destRect = new Rectangle(
                drawStartX,
                drawStartY,
                drawEndX - drawStartX,
                drawEndY - drawStartY
            );

            // Apply distance shading
            float shade = Math.Clamp(1.0f - transformY * 0.03f, 0.3f, 1.0f);
            Color tint = new Color(
                ((byte)255 * shade),
                ((byte)255 * shade),
                ((byte)255 * shade),
                 (byte)255
            );

            // Draw with shader
            Raylib.BeginShaderMode(spriteShader);
            Raylib.DrawTexturePro(
                sprite.Texture,
                srcRect,
                destRect,
                Vector2.Zero,
                0f,
                tint
            );
            Raylib.EndShaderMode();
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
        int MAP_SIZE = MAP.GetLength(0);
        Vector2 MAP_POS = new Vector2(screenWidth - MAP_SIZE * MAP_SCALE - 10, 10);

        // Draw map background
        Raylib.DrawRectangle(
            (int)MAP_POS.X,
            (int)MAP_POS.Y,
            MAP_SIZE * MAP_SCALE,
            MAP_SIZE * MAP_SCALE,
            MapBgColor
        );

        // Draw map tiles
        for (int y = 0; y < MAP_SIZE; y++)
        {
            for (int x = 0; x < MAP_SIZE; x++)
            {
                if (MAP[y, x] > 0)
                {
                    Raylib.DrawRectangle(
                        (int)MAP_POS.X + x * MAP_SCALE,
                        (int)MAP_POS.Y + y * MAP_SCALE,
                        MAP_SCALE,
                        MAP_SCALE,
                        MAP[y, x] == 1 ? MapWallColor : MapDoorColor
                    );
                }
            }
        }

        // Draw player
        int playerMapX = (int)(MAP_POS.X + playerPos.X * MAP_SCALE);
        int playerMapY = (int)(MAP_POS.Y + playerPos.Y * MAP_SCALE);
        Raylib.DrawCircle(playerMapX, playerMapY, 3, PlayerColor);

        // Draw direction line
        Vector2 endPos = new Vector2(
            playerMapX + playerDir.X * 15,
            playerMapY + playerDir.Y * 15
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

    private static RayHit CastDDA(Vector2 rayDir, Vector2 position, int[,] map) => CastDDA(rayDir.X, rayDir.Y, position.X, position.Y, map);
    private static RayHit CastDDA(float rayDirX, float rayDirY, float posX, float posY, int[,] map)
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
            if (map[mapY, mapX] > 0) hit = true;
        }

        if (hit)
        {
            // Calculate distance
            float perpWallDist = side == 0 ?
                (mapX - playerPos.X + (1 - stepX) * 0.5f) / rayDir.X :
                (mapY - playerPos.Y + (1 - stepY) * 0.5f) / rayDir.Y;

            perpWallDist = MathF.Abs(perpWallDist);
            perpWallDist = MathF.Max(perpWallDist, 0.01f);

            return new RayHit(mapY, mapX, perpWallDist, side);
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

        Stopwatch rayTimer = Stopwatch.StartNew();

        for (int x = 0; x < internalScreenWidth; x++)
        {
            // Calculate ray direction with internal width
            float cameraX = 2 * x / (float)internalScreenWidth - 1;
            Vector2 rayDir = new Vector2(
                playerDir.X + cameraPlane.X * cameraX,
                playerDir.Y + cameraPlane.Y * cameraX
            );

            // DDA setup
            int mapX = (int)playerPos.X;
            int mapY = (int)playerPos.Y;

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
                (playerPos.X - mapX) * deltaDist.X :
                (mapX + 1.0f - playerPos.X) * deltaDist.X;

            sideDist.Y = rayDir.Y < 0 ?
                (playerPos.Y - mapY) * deltaDist.Y :
                (mapY + 1.0f - playerPos.Y) * deltaDist.Y;

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
                if (mapX < 0 || mapX >= MAP.GetLength(0) || mapY < 0 || mapY >= MAP.GetLength(1)) break;
                if (MAP[mapY, mapX] > 0) hit = true;
            }

            if (hit)
            {
                // Calculate distance
                float perpWallDist = side == 0 ?
                    (mapX - playerPos.X + (1 - stepX) * 0.5f) / rayDir.X :
                    (mapY - playerPos.Y + (1 - stepY) * 0.5f) / rayDir.Y;

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
                    playerPos.Y + perpWallDist * rayDir.Y :
                    playerPos.X + perpWallDist * rayDir.X;
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
                    MAP[mapY, mapX] == 1 ? textures["wall"] : textures["door"],
                    srcRect,
                    destRect,
                    Vector2.Zero,
                    0f,
                    tint
                );
            }
        }

        rayTimer.Stop();
        rayLoopTimeMs = rayTimer.Elapsed.TotalMilliseconds;

        // Update depth texture with z-buffer
        Stopwatch depthTimer = Stopwatch.StartNew();
        UpdateDepthTexture();
        depthTimer.Stop();
        depthTextureTimeMs = depthTimer.Elapsed.TotalMilliseconds;

        Stopwatch spriteTimer = Stopwatch.StartNew();
        DrawEntitys();
        spriteTimer.Stop();
        spriteDrawTimeMs = spriteTimer.Elapsed.TotalMilliseconds;

        Stopwatch zBufferTimer = Stopwatch.StartNew();
        DrawZBuffer();
        zBufferTimer.Stop();
        zBufferDrawTimeMs = zBufferTimer.Elapsed.TotalMilliseconds;

        Raylib.EndTextureMode();
    }

    private static Entity CastShootingRay(out float hitDistance)
    {
        hitDistance = CastDDA(playerDir, playerPos, MAP).distance; // Distance to wall hit

        Entity hitEnemy = null;
        float closestT = float.MaxValue;
        const float spriteRadius = 0.4f; // Sprite collision radius

        foreach (Entity entity in entities)
        {
            Vector2 toSprite = entity.transform.Position - playerPos;
            float t = Vector2.Dot(toSprite, playerDir);

            // Skip if behind player or too close
            if (t < 0.1f) continue;

            Vector2 closestPoint = playerPos + t * playerDir;
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

    private static void ToggleDoor()
    {
        RayHit hit = CastDDA(playerDir, playerPos, MAP);

        if (MAP[hit.mapX, hit.mapY] == 2 && hit.distance <= 1f)
        {
            // Toggle door state
            Vector2IntR doorPos = new Vector2IntR(hit.mapX, hit.mapY);
            roomGenerator.doorStates[doorPos] = !roomGenerator.doorStates[doorPos];

            // Update map for pathfinding
            MAP[doorPos.x, doorPos.y] = roomGenerator.doorStates[doorPos] ? 0 : 2; // 0 = walkable

            Console.WriteLine($"Toggled door at ({doorPos.x},{doorPos.y}) - Now {(roomGenerator.doorStates[doorPos] ? "OPEN" : "CLOSED")}");
            return;
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
                Stopwatch updateTimer = Stopwatch.StartNew();
                FixedUpdate();
                updateTimer.Stop();
                updateTimeMs += updateTimer.Elapsed.TotalMilliseconds;

                accumulator -= fixedDeltaTime;
                upsCount++;
            }

            // Update UPS counter every second
            upsTimer += frameTime;
            if (upsTimer >= 1.0)
            {
                ups = upsCount;
                upsCount = 0;
                updateTimeMs /= ups; // Average update time
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
    }

    private static void HandleEntites()
    {
        Stack<Entity> entitiesToRemove = new Stack<Entity>();

        foreach (Entity entity in entities)
        {
            entity.GetComponent<RoachAI>().targetPosition = playerPos;

            entity.Update();

            //Remove dead entites
            if (entity.GetComponent<HealthComponent>().CurrentHP <= 0)
            {
                entitiesToRemove.Push(entity);
            }
        }

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
            playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(rotationStep));
            cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(rotationStep));
        }

        if (Raylib.IsKeyDown(KeyboardKey.A))
        {
            // Rotate right using matrix multiplication
            playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(-rotationStep));
            cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(-rotationStep));
        }

        if (Raylib.IsKeyDown(KeyboardKey.W))
        {
            Vector2 newPos = playerPos + playerDir * moveStep;
            if (newPos.X >= 0 && newPos.X < MAP.GetLength(0) && newPos.Y >= 0 && newPos.Y < MAP.GetLength(1) &&
                MAP[(int)newPos.Y, (int)newPos.X] == 0)
            {
                playerPos = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.S))
        {
            Vector2 newPos = playerPos - playerDir * moveStep;
            if (newPos.X >= 0 && newPos.X < MAP.GetLength(0) && newPos.Y >= 0 && newPos.Y < MAP.GetLength(1) &&
                MAP[(int)newPos.Y, (int)newPos.X] == 0)
            {
                playerPos = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.Q))
        {
            Vector2 newPos = playerPos + new Vector2(playerDir.Y, -playerDir.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < MAP.GetLength(0) && newPos.Y >= 0 && newPos.Y < MAP.GetLength(1) &&
                MAP[(int)newPos.Y, (int)newPos.X] == 0)
            {
                playerPos = newPos;
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.E))
        {
            Vector2 newPos = playerPos - new Vector2(playerDir.Y, -playerDir.X) * moveStep;
            if (newPos.X >= 0 && newPos.X < MAP.GetLength(0) && newPos.Y >= 0 && newPos.Y < MAP.GetLength(1) &&
                MAP[(int)newPos.Y, (int)newPos.X] == 0)
            {
                playerPos = newPos;
            }
        }

        //Interact (closest door, other interactables)
        if (Raylib.IsKeyDown(KeyboardKey.F))
        {
            ToggleDoor();
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
            playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(mouseRotationStep));
            cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(mouseRotationStep));
        }

        if (mouseDelta.X < 0)
        {
            playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(-mouseRotationStep));
            cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(-mouseRotationStep));
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            float wallDist;
            Entity hitEnemy = CastShootingRay(out wallDist);

            if (hitEnemy != null)
            {
                hitEnemy.GetComponent<HealthComponent>().TakeDamage(1);
                Console.WriteLine("Hit enemy!");

                // Optional: Play hit sound
                // Raylib.PlaySound(hitSound);
            }
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