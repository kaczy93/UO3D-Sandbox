using System.Runtime.InteropServices;

namespace SDL3_Sandbox;

[StructLayout(LayoutKind.Sequential)]
public record struct TerrainTile(int Id, int X, int Y, int Z);
    
