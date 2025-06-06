using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

class Raycaster
{
    class Sprite
    {
        public Vector2 Position;
        public Texture2D Texture;

        public Sprite(Vector2 position, Texture2D texture)
        {
            Position = position;
            Texture = texture;
        }
    }

    static List<Sprite> sprites = new();

    const int WIDTH = 800;
    const int HEIGHT = 600;
    const int INTERNAL_WIDTH = 400;
    const int INTERNAL_HEIGHT = 300;
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

        Texture2D enemyTex = Raylib.LoadTexture("enemy.png");
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

            if (transformY <= 0) continue; // Sprite is behind the camera

            // Projected screen position
            int spriteScreenX = (int)((INTERNAL_WIDTH / 2) * (1 + transformX / transformY));

            // Size scaling
            int spriteHeight = Math.Abs((int)(INTERNAL_HEIGHT / transformY));
            int spriteWidth = spriteHeight;

            // Destination rectangle (on screen)
            Rectangle dest = new Rectangle(
                spriteScreenX - spriteWidth / 2,
                INTERNAL_HEIGHT / 2 - spriteHeight / 2,
                spriteWidth,
                spriteHeight
            );

            // Source rectangle (from texture)
            Rectangle source = new Rectangle(0, 0, sprite.Texture.Width, sprite.Texture.Height);

            // Depth check using zBuffer (clip horizontally)
            int startX = (int)Math.Clamp(dest.X, 0, INTERNAL_WIDTH);
            int endX = (int)Math.Clamp(dest.X + dest.Width, 0, INTERNAL_WIDTH);

            bool occluded = false;
            for (int x = startX; x < endX; x++)
            {
                if (transformY > 0 && x >= 0 && x < INTERNAL_WIDTH && transformY < zBuffer[x])
                {
                    occluded = false;
                    break;
                }
                else
                {
                    occluded = true;
                }
            }

            if (!occluded)
            {
                Raylib.DrawTexturePro(sprite.Texture, source, dest, Vector2.Zero, 0, Color.White);
            }
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
                zBuffer[x] = perpWallDist;

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
            if (Raylib.IsKeyDown(KeyboardKey.A))
            {
                // Rotate left using matrix multiplication
                playerDir = Vector2.Transform(playerDir, Matrix3x2.CreateRotation(ROT_SPEED));
                cameraPlane = Vector2.Transform(cameraPlane, Matrix3x2.CreateRotation(ROT_SPEED));
            }

            if (Raylib.IsKeyDown(KeyboardKey.D))
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

            if (Raylib.IsKeyDown(KeyboardKey.E))
            {
                Vector2 newPos = playerPos - new Vector2(playerDir.Y, -playerDir.X) * MOVE_SPEED;
                if (MAP[(int)newPos.Y, (int)newPos.X] == 0) playerPos = newPos;
            }

            if (Raylib.IsKeyDown(KeyboardKey.Q))
            {
                Vector2 newPos = playerPos + new Vector2(playerDir.Y, -playerDir.X) * MOVE_SPEED;
                if (MAP[(int)newPos.Y, (int)newPos.X] == 0) playerPos = newPos;
            }

            // Update the 3D view
            CastRays();
            DrawSprites();

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