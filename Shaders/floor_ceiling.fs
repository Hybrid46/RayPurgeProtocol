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

// GI
uniform sampler2D giTexture;
uniform vec2 gridSize;

const int TilesPerRow = 16;

void main()
{
    // ---- Screen space ----
    float flippedY = screenHeight - gl_FragCoord.y;
    vec2 screenPos = vec2(gl_FragCoord.x / screenWidth,
                          flippedY / screenHeight);

    float cameraX = 2.0 * screenPos.x - 1.0;
    vec2 rayDir = playerDir + cameraPlane * cameraX;

    // ---- Is Floor / Ceiling ----
    float horizonY = screenHeight * 0.5;
    bool isCeiling = flippedY < horizonY;

    float yOffset = abs(flippedY - horizonY);
    yOffset = max(yOffset, 0.0001);

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

    // ---- Atlas UV ----
    vec2 localUV = fract(worldPos);

    int col = layer % TilesPerRow;
    int row = layer / TilesPerRow;

    vec2 atlasSize = vec2(textureSize(floorTexture, 0));
    vec2 tileSize = atlasSize / float(TilesPerRow);

    vec2 pixelUV = vec2(col, row) * tileSize
                 + localUV * tileSize;

    vec2 uv = pixelUV / atlasSize;

    // ---- Sample ----
    vec4 color = isCeiling
        ? texture(ceilingTexture, uv)
        : texture(floorTexture, uv);

    // ---- Distance shading ----
    float shade = clamp(1.0 - distanceToPlane * 0.03, 0.3, 1.0);
    color.rgb *= shade;

    vec2 giUV = vec2(worldPos.x / gridSize.x, 1.0 - worldPos.y / gridSize.y);
    vec3 radiance = texture(giTexture, giUV).rgb;

    finalColor = color; // WORKS seeing color only, no GI
    finalColor = vec4(min(color.rgb + radiance, 1), 1); // not working, original merge
    finalColor = vec4(color.rgb * radiance, 1); // NOT WORKS i see color only, no GI -> GI should be multiplied to the smapled floor ceiling texture as lighting, but it is not working, i see only the color of the floor ceiling texture, no GI lighting applied
    finalColor = vec4(radiance, 1); // WORKS, seeing GI only

    // float lum = dot(radiance, vec3(0.299, 0.587, 0.114));
    // finalColor = vec4(vec3(lum), 1.0);

    // vec3 linearColor = pow(color.rgb, vec3(2.2));
    // vec3 result = min(linearColor * radiance, 1.0);
    // finalColor = vec4(pow(result, vec3(1.0/2.2)), color.a);

    // Debugs
    // finalColor = vec4(fract(worldPos), 0, 1);
    // finalColor = vec4(localUV.xy,0,1);
    // finalColor = vec4(pixelUV.xy,0,1);
    // finalColor = vec4(uv.xy,0,1);
}
