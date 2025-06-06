#version 330

// Input from vertex shader
in vec2 fragTexCoord;
in vec4 fragColor;

// Input uniform values
uniform sampler2D texture0;
uniform float spriteDepth;
uniform float zBuffer[200]; // Must match INTERNAL_WIDTH
uniform int screenWidth;    // Must match INTERNAL_WIDTH

// Output fragment color
out vec4 finalColor;

void main()
{
    vec4 texelColor = texture(texture0, fragTexCoord);
    if (texelColor.a == 0.0) discard;

    // Get screen position
    int x = int(gl_FragCoord.x);
    x = clamp(x, 0, screenWidth - 1);
    
    // Depth test - discard if behind wall
    if (zBuffer[x] < spriteDepth) {
        discard;
    }

    finalColor = texelColor * fragColor;
}