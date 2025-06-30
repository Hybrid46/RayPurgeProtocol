#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

out vec4 finalColor;

uniform vec2 playerPos;
uniform vec2 playerDir;
uniform vec2 cameraPlane;
uniform float screenWidth;
uniform float screenHeight;
uniform sampler2D ceilingTexture;
uniform sampler2D floorTexture;

void main()
{
    // Flip Y coordinate to match Raylib's coordinate system
    float flippedY = screenHeight - gl_FragCoord.y;
    
    // Calculate normalized screen coordinates
    vec2 screenPos = vec2(gl_FragCoord.x / screenWidth, flippedY / screenHeight);
    
    // Calculate ray direction for this pixel
    float cameraX = 2.0 * screenPos.x - 1.0;
    vec2 rayDir = playerDir + cameraPlane * cameraX;
    
    // Determine if we're drawing ceiling or floor
    bool isCeiling = flippedY < (screenHeight / 2.0);
    float horizonY = screenHeight / 2.0;
    float yOffset = isCeiling ? 
        (horizonY - flippedY) : 
        (flippedY - horizonY);
    
    // Avoid division by zero
    yOffset = max(yOffset, 0.0001);
    
    // Calculate distance to plane
    float distanceToPlane = (screenHeight / 2.0) / yOffset;
    
    // Calculate world position
    vec2 worldPos = playerPos + rayDir * distanceToPlane;
    
    // Sample texture with tiling
    vec2 texCoord = mod(worldPos, 1.0);
    
    // Apply distance shading
    float shade = clamp(1.0 - distanceToPlane * 0.03, 0.3, 1.0);
    
    // Choose texture based on ceiling/floor
    vec4 texColor = isCeiling ? 
        texture(ceilingTexture, texCoord) : 
        texture(floorTexture, texCoord);
    
    // Apply shading
    texColor.rgb *= shade;
    finalColor = texColor;
}