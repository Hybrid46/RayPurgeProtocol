#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;     // Sprite texture
uniform sampler2D depthTexture; // Depth texture (R32F format)
uniform float lightingFactor;   // Lighting factor (scalar)
uniform float spriteDepth;      // Sprite depth

out vec4 finalColor;

void main()
{
    // Get depth value directly from red channel (no reconstruction needed)
    float storedDepth = texelFetch(depthTexture, ivec2(int(gl_FragCoord.x), 0), 0).r;
    
    // Depth comparison with epsilon to prevent z-fighting
    if (spriteDepth > storedDepth + 0.01) {
        discard;
    }

    // Get color from sprite texture
    vec4 texelColor = texture(texture0, fragTexCoord);
    
    // Discard transparent pixels
    if (texelColor.a < 0.001) {
        discard;
    }

    // Apply lighting
    finalColor = vec4(texelColor.rgb * lightingFactor * fragColor.rgb, texelColor.a);
}