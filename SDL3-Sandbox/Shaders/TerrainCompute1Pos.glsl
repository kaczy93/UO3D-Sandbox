#version 460

const float TILE_SIZE = 44.0f * inversesqrt(2.0f);
const float TILE_Z_SCALE = 4.0f;

layout (local_size_x = 64) in;

struct TerrainTile {
    int id;
    int x;
    int y;
    int z;
};

layout (std140, set = 0, binding = 0) readonly buffer InputBuffer {
    TerrainTile[] tiles;
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
    VertexVirt[] vertex;
} outputData;

layout(set = 2, binding = 0) uniform MapDimensions {
    vec4 dims;
} mapDimensions;

void main() {
    int WIDTH = int(mapDimensions.dims.x);
    int HEIGHT = int(mapDimensions.dims.y);
    
    uint inputIndex = gl_GlobalInvocationID.x;
    
    TerrainTile tile = inputData.tiles[inputIndex];
    
    //This is index exactly as in map.mul layout
    uint outputIndex = tile.x * HEIGHT + tile.y;
    
//    Vertex vertex = outputData.vertex[outputIndex];
//    vertex.Position = vec3(float(tile.x) * TILE_SIZE, float(tile.y) * TILE_SIZE, float(tile.z) * TILE_Z_SCALE);
//    vertex.Normal = vec3(0.0f);
//    vertex.TexCoord = vec2(float(tile.id), 0.0f); //Let's keep id for now
    VertexVirt vertex = outputData.vertex[outputIndex];
    vertex.PositionXYZNormalX = vec4(float(tile.x) * TILE_SIZE, float(tile.y) * TILE_SIZE, float(tile.z) * TILE_Z_SCALE, 0.0f);
    vertex.NormalYZTexUV = vec4(0.0f, 0.0f, 0.0f, 0.0f);
    
    outputData.vertex[outputIndex] = vertex; //Do we need this assignment?
}

