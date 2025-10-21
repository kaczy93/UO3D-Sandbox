#version 460

//This two should be uniform of landscape dimensions
const uint WIDTH = 8; 
const uint HEIGHT = 8;

layout (local_size_x = 64) in;

//We could pass only x,y here
struct TerrainTile {
    int id;
    int x;
    int y;
    int z;
};

layout (std140, set = 0, binding = 0) readonly buffer InputBuffer {
    TerrainTile[] tilePos;
} inputData;

struct VertexVirt {
    vec4 PositionXYZNormalX;
    vec4 NormalYZTexUV;
};

struct VertexReal {
    vec3 Position;
    vec3 Normal;
    vec2 TexCoord;
};

layout(std140, set = 0, binding = 1) readonly buffer InVertexBuffer {
    VertexVirt[] vertices;
} inputVertex;

layout(std140, set = 1, binding = 0) buffer OutVertexBuffer {
    VertexVirt[] vertices;
} outputVertex;

void main() {
    TerrainTile inputTile = inputData.tilePos[gl_GlobalInvocationID.x];
    uint inputIndex   = inputTile.x * HEIGHT + inputTile.y;
    
    uint outputVertexIndex = inputIndex * 6;
    
    outputVertex.vertices[outputVertexIndex] = inputVertex.vertices[inputIndex];
    outputVertex.vertices[outputVertexIndex].NormalYZTexUV.zw = vec2(0,0);

    outputVertex.vertices[outputVertexIndex + 1] = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + inputTile.y];
    outputVertex.vertices[outputVertexIndex + 1].NormalYZTexUV.zw = vec2(1,0);
    
    outputVertex.vertices[outputVertexIndex + 2] = inputVertex.vertices[inputTile.x * HEIGHT + min(inputTile.y + 1, HEIGHT - 1)];
    outputVertex.vertices[outputVertexIndex + 2].NormalYZTexUV.zw = vec2(0,1);
    
    outputVertex.vertices[outputVertexIndex + 3] = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + min(inputTile.y + 1, HEIGHT - 1)];
    outputVertex.vertices[outputVertexIndex + 3].NormalYZTexUV.zw = vec2(1,1);

    outputVertex.vertices[outputVertexIndex + 4] = outputVertex.vertices[outputVertexIndex + 2];
    outputVertex.vertices[outputVertexIndex + 5] = outputVertex.vertices[outputVertexIndex + 1];
    
    
//    outputVertex.vertices[outputVertexIndex + 4] = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + min(inputTile.y + 1, HEIGHT - 1)];
//    outputVertex.vertices[outputVertexIndex + 4].NormalYZTexUV.zw = vec2(1,1);
//    
//    outputVertex.vertices[outputVertexIndex + 5] = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + inputTile.y];
//    outputVertex.vertices[outputVertexIndex + 5].NormalYZTexUV.zw = vec2(1,0);
}

