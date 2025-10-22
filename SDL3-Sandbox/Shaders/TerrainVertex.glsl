#version 460

layout (location = 0) in vec3 Position;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec2 TexCoord;
layout (location = 0) out vec3 OutNormal;
layout (location = 1) out vec2 OutTexCoord;

struct TerrainTile {
    int id;
    int x;
    int y;
    int z;
};

layout (std140, set = 0, binding = 0) readonly buffer InputBuffer {
    TerrainTile[] tiles;
} inputData;

struct Vertex {
    vec3 Position;
    vec3 Normal;
    vec2 TexCoord;
};

layout(set = 0, binding = 1) readonly buffer TerrainMesh {
    Vertex[] tiles;
};

struct TextureInfo {
    float x;
    float y;
    float w;
    float h;
};

layout(std140, set = 0, binding = 2) readonly buffer TextureInfoBuffer {
    TextureInfo[0x4000] art;
    TextureInfo[0x4000] tex;
} texInfoBuffer;

layout(set = 1, binding = 0) uniform ModelViewProj {
    mat4 matrix;
} modelViewProj;

void main(){
    gl_Position = modelViewProj.matrix * vec4(Position, 1.0);
    OutNormal = Normal;
    OutTexCoord = TexCoord;
}