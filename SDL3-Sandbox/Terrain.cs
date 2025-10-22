namespace SDL3_Sandbox;

public class Terrain
{
    private int _totalWidth = 896;
    private int _totalHeight = 512;
    public int Width;
    public int Height;
    public TerrainTile[] Tiles;

    public Terrain(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new TerrainTile[width * height * 64];
    }
    
    public void Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        var globalIndex = 0;
        for (var chunkY = 0; chunkY < Height; chunkY++)
        {
            for (var chunkX = 0; chunkX < Width; chunkX++)
            {
                stream.Seek((chunkX * _totalHeight + chunkY) * 196, SeekOrigin.Begin);
                reader.ReadInt32(); //header
                for (var localY = 0; localY < 8; localY++)
                {
                    for (var localX = 0; localX < 8; localX++)
                    {
                        var tile = new TerrainTile()
                        {
                            X = chunkX * 8 + localX,
                            Y = chunkY * 8 + localY,
                            Id = reader.ReadUInt16(),
                            Z = reader.ReadSByte(),
                        };
                        // _tiles[tile.X * _totalHeight + tile.Y] = tile;
                        Tiles[globalIndex] = tile;
                        globalIndex++;
                    }
                }
            }
        }
    }
}