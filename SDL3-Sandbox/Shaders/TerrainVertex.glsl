#version 460

layout (location = 0) in vec3 Position;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec2 TexCoord;
layout (location = 0) out vec3 OutNormal;
layout (location = 1) out vec2 OutTexCoord;

layout(set = 1, binding = 0) uniform ModelViewProj {
    mat4 matrix;
} modelViewProj;

void main(){
    gl_Position = modelViewProj.matrix * vec4(Position, 1.0);
    OutNormal = Normal;
    OutTexCoord = TexCoord;
}