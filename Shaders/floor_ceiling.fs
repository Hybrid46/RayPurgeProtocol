#version 330 core
out vec4 finalColor;

uniform sampler2D floorTexture;
uniform sampler2D ceilingTexture;
uniform sampler2D floorIdTex;
uniform sampler2D ceilingIdTex;

uniform vec2 playerPos;
uniform vec2 playerDir;
uniform vec2 cameraPlane;

uniform float screenWidth;
uniform float screenHeight;

const int TilesPerRow = 16;

void main()
{
    // ---- Screen space ----
    float flippedY = screenHeight - gl_FragCoord.y;
    vec2 screenPos = vec2(gl_FragCoord.x / screenWidth,
                          flippedY / screenHeight);

    float cameraX = 2.0 * screenPos.x - 1.0;
    vec2 rayDir = playerDir + cameraPlane * cameraX;

    // ---- Floor / Ceiling ----
    float horizonY = screenHeight * 0.5;
    bool isCeiling = flippedY < horizonY;

    float yOffset = abs(flippedY - horizonY);
    yOffset = max(yOffset, 0.0001);

    // ✅ KEEP THIS — THIS IS WHY YOUR UVs WORK
    float distanceToPlane = horizonY / yOffset;

    // ---- World position ----
    vec2 worldPos = playerPos + rayDir * distanceToPlane;

    // ---- Tile lookup ----
    ivec2 tile = ivec2(floor(worldPos));
    ivec2 mapSize = textureSize(floorIdTex, 0);

    if (tile.x < 0 || tile.y < 0 || tile.x >= mapSize.x || tile.y >= mapSize.y)
        discard;

    float idNorm = isCeiling
        ? texelFetch(ceilingIdTex, tile, 0).r
        : texelFetch(floorIdTex, tile, 0).r;

    int layer = int(idNorm * 255.0);
    if (layer == 0) discard;

    // ---- Local UV (UNCHANGED SCALE) ----
    vec2 localUV = fract(worldPos);

    // ---- Atlas UV ----
    int col = layer % TilesPerRow;
    int row = layer / TilesPerRow;

    vec2 atlasSize = vec2(textureSize(floorTexture, 0));
    vec2 tileSize = atlasSize / float(TilesPerRow);

    vec2 pixelUV = vec2(col, row) * tileSize
                 + localUV * tileSize
                 + vec2(0.5);

    vec2 uv = pixelUV / atlasSize;

    // ---- Sample ----
    vec4 color = isCeiling
        ? texture(ceilingTexture, uv)
        : texture(floorTexture, uv);

    // ---- Distance shading (same feel as before) ----
    float shade = clamp(1.0 - distanceToPlane * 0.03, 0.3, 1.0);
    color.rgb *= shade;

    finalColor = color;
}
