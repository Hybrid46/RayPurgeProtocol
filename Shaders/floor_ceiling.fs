#version 330

in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D floorIdTex;
uniform sampler2D ceilingIdTex;
uniform sampler2D floorTexture;   // floor atlas
uniform sampler2D ceilingTexture; // ceiling atlas

uniform vec2 playerPos;
uniform vec2 playerDir;
uniform vec2 cameraPlane;

uniform float screenWidth;
uniform float screenHeight;
uniform float horizon;
uniform float camHeight;

const int TilesPerRow = 16;
const float TileSize = 1.0 / float(TilesPerRow);

void main()
{
    float y = gl_FragCoord.y;
    bool isFloor = y > horizon;

    float p = abs(y - horizon);
    if (p < 0.0001) discard;

    float rowDist = camHeight / p;

    float x = (gl_FragCoord.x / screenWidth) * 2.0 - 1.0;
    vec2 rayDir = playerDir + cameraPlane * x;

    vec2 worldPos = playerPos + rayDir * rowDist;

    ivec2 tile = ivec2(floor(worldPos));

    // Convert grid coord to texture UV
    vec2 idUV = (vec2(tile) + 0.5) / vec2(textureSize(floorIdTex, 0));
    float idNorm = isFloor
        ? texture(floorIdTex, idUV).r
        : texture(ceilingIdTex, idUV).r;

    int layer = int(idNorm * 255.0 + 0.5);

    vec2 localUV = fract(worldPos);

    // Compute atlas UV
    float layerF = float(layer);
    float col = mod(layerF, float(TilesPerRow));
    float row = floor(layerF / float(TilesPerRow));
    vec2 uvOffset = vec2(col, row) * TileSize;
    vec2 uv = uvOffset + localUV * TileSize;

    vec4 color = isFloor
        ? texture(floorTexture, uv)
        : texture(ceilingTexture, uv);

    finalColor = color;
}



//#version 330 core

//out vec4 finalColor;

//uniform sampler2D floorIdTex;
//uniform sampler2D ceilingIdTex;
//uniform sampler2D floorTexture;
//uniform sampler2D ceilingTexture;

//uniform vec2 playerPos;
//uniform vec2 playerDir;
//uniform vec2 cameraPlane;

//uniform float screenWidth;
//uniform float horizon;
//uniform float camHeight;

//const int TilesPerRow = 16;
//const float TileSize = 1.0 / float(TilesPerRow);

//void main()
//{
//    float y = gl_FragCoord.y;
//    bool isFloor = y > horizon;

//    float p = abs(y - horizon);
//    if (p < 0.0001) discard;

//    float rowDist = camHeight / p;

//    float x = (gl_FragCoord.x / screenWidth) * 2.0 - 1.0;
//    vec2 rayDir = playerDir + cameraPlane * x;

//    vec2 worldPos = playerPos + rayDir * rowDist;

//    ivec2 tile = ivec2(floor(worldPos));
//    ivec2 mapSize = textureSize(floorIdTex, 0);

//    if (tile.x < 0 || tile.y < 0 || tile.x >= mapSize.x || tile.y >= mapSize.y)
//        discard;

//    int layer = isFloor
//        ? int(texelFetch(floorIdTex, tile, 0).r * 255.0 + 0.5)
//        : int(texelFetch(ceilingIdTex, tile, 0).r * 255.0 + 0.5);

//    if (layer == 0) discard;

//    vec2 localUV = fract(worldPos);
//    localUV.y = 1.0 - localUV.y; // only if needed

//    int col = layer % TilesPerRow;
//    int row = layer / TilesPerRow;

//    vec2 uvOffset = vec2(col, row) * TileSize;

//    float atlasSize = float(textureSize(floorTexture, 0).x);
//    float pad = 0.5 / atlasSize;

//    vec2 uv = uvOffset + localUV * (TileSize - 2.0 * pad) + pad;

//    finalColor = isFloor
//        ? texture(floorTexture, uv)
//        : texture(ceilingTexture, uv);
//}



// Alternative2

//#version 330 core

//in vec2 fragTexCoord;
//in vec4 fragColor;
//out vec4 finalColor;

//uniform sampler2D floorIdTex;
//uniform sampler2D ceilingIdTex;
//uniform sampler2D floorTexture;
//uniform sampler2D ceilingTexture;

//uniform vec2 playerPos;
//uniform vec2 playerDir;
//uniform vec2 cameraPlane;

//uniform float screenWidth;
//uniform float screenHeight;
//uniform float horizon;
//uniform float camHeight;

//const int TilesPerRow = 16;
//const float TileSize = 1.0 / float(TilesPerRow);

//void main()
//{
//    float y = gl_FragCoord.y;
//    bool isFloor = y > horizon;

//    float p = abs(y - horizon);
//    if (p < 0.0001) discard;

//    float rowDist = camHeight / p;

//    float x = (gl_FragCoord.x / screenWidth) * 2.0 - 1.0;
//    vec2 rayDir = playerDir + cameraPlane * x;

//    vec2 worldPos = playerPos + rayDir * rowDist;

//    ivec2 tile = ivec2(floor(worldPos));

//    // Convert grid coord to texture UV
//    vec2 idUV = (vec2(tile) + 0.5) / vec2(textureSize(floorIdTex, 0));
//    float idNorm = isFloor
//        ? texture(floorIdTex, idUV).r
//        : texture(ceilingIdTex, idUV).r;

//    int layer = int(idNorm * 255.0 + 0.5);

//    vec2 localUV = fract(worldPos);

//    // Compute atlas UV
//    float layerF = float(layer);
//    float col = mod(layerF, float(TilesPerRow));
//    float row = floor(layerF / float(TilesPerRow));
//    vec2 uvOffset = vec2(col, row) * TileSize;
//    vec2 uv = uvOffset + localUV * TileSize;

//    vec4 color = isFloor
//        ? texture(floorTexture, uv)
//        : texture(ceilingTexture, uv);

//    finalColor = color;
//}
