using PurgeProtocol.ComponentSystem;
using Raylib_cs;
using System.Numerics;

public class RC2DGI
{
    // ---------- Config ----------
    const int Width = 1000;
    const int Height = 1000;

    // ---------- Shaders ----------
    private Shader screenUV_shader;
    private Shader jumpFlood_shader;
    private Shader distanceField_shader;
    private Shader GI_shader;
    private Shader GIBlitter_shader;
    private Shader blur_shader;

    // ---------- Render textures ----------
    private RenderTexture2D colorRT;
    private RenderTexture2D emissiveRT;
    private RenderTexture2D jumpRT1, jumpRT2;
    private RenderTexture2D distRT;
    private RenderTexture2D giRT1, giRT2;
    private RenderTexture2D tempRT;
    private RenderTexture2D cascadeBlurRT;

    // GI Config
    private int cascadeCount;
    private float renderScale;
    private float rayRange;
    private Vector2IntR cascadeResolution;
    private float cascadeBlurRadius = 1.5f;
    private float reflectivity = 0.0f;

    public void Initialize()
    {
        // Load shaders
        screenUV_shader = Raylib.LoadShader(null, "shaders/ScreenUV.fs");
        jumpFlood_shader = Raylib.LoadShader(null, "shaders/JumpFlood.fs");
        distanceField_shader = Raylib.LoadShader(null, "shaders/DistanceField.fs");
        GI_shader = Raylib.LoadShader(null, "shaders/RadianceCascades.fs");
        GIBlitter_shader = Raylib.LoadShader(null, "shaders/Merge.fs");
        blur_shader = Raylib.LoadShader(null, "shaders/Blur.fs");

        // Setup GI config
        cascadeCount = 6;
        renderScale = 1.0f;
        rayRange = 2.0f;

        double powVal = Math.Pow(2, cascadeCount);
        int cascadeWidth = (int)Math.Ceiling((Width * renderScale) / powVal) * (int)powVal;
        int cascadeHeight = (int)Math.Ceiling((Height * renderScale) / powVal) * (int)powVal;
        cascadeResolution = new Vector2IntR(cascadeWidth, cascadeHeight);

        emissiveRT = Raylib.LoadRenderTexture(Width, Height);
        colorRT = Raylib.LoadRenderTexture(Width, Height);
        distRT = Raylib.LoadRenderTexture(Width, Height);
        jumpRT1 = Raylib.LoadRenderTexture(Width, Height);
        jumpRT2 = Raylib.LoadRenderTexture(Width, Height);
        giRT1 = Raylib.LoadRenderTexture(cascadeResolution.x, cascadeResolution.y);
        giRT2 = Raylib.LoadRenderTexture(cascadeResolution.x, cascadeResolution.y);
        tempRT = Raylib.LoadRenderTexture(Width, Height);
        cascadeBlurRT = Raylib.LoadRenderTexture(cascadeResolution.x, cascadeResolution.y);

        Raylib.SetTextureFilter(emissiveRT.Texture, TextureFilter.Point);
        Raylib.SetTextureFilter(colorRT.Texture, TextureFilter.Point);
        Raylib.SetTextureFilter(distRT.Texture, TextureFilter.Point);
        Raylib.SetTextureFilter(jumpRT1.Texture, TextureFilter.Point);
        Raylib.SetTextureFilter(jumpRT2.Texture, TextureFilter.Point);
        Raylib.SetTextureFilter(tempRT.Texture, TextureFilter.Point);

        Raylib.SetTextureFilter(cascadeBlurRT.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(giRT1.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(giRT2.Texture, TextureFilter.Bilinear);

        ClearAllRTs();
    }

    public void Update(List<Rectangle> absorbers, List<LightEmitter> lightEmitters)
    {
        ClearAllRTs();

        // 1. Scene render
        // Absorbers
        Raylib.BeginTextureMode(colorRT);
        foreach (Rectangle rectangle in absorbers)
        {
            Raylib.DrawRectangle((int)rectangle.Center.X,
                                 (int)rectangle.Center.Y,
                                 (int)rectangle.Width,
                                 (int)rectangle.Height,
                                 Color.White);
        }
        Raylib.EndTextureMode();

        // Emitters
        Raylib.BeginTextureMode(emissiveRT);
        foreach (LightEmitter lightEmitter in lightEmitters)
        {
            Vector2 position = lightEmitter.Entity.transform.Position;

            if (lightEmitter.isCircle)
            {
                Raylib.DrawCircleV(position, lightEmitter.radius, lightEmitter.color);
            }
            else
            {
                Raylib.DrawRectangle((int)position.X, (int)position.Y, lightEmitter.width, lightEmitter.height, lightEmitter.color);
            }
        }
        Raylib.EndTextureMode();

        // 2. RC2DGI pipeline
        DoRC2DGI();

        // 3. Display final
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        // Draw main scene
        Raylib.DrawTextureRec(colorRT.Texture,
            new Rectangle(0, 0, colorRT.Texture.Width, -colorRT.Texture.Height),
            Vector2.Zero, Color.White);

        // Draw debug textures
        int debugSize = 100;
        int padding = 10;
        int startX = Width - debugSize - padding;

        DrawDebugTexture(colorRT, new Vector2(startX, padding), debugSize, "Scene");
        DrawDebugTexture(emissiveRT, new Vector2(startX, padding * 2 + debugSize), debugSize, "Emissive");
        DrawDebugTexture(jumpRT2, new Vector2(startX, padding * 3 + debugSize * 2), debugSize, "Jump2");
        DrawDebugTexture(distRT, new Vector2(startX, padding * 4 + debugSize * 3), debugSize, "Distance");
        DrawDebugTexture(giRT1, new Vector2(startX, padding * 5 + debugSize * 4), debugSize, "GI1");
        DrawDebugTexture(giRT2, new Vector2(startX, padding * 6 + debugSize * 5), debugSize, "GI2");
        DrawDebugTexture(tempRT, new Vector2(startX, padding * 7 + debugSize * 6), debugSize, "temp");

        Raylib.EndDrawing();
    }

    public void Dispose()
    {
        Raylib.UnloadRenderTexture(colorRT);
        Raylib.UnloadRenderTexture(emissiveRT);
        Raylib.UnloadRenderTexture(jumpRT1);
        Raylib.UnloadRenderTexture(jumpRT2);
        Raylib.UnloadRenderTexture(distRT);
        Raylib.UnloadRenderTexture(giRT1);
        Raylib.UnloadRenderTexture(giRT2);
        Raylib.UnloadRenderTexture(tempRT);
        Raylib.UnloadRenderTexture(cascadeBlurRT);

        Raylib.UnloadShader(screenUV_shader);
        Raylib.UnloadShader(jumpFlood_shader);
        Raylib.UnloadShader(distanceField_shader);
        Raylib.UnloadShader(GI_shader);
        Raylib.UnloadShader(GIBlitter_shader);
        Raylib.UnloadShader(blur_shader);

        Raylib.CloseWindow();
    }

    // ---------- RC2DGI pipeline ----------
    private void DoRC2DGI()
    {
        bool jumpFlood1IsFinal = false;
        bool gi1IsFinal = false;
        Vector2 aspect = new Vector2(Width, Height) / Math.Max(Width, Height);
        Vector2 screen = new Vector2(Width, Height);

        // ---- 1. ScreenUV ----
        //Copying the color texture to another one using Screen UV shader to start the JumpFlood Algorithm
        Raylib.BeginTextureMode(jumpRT1);
        Raylib.ClearBackground(Color.Black);
        Raylib.BeginShaderMode(screenUV_shader);
        Raylib.DrawTextureRec(colorRT.Texture,
            new Rectangle(0, 0, colorRT.Texture.Width, -colorRT.Texture.Height),
            Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        //Start JumpFlood Algorithm
        jumpFlood1IsFinal = true;
        int max = (int)Math.Max(screen.X, screen.Y);
        //int steps = Mathf.CeilToInt(Mathf.Log(max)); -> Unity original
        int steps = (int)Math.Ceiling(Math.Log(max, 2.0));
        if (steps < 1) steps = 1;

        float stepSize = 1.0f;

        for (int i = 0; i < steps; ++i)
        {
            stepSize *= 0.5f;
            Raylib.SetShaderValue(jumpFlood_shader, Raylib.GetShaderLocation(jumpFlood_shader, "_StepSize"), stepSize, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(jumpFlood_shader, Raylib.GetShaderLocation(jumpFlood_shader, "_Aspect"), aspect, ShaderUniformDataType.Vec2);

            if (jumpFlood1IsFinal)
            {
                // src jumpRT1 → dst jumpRT2
                Raylib.BeginTextureMode(jumpRT2);
                Raylib.BeginShaderMode(jumpFlood_shader);
                Raylib.DrawTextureRec(jumpRT1.Texture,
                    new Rectangle(0, 0, jumpRT1.Texture.Width, -jumpRT1.Texture.Height),
                    Vector2.Zero, Color.White);
                Raylib.EndShaderMode();
                Raylib.EndTextureMode();
            }
            else
            {
                // src jumpRT2 → dst jumpRT1
                Raylib.BeginTextureMode(jumpRT1);
                Raylib.BeginShaderMode(jumpFlood_shader);
                Raylib.DrawTextureRec(jumpRT2.Texture,
                    new Rectangle(0, 0, jumpRT2.Texture.Width, -jumpRT2.Texture.Height),
                    Vector2.Zero, Color.White);
                Raylib.EndShaderMode();
                Raylib.EndTextureMode();
            }

            jumpFlood1IsFinal = !jumpFlood1IsFinal;
        }

        // ---- 3. Distance field ----
        RenderTexture2D finalJumpFloodRT = jumpFlood1IsFinal ? jumpRT1 : jumpRT2;
        Raylib.BeginTextureMode(distRT);
        Raylib.BeginShaderMode(distanceField_shader);

        //aspect ratio unnecessary because both textures are same size
        //Raylib.SetShaderValue(distanceField_shader, Raylib.GetShaderLocation(distanceField_shader, "_Aspect"), aspect, ShaderUniformDataType.Vec2);

        Raylib.DrawTextureRec(finalJumpFloodRT.Texture,
            new Rectangle(0, 0, finalJumpFloodRT.Texture.Width, -finalJumpFloodRT.Texture.Height),
            Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        // ---- 4. Radiance cascades ----
        gi1IsFinal = false;

        for (int i = cascadeCount - 1; i >= 0; i--)
        {
            RenderTexture2D srcGI = gi1IsFinal ? giRT1 : giRT2;
            RenderTexture2D dstGI = gi1IsFinal ? giRT2 : giRT1;

            Raylib.BeginTextureMode(dstGI);
            Raylib.ClearBackground(Color.Black);
            Raylib.BeginShaderMode(GI_shader);
            SetGIShaderValues(aspect, i);

            Raylib.DrawTextureRec(srcGI.Texture,
                new Rectangle(0, 0, srcGI.Texture.Width, -srcGI.Texture.Height),
                Vector2.Zero, Color.White);
            Raylib.EndShaderMode();
            Raylib.EndTextureMode();

            gi1IsFinal = !gi1IsFinal;
        }

        // Determine the final cascade buffer
        RenderTexture2D finalGI = gi1IsFinal ? giRT1 : giRT2;

        if (cascadeBlurRadius > 0f)
        {
            // ---- 5. Blur the final cascade result ----
            Raylib.BeginTextureMode(cascadeBlurRT);
            Raylib.ClearBackground(Color.Black);
            Raylib.BeginShaderMode(blur_shader);
            Raylib.SetShaderValue(blur_shader, Raylib.GetShaderLocation(blur_shader, "_Resolution"), new Vector2(cascadeResolution.x, cascadeResolution.y), ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(blur_shader, Raylib.GetShaderLocation(blur_shader, "_BlurRadius"), cascadeBlurRadius, ShaderUniformDataType.Float);
            Raylib.DrawTextureRec(finalGI.Texture,
                new Rectangle(0, 0, finalGI.Texture.Width, -finalGI.Texture.Height),
                Vector2.Zero, Color.White);
            Raylib.EndShaderMode();
            Raylib.EndTextureMode();

            // Copy blurred cascade back to finalGI
            Raylib.BeginTextureMode(finalGI);
            Raylib.DrawTextureRec(cascadeBlurRT.Texture,
                new Rectangle(0, 0, cascadeBlurRT.Texture.Width, -cascadeBlurRT.Texture.Height),
                Vector2.Zero, Color.White);
            Raylib.EndTextureMode();
        }

        // ---- 6. Merge blurred GI with scene ----
        Raylib.BeginTextureMode(tempRT);
        Raylib.BeginShaderMode(GIBlitter_shader);
        Raylib.SetShaderValueTexture(GIBlitter_shader, Raylib.GetShaderLocation(GIBlitter_shader, "_GITex"), finalGI.Texture);
        Raylib.DrawTextureRec(colorRT.Texture,
            new Rectangle(0, 0, colorRT.Texture.Width, -colorRT.Texture.Height),
            Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        // Copy back to colorRT
        Raylib.BeginTextureMode(colorRT);
        Raylib.DrawTextureRec(tempRT.Texture,
            new Rectangle(0, 0, tempRT.Texture.Width, -tempRT.Texture.Height),
            Vector2.Zero, Color.White);
        Raylib.EndTextureMode();

    }

    private void SetGIShaderValues(Vector2 aspect, int cascadeLevel)
    {
        // bind textures
        Raylib.SetShaderValueTexture(GI_shader, Raylib.GetShaderLocation(GI_shader, "_ColorTex"), colorRT.Texture);
        Raylib.SetShaderValueTexture(GI_shader, Raylib.GetShaderLocation(GI_shader, "_EmissiveTex"), emissiveRT.Texture);
        Raylib.SetShaderValueTexture(GI_shader, Raylib.GetShaderLocation(GI_shader, "_DistanceTex"), distRT.Texture);

        // uniform params
        Vector2 cascadeResVec = new Vector2(cascadeResolution.x, cascadeResolution.y);
        int locCascadeRes = Raylib.GetShaderLocation(GI_shader, "_CascadeResolution");
        Raylib.SetShaderValue(GI_shader, locCascadeRes, cascadeResVec, ShaderUniformDataType.Vec2);

        Raylib.SetShaderValue(GI_shader, Raylib.GetShaderLocation(GI_shader, "_CascadeLevel"), cascadeLevel, ShaderUniformDataType.Int);
        Raylib.SetShaderValue(GI_shader, Raylib.GetShaderLocation(GI_shader, "_CascadeCount"), cascadeCount, ShaderUniformDataType.Int);
        Raylib.SetShaderValue(GI_shader, Raylib.GetShaderLocation(GI_shader, "_Aspect"), aspect, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(GI_shader, Raylib.GetShaderLocation(GI_shader, "_RayRange"), rayRange, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(GI_shader, Raylib.GetShaderLocation(GI_shader, "_Reflectivity"), reflectivity, ShaderUniformDataType.Float);
    }

    private void DrawDebugTexture(RenderTexture2D texture, Vector2 position, int size, string label)
    {
        // Draw the texture
        Raylib.DrawTexturePro(texture.Texture,
            new Rectangle(0, 0, texture.Texture.Width, -texture.Texture.Height),
            new Rectangle(position.X, position.Y, size, size),
            Vector2.Zero, 0f, Color.White);

        // Draw a border
        Raylib.DrawRectangleLines((int)position.X, (int)position.Y, size, size, Color.Red);

        // Draw label
        Raylib.DrawText(label, (int)position.X, (int)position.Y - 20, 20, Color.White);
    }

    private void ClearAllRTs()
    {
        ClearTexture(colorRT);
        ClearTexture(emissiveRT);
        ClearTexture(jumpRT1);
        ClearTexture(jumpRT2);
        ClearTexture(distRT);
        ClearTexture(giRT1);
        ClearTexture(giRT2);
        ClearTexture(tempRT);

        void ClearTexture(RenderTexture2D texture)
        {
            Raylib.BeginTextureMode(texture);
            Raylib.ClearBackground(Color.Black);
            Raylib.EndTextureMode();
        }
    }
}
