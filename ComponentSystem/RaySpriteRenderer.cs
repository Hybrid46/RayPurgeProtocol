using Raylib_cs;
using System.Numerics;
using static Settings;

public class RaySpriteRenderer : Component
{
    public Texture2D Texture { get; set; }
    public Vector2 Position => Entity.transform.Position;

    public void Draw(Entity player, Shader spriteShader)
    {
        PlayerController playerController = player.playerController;

        // Transform sprite position relative to camera
        Vector2 spritePos = Position - player.transform.Position;
        float invDet = 1.0f / (playerController.CameraPlane.X * playerController.Direction.Y - playerController.Direction.X * playerController.CameraPlane.Y);

        float transformX = invDet * (playerController.Direction.Y * spritePos.X - playerController.Direction.X * spritePos.Y);
        float transformY = invDet * (-playerController.CameraPlane.Y * spritePos.X + playerController.CameraPlane.X * spritePos.Y);

        if (transformY <= 0) return;  // Behind camera

        // Calculate sprite screen position and size
        int spriteScreenX = (int)((internalScreenWidth / 2) * (1 + transformX / transformY));
        int spriteHeight = Math.Abs((int)(internalScreenHeight / transformY));
        if (spriteHeight < 2) return;

        float aspectRatio = Texture.Width / (float)Texture.Height;
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
            texOffsetX * Texture.Width,
            texOffsetY * Texture.Height,
            texWidth * Texture.Width,
            texHeight * Texture.Height
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
            Texture,
            srcRect,
            destRect,
            Vector2.Zero,
            0f,
            tint
        );
        Raylib.EndShaderMode();
    }
}