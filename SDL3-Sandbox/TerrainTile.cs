using System.Runtime.InteropServices;

namespace SDL3_Sandbox;

//This comes as ushorts and sbyte, but storage buffers have to be 16-byte aligned
[StructLayout(LayoutKind.Sequential)]
public record struct TerrainTile(int Id, int X, int Y, int Z);
    
