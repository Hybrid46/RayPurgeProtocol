#version 330

in vec2 fragTexCoord;

uniform sampler2D texture0;     // Sprite texture
uniform sampler2D depthTexture; // Depth texture
uniform vec4 lightingColor;     // Lighting color

uniform float screenWidth;      // Screen width for UV calculation
uniform float spriteDepth;      // Sprite depth
uniform float maxDepth;         // Maximum depth value

out vec4 finalColor;

void main()
{
    // Calculate normalized texture coordinates for depth buffer
    vec2 screenUV = vec2(gl_FragCoord.x / screenWidth, 0.0);

    // Sample depth texture (using red channel)
    float storedDepth = texture(depthTexture, screenUV).r * maxDepth;
    
    // Depth comparison - discard if behind stored depth
    if(spriteDepth > storedDepth) 
        discard;
    
    // Get color from sprite texture
    vec4 spriteColor = texture(texture0, fragTexCoord);
    
    // Discard transparent pixels
    if (spriteColor.a < 0.001) {
        discard;
    }

    // Apply lighting color
    finalColor = vec4(spriteColor.rgb * lightingColor, spriteColor.a);
}