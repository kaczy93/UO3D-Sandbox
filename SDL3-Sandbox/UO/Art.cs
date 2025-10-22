using ClassicUO.Assets;
using SDL3_Sandbox.UO;
using static SDL3.SDL;

namespace ClassicUO.Renderer.Arts
{
    public sealed class Art
    {
        private readonly TextureAtlas _atlas;
        private readonly ArtLoader _artLoader;
        private readonly SpriteInfo[] _spriteInfos;
        private readonly SDL_Rect[] _realArtBounds;

        public Art(IntPtr device, ArtLoader artLoader)
        {
            _atlas = new TextureAtlas(device);
            _artLoader = artLoader;
            _spriteInfos = new SpriteInfo[_artLoader.File.Entries.Length];
            _realArtBounds = new SDL_Rect[_spriteInfos.Length];
        }

        
        public ref readonly SpriteInfo GetLand(uint idx)
            => ref Get((uint)(idx & ~0x4000));

        public ref readonly SpriteInfo GetArt(uint idx)
            => ref Get(idx + 0x4000);

        private ref readonly SpriteInfo Get(uint idx)
        {
            if (idx >= _spriteInfos.Length)
                return ref SpriteInfo.Empty;

            ref var spriteInfo = ref _spriteInfos[idx];

            if (spriteInfo.Texture == IntPtr.Zero)
            {
                var artInfo = _artLoader.GetArt(idx);

                if (artInfo.Pixels.IsEmpty && idx > 0)
                {
                    // Trying to load a texture that does not exist in the client MULs
                    // Degrading gracefully and only crash if not even the fallback ItemID exists
                    // Log.Error(
                    //     $"Texture not found for sprite: idx: {idx}; itemid: {(idx > 0x4000 ? idx - 0x4000 : '-')}"
                    // );
                    return ref Get(0); // ItemID of "UNUSED" placeholder
                }

                spriteInfo.Texture = _atlas.AddSprite(
                    artInfo.Pixels,
                    artInfo.Width,
                    artInfo.Height,
                    out spriteInfo.UV
                );

                if (idx > 0x4000)
                {
                    idx -= 0x4000;

                    var pos1 = 0;
                    int minX = artInfo.Width,
                        minY = artInfo.Height,
                        maxX = 0,
                        maxY = 0;

                    for (int y = 0; y < artInfo.Height; ++y)
                    {
                        for (int x = 0; x < artInfo.Width; ++x)
                        {
                            if (artInfo.Pixels[pos1++] != 0)
                            {
                                minX = Math.Min(minX, x);
                                maxX = Math.Max(maxX, x);
                                minY = Math.Min(minY, y);
                                maxY = Math.Max(maxY, y);
                            }
                        }
                    }

                    _realArtBounds[idx] = new SDL_Rect{
                        x = minX, 
                        y = minY, 
                        w = maxX - minX, 
                        h = maxY - minY
                    };
                }
                
            }

            return ref spriteInfo;
        }
        
        public SDL_Rect GetRealArtBounds(uint idx) =>
            idx < 0 || idx >= _realArtBounds.Length
                ? default
                : _realArtBounds[idx];
    }
}
