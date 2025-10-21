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

layout(std140, set = 1, binding = 0) buffer VertexBuffer {
    VertexVirt[] vertices;
} inOutVert;

void magic(VertexVirt tile, VertexVirt tile1, VertexVirt tile2, inout vec3 normal){
    vec3 u = tile1.PositionXYZNormalX.xyz - tile.PositionXYZNormalX.xyz;
    vec3 v = tile2.PositionXYZNormalX.xyz - tile.PositionXYZNormalX.xyz;
    normal = normal + cross(u,v);
}

void main() {
    TerrainTile inputTile = inputData.tilePos[gl_GlobalInvocationID.x];
    uint tileIndex   = inputTile.x * HEIGHT + inputTile.y;

    VertexVirt tile = inOutVert.vertices[tileIndex];

    VertexVirt topTile    = inOutVert.vertices[inputTile.x * HEIGHT + max(inputTile.y - 1, 0)];
    VertexVirt bottomTile = inOutVert.vertices[inputTile.x * HEIGHT + min(inputTile.y + 1, HEIGHT)];
    VertexVirt leftTile   = inOutVert.vertices[max(inputTile.x - 1, 0)     * HEIGHT + inputTile.y];
    VertexVirt rightTile  = inOutVert.vertices[min(inputTile.x + 1, WIDTH) * HEIGHT + inputTile.y];

    vec3 res = vec3(0);
    magic(tile, leftTile, topTile, res);
    magic(tile, topTile, rightTile, res);
    magic(tile, rightTile, bottomTile, res);
    magic(tile, bottomTile, leftTile, res);
    
    res = normalize(res);
    
    inOutVert.vertices[tileIndex].PositionXYZNormalX.w = res.x;
    inOutVert.vertices[tileIndex].NormalYZTexUV.xy = res.yz;
}



