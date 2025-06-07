#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform int screenWidth;
uniform int screenHeight;
uniform float zBuffer[400]; // Must match INTERNAL_WIDTH
uniform float spriteDepth;

out vec4 finalColor;

void main()
{
    // Calculate screen position
    int x = int(gl_FragCoord.x);
    int y = screenHeight - int(gl_FragCoord.y); // Flip Y to match buffer
    
    // Only process pixels within viewport
    if (x < 0 || x >= screenWidth || y < 0 || y >= screenHeight) {
        discard;
    }

    // Get color from texture
    vec4 texelColor = texture(texture0, fragTexCoord);
    
    // Discard transparent pixels
    if (texelColor.a < 0.001) {
        discard;
    }

    // Depth comparison
    if (spriteDepth > zBuffer[x]) {
        discard;
    }

    // Apply distance shading
    float shade = clamp(1.0 - spriteDepth * 0.03, 0.3, 1.0);
    finalColor = vec4(texelColor.rgb * shade * fragColor.rgb, texelColor.a);
}