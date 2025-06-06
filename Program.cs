using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

class Raycaster
{
    class Sprite
    {
        public Vector2 Position;
        public Texture2D Texture;
        public Color[] TextureData;

        public Sprite(Vector2 position, Texture2D texture)
        {
            Position = position;
            Texture = texture;
            TextureData = new Color[texture.Width * texture.Height];

            Image textureImage = Raylib.LoadImageFromTexture(texture);

            for (int y = 0; y < texture.Height; y++)
            {
                for (int x = 0; x < texture.Width; x++)
                {
                    TextureData[y * texture.Width + x] = Raylib.GetImageColor(textureImage, x, y);
                }
            }

            Raylib.UnloadImage(textureImage);
        }
    }

    static List<Sprite> sprites = new();
    const int WIDTH = 800;
    const int HEIGHT = 600;
    const int INTERNAL_WIDTH = 200;
    const int INTERNAL_HEIGHT = 150;
    const int TEXTURE_SIZE = 64;

    // Mini-map settings
    const int MAP_SCALE = 10;
    const int MAP_SIZE = 16;
    static readonly Vector2 MAP_POS = new Vector2(WIDTH - MAP_SIZE * MAP_SCALE - 10, 10);

    // 16x16 map
    static readonly int[,] MAP = {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,1,0,1,0,0,1,0,1,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1},
        {1,0,0,1,0,0,0,0,0,0,0,0,1,0,0,1},
        {1,0,0,0,0,0,1,0,0,1,0,0,0,0,0,1},
        {1,0,1,0,1,0,0,0,0,0,0,1,0,1,0,1},
        {1,0,1,0,1,0,0,0,0,0,0,1,0,1,0,1},
        {1,0,0,0,0,0,1,0,0,1,0,0,0,0,0,1},
        {1,0,0,1,0,0,0,0,0,0,0,0,1,0,0,1},
        {1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,1,0,1,0,0,1,0,1,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };

    // Player
    static Vector2 playerPos = new Vector2(1.5f, 1.5f);
    static Vector2 playerDir = new Vector2(1, 0);
    static Vector2 cameraPlane = new Vector2(0, 0.66f);
    const float MOVE_SPEED = 0.05f;
    const float ROT_SPEED = 0.1f;

    // Colors
    static Color SkyColor = new Color(100, 100, 255, 255);
    static Color GroundColor = new Color(50, 50, 50, 255);
    static Color MapWallColor = new Color(100, 100, 100, 255);
    static Color MapBgColor = new Color(30, 30, 30, 255);
    static Color PlayerColor = new Color(0, 255, 0, 255);
    static Color EnemyColor = new Color(255, 0, 0, 255);

    // Textures
    static Color[] textureData;
    static RenderTexture2D renderTarget;

    static float[] zBuffer = new float[INTERNAL_WIDTH];
    static float maxZ = 0f;

    static void LoadTextures()
    {
        // Generate texture data directly
        textureData = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

        for (int y = 0; y < TEXTURE_SIZE; y++)
        {
            for (int x = 0; x < TEXTURE_SIZE; x++)
            {
                bool isDark = (x / 8 + y / 8) % 2 == 0;
                textureData[y * TEXTURE_SIZE + x] = isDark ?
                    new Color(180, 0, 0, 255) :
                    new Color(220, 0, 0, 255);
            }
        }

        renderTarget = Raylib.LoadRenderTexture(INTERNAL_WIDTH, INTERNAL_HEIGHT);

        Texture2D enemyTex = Raylib.LoadTexture("Assets/enemy.png");
        Raylib.SetTextureFilter(enemyTex, TextureFilter.Bilinear);

        sprites.Add(new Sprite(new Vector2(5.5f, 5.5f), enemyTex));
    }

    static void DrawSprites()
    {
        // Sort sprites by distance from player (far to near)
        sprites.Sort((a, b) =>
            (b.Position - playerPos).LengthSquared().CompareTo((a.Position - playerPos).LengthSquared())
        );

        foreach (var sprite in sprites)
        {
            // Relative position to camera
            Vector2 spriteRel = sprite.Position - playerPos;

            // Inverse camera transform
            float invDet = 1.0f / (cameraPlane.X * playerDir.Y - playerDir.X * cameraPlane.Y);
            float transformX = invDet * (playerDir.Y * spriteRel.X - playerDir.X * spriteRel.Y);
            float transformY = invDet * (-cameraPlane.Y * spriteRel.X + cameraPlane.X * spriteRel.Y);

            // Projected screen position
            if (transformY <= 0) continue;

            int spriteScreenX = (int)((INTERNAL_WIDTH / 2) * (1 + transformX / transformY));
            int spriteHeight = Math.Abs((int)(INTERNAL_HEIGHT / transformY));
            if (spriteHeight < 2) continue;

            int spriteWidth = spriteHeight;

            // Calculate draw boundaries
            int drawStartX = Math.Clamp(spriteScreenX - spriteWidth / 2, 0, INTERNAL_WIDTH);
            int drawEndX = Math.Clamp(spriteScreenX + spriteWidth / 2, 0, INTERNAL_WIDTH);

            // Draw column by column
            for (int stripe = drawStartX; stripe < drawEndX; stripe++)
            {
                // Skip if behind wall
                if (transformY > zBuffer[stripe]) continue;

                // Calculate texture X coordinate
                int texX = (int)((stripe - (spriteScreenX - spriteWidth / 2)) *
                                sprite.Texture.Width / spriteWidth);

                // Vertical line parameters
                int drawStartY = Math.Clamp(-spriteHeight / 2 + INTERNAL_HEIGHT / 2, 0, INTERNAL_HEIGHT);
                int drawEndY = Math.Clamp(spriteHeight / 2 + INTERNAL_HEIGHT / 2, 0, INTERNAL_HEIGHT);

                // Calculate texture stepping
                float stepY = (float)sprite.Texture.Height / spriteHeight;
                float texYPos = (drawStartY - INTERNAL_HEIGHT / 2f + spriteHeight / 2f) * stepY;

                // Draw vertical stripe
                for (int y = drawStartY; y < drawEndY; y++)
                {
                    int texY = (int)texYPos;
                    if (texY >= sprite.Texture.Height) texY = sprite.Texture.Height - 1;

                    Color color = sprite.TextureData[texY * sprite.Texture.Width + texX];

                    // Skip transparent pixels
                    if (color.A == 0)
                    {
                        texYPos += stepY;
                        continue;
                    }

                    // Apply distance shading
                    float shade = Math.Clamp(1.0f - transformY * 0.03f, 0.3f, 1.0f);
                    color = new Color(
                        (byte)(color.R * shade),
                        (byte)(color.G * shade),
                        (byte)(color.B * shade),
                        color.A
                    );

                    Raylib.DrawPixel(stripe, y, color);
                    texYPos += stepY;
                }
            }
        }
    }

    static void DrawZBuffer()
    {
        if (maxZ <= 0) return;

        // Draw depth buffer as a gradient at the bottom
        int debugHeight = INTERNAL_HEIGHT / 8; // Use 12.5% of screen height
        for (int y = 0; y < debugHeight; y++)
        {
            for (int x = 0; x < INTERNAL_WIDTH; x++)
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

                Raylib.DrawPixel(x, INTERNAL_HEIGHT - debugHeight + y, color);
            }
        }

        // Draw depth scale markers
        for (int i = 1; i <= 10; i++)
        {
            float depth = zBuffer[INTERNAL_WIDTH / 10 * i - 1];
            string text = $"{depth:F1}";
            int yPos = INTERNAL_HEIGHT - debugHeight / 2;
            Raylib.DrawText(text, i * INTERNAL_WIDTH / 10, yPos, 6, Color.Red);
        }
    }

    static void DrawMinimap()
    {
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
                if (MAP[y, x] == 1)
                {
                    Raylib.DrawRectangle(
                        (int)MAP_POS.X + x * MAP_SCALE,
                        (int)MAP_POS.Y + y * MAP_SCALE,
                        MAP_SCALE,
                        MAP_SCALE,
                        MapWallColor
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

        // Draw enemys
        foreach (Sprite sprite in sprites)
        {
            int posX = (int)(MAP_POS.X + sprite.Position.X * MAP_SCALE);
            int posY = (int)(MAP_POS.Y + sprite.Position.Y * MAP_SCALE);
            Raylib.DrawCircle(posX, posY, 3, EnemyColor);
        }
    }

    static void CastRays()
    {
        Raylib.BeginTextureMode(renderTarget);
        Raylib.ClearBackground(SkyColor);
        Raylib.DrawRectangle(0, INTERNAL_HEIGHT / 2, INTERNAL_WIDTH, INTERNAL_HEIGHT / 2, GroundColor);

        // Reset z-buffer
        Array.Fill(zBuffer, float.MaxValue);
        maxZ = 0f;

        for (int x = 0; x < INTERNAL_WIDTH; x++)
        {
            // Calculate ray direction with internal width
            float cameraX = 2 * x / (float)INTERNAL_WIDTH - 1;
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
                if (mapX < 0 || mapX >= MAP_SIZE || mapY < 0 || mapY >= MAP_SIZE) break;
                if (MAP[mapY, mapX] == 1) hit = true;
            }

            if (hit)
            {
                // Calculate distance
                float perpWallDist = side == 0 ?
                    (mapX - playerPos.X + (1 - stepX) * 0.5f) / rayDir.X :
                    (mapY - playerPos.Y + (1 - stepY) * 0.5f) / rayDir.Y;
                perpWallDist = MathF.Abs(perpWallDist);

                //ZBuffer
                zBuffer[x] = perpWallDist;
                if (zBuffer[x] > maxZ && zBuffer[x] < float.MaxValue) maxZ = zBuffer[x];

                // Calculate wall height
                int lineHeight = (int)(INTERNAL_HEIGHT / perpWallDist);
                int drawStart = Math.Clamp(-lineHeight / 2 + INTERNAL_HEIGHT / 2, 0, INTERNAL_HEIGHT);
                int drawEnd = Math.Clamp(lineHeight / 2 + INTERNAL_HEIGHT / 2, 0, INTERNAL_HEIGHT);

                // Calculate texture x-coordinate
                float wallX = side == 0 ?
                    playerPos.Y + perpWallDist * rayDir.Y :
                    playerPos.X + perpWallDist * rayDir.X;
                wallX -= MathF.Floor(wallX);

                int texX = (int)(wallX * TEXTURE_SIZE);
                if ((side == 0 && rayDir.X > 0) || (side == 1 && rayDir.Y < 0))
                    texX = TEXTURE_SIZE - texX - 1;

                // Calculate texture stepping
                float step = 1.0f * TEXTURE_SIZE / lineHeight;
                float texPosY = (drawStart - INTERNAL_HEIGHT / 2f + lineHeight / 2f) * step;

                // Shading factors
                float shade = Math.Clamp(1.0f - perpWallDist * 0.03f, 0.3f, 1.0f);
                float darken = side == 1 ? 0.6f : 1.0f;

                // Draw vertical texture slice
                for (int y = drawStart; y < drawEnd; y++)
                {
                    int texY = (int)texPosY & (TEXTURE_SIZE - 1);
                    Color color = textureData[texY * TEXTURE_SIZE + texX];

                    // Apply shading
                    Color shadedColor = new Color(
                        (byte)(color.R * shade * darken),
                        (byte)(color.G * shade * darken),
                        (byte)(color.B * shade * darken),
                        color.A
                    );

                    Raylib.DrawPixel(x, y, shadedColor);
                    texPosY += step;
                }
            }
        }

        DrawSprites();
        DrawZBuffer();

        Raylib.EndTextureMode();
    }

    static void Main()
    {
        Raylib.InitWindow(WIDTH, HEIGHT, "Optimized Raycaster");
        Raylib.SetTargetFPS(60);

        LoadTextures();

        while (!Raylib.WindowShouldClose())
        {
            // Handle input
            if (Raylib.IsKeyDown(KeyboardKey.D))
            {
                // Rotate left using matrix multiplication
                playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(ROT_SPEED));
                cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(ROT_SPEED));
            }

            if (Raylib.IsKeyDown(KeyboardKey.A))
            {
                // Rotate right using matrix multiplication
                playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(-ROT_SPEED));
                cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(-ROT_SPEED));
            }

            if (Raylib.IsKeyDown(KeyboardKey.W))
            {
                Vector2 newPos = playerPos + playerDir * MOVE_SPEED;
                if (newPos.X >= 0 && newPos.X < MAP_SIZE && newPos.Y >= 0 && newPos.Y < MAP_SIZE &&
                    MAP[(int)newPos.Y, (int)newPos.X] == 0)
                {
                    playerPos = newPos;
                }
            }

            if (Raylib.IsKeyDown(KeyboardKey.S))
            {
                Vector2 newPos = playerPos - playerDir * MOVE_SPEED;
                if (newPos.X >= 0 && newPos.X < MAP_SIZE && newPos.Y >= 0 && newPos.Y < MAP_SIZE &&
                    MAP[(int)newPos.Y, (int)newPos.X] == 0)
                {
                    playerPos = newPos;
                }
            }

            if (Raylib.IsKeyDown(KeyboardKey.Q))
            {
                Vector2 newPos = playerPos + new Vector2(playerDir.Y, -playerDir.X) * MOVE_SPEED;
                if (newPos.X >= 0 && newPos.X < MAP_SIZE && newPos.Y >= 0 && newPos.Y < MAP_SIZE &&
                    MAP[(int)newPos.Y, (int)newPos.X] == 0)
                {
                    playerPos = newPos;
                }
            }

            if (Raylib.IsKeyDown(KeyboardKey.E))
            {
                Vector2 newPos = playerPos - new Vector2(playerDir.Y, -playerDir.X) * MOVE_SPEED;
                if (newPos.X >= 0 && newPos.X < MAP_SIZE && newPos.Y >= 0 && newPos.Y < MAP_SIZE &&
                    MAP[(int)newPos.Y, (int)newPos.X] == 0)
                {
                    playerPos = newPos;
                }
            }

            // Update the 3D view
            CastRays();

            // Drawing
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Draw upscaled texture to screen
            Rectangle source = new Rectangle(0, 0, INTERNAL_WIDTH, -INTERNAL_HEIGHT); // Flip vertically
            Rectangle dest = new Rectangle(0, 0, WIDTH, HEIGHT);
            Raylib.DrawTexturePro(
                renderTarget.Texture,
                source,
                dest,
                Vector2.Zero,
                0f,
                Color.White
            );

            // Draw minimap and FPS
            DrawMinimap();
            Raylib.DrawFPS(10, 10);

            Raylib.EndDrawing();
        }

        // Cleanup
        Raylib.UnloadRenderTexture(renderTarget);
        foreach (var sprite in sprites) Raylib.UnloadTexture(sprite.Texture);
        Raylib.CloseWindow();
    }
}