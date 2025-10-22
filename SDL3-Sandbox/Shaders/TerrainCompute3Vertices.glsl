#version 460

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

layout(std140, set = 0, binding = 1) readonly buffer InVertexBuffer {
    VertexVirt[] vertices;
} inputVertex;

struct Rect { 
    float x;
    float y;
    float w;
    float h;
};

struct TexInfo {
    Rect art;
    Rect tex;
};

layout(std140, set = 0, binding = 2) readonly buffer TextureInfoBuffer {
    TexInfo[0x4000] info;
} texInfoBuffer;

layout(std140, set = 1, binding = 0) buffer OutVertexBuffer {
    VertexVirt[] vertices;
} outputVertex;

layout(set = 2, binding = 0) uniform MapDimensions {
    vec4 dims;
} mapDimensions;

void main() {
    int WIDTH = int(mapDimensions.dims.x);
    int HEIGHT = int(mapDimensions.dims.y);
    
    TerrainTile inputTile = inputData.tilePos[gl_GlobalInvocationID.x];
    uint inputIndex   = inputTile.x * HEIGHT + inputTile.y;
    
    uint outputVertexIndex = inputIndex * 6;

    VertexVirt top = inputVertex.vertices[inputIndex];
    VertexVirt right = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + inputTile.y];
    VertexVirt left =  inputVertex.vertices[inputTile.x * HEIGHT + min(inputTile.y + 1, HEIGHT - 1)];
    VertexVirt bottom = inputVertex.vertices[min(inputTile.x + 1, WIDTH - 1) * HEIGHT + min(inputTile.y + 1, HEIGHT - 1)];
    
    if( top.PositionXYZNormalX.z == right.PositionXYZNormalX.z && 
        top.PositionXYZNormalX.z == left.PositionXYZNormalX.z &&
        top.PositionXYZNormalX.z == bottom.PositionXYZNormalX.z)
    {
        //Flat tile
        Rect texInfoRect = texInfoBuffer.info[inputTile.id].art;
        top.NormalYZTexUV.zw = vec2(texInfoRect.x + texInfoRect.w * 0.5f, texInfoRect.y);
        right.NormalYZTexUV.zw = vec2(texInfoRect.x + texInfoRect.w, texInfoRect.y + texInfoRect.h * 0.5f);
        left.NormalYZTexUV.zw = vec2(texInfoRect.x, texInfoRect.y + texInfoRect.h * 0.5f);
        bottom.NormalYZTexUV.zw = vec2(texInfoRect.x + texInfoRect.w * 0.5f, texInfoRect.y + texInfoRect.h);
    }
    else
    {
        Rect texInfoRect = texInfoBuffer.info[inputTile.id].tex;
        top.NormalYZTexUV.zw = vec2(texInfoRect.x, texInfoRect.y);
        right.NormalYZTexUV.zw = vec2(texInfoRect.x + texInfoRect.w, texInfoRect.y);
        left.NormalYZTexUV.zw = vec2(texInfoRect.x, texInfoRect.y + texInfoRect.h);
        bottom.NormalYZTexUV.zw = vec2(texInfoRect.x + texInfoRect.w, texInfoRect.y + texInfoRect.h);
    }
    
   
    
    outputVertex.vertices[outputVertexIndex] = top;
    outputVertex.vertices[outputVertexIndex + 1] = right;
    outputVertex.vertices[outputVertexIndex + 2] = left;
    outputVertex.vertices[outputVertexIndex + 3] = bottom;
    outputVertex.vertices[outputVertexIndex + 4] = left;
    outputVertex.vertices[outputVertexIndex + 5] = right;
}

