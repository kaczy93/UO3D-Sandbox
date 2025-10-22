#version 460

const vec3 LIGHT_DIRECTION = normalize(vec3(0.0f, 1.0f, 1.0f));
const float BRIGHT_LIGHT = 1.5f;

layout (location = 0) out vec4 FragColor;

layout (location = 0) in vec3 InNormal;
layout (location = 1) in vec2 InTexCoord;

layout (set = 2, binding = 0) uniform sampler2D ArtSampler;
layout (set = 2, binding = 1) uniform sampler2D TexSampler;

float get_light(vec3 normal)
{
    vec3 normalizedNormal = normalize(normal); //Shouldn't input be already normalized here?
    float base = (max(dot(normalizedNormal, LIGHT_DIRECTION), 0.0f) / 2.0f) + 0.5f;

    // At 45 degrees (the angle the flat tiles are lit at) it must come out
    // to (cos(45) / 2) + 0.5 or 0.85355339...
    return base + ((BRIGHT_LIGHT * (base - 0.85355339f)) - (base - 0.85355339f));
}

void main()
{
    vec4 color;
    if(InTexCoord.x > 0) {
        color = texture(TexSampler, InTexCoord - 1);
    }
    else {
        color = texture(ArtSampler, InTexCoord);
    }
    
    if(color.a == 0)
            discard;
    
    color.rgb *= get_light(InNormal);
    
    FragColor = color;
}
