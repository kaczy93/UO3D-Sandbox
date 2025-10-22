using ClassicUO.Assets;

namespace ClassicUO.Renderer.Texmaps
{
    public sealed class Texmap
    {
        private readonly TextureAtlas _atlas;
        private readonly TexmapsLoader _texmapsLoader;
        private readonly SpriteInfo[] _spriteInfos;

        public Texmap(IntPtr device, TexmapsLoader texmapsLoader)
        {
            _atlas = new TextureAtlas(device);
            _texmapsLoader = texmapsLoader;
            _spriteInfos = new SpriteInfo[texmapsLoader.File.Entries.Length];
        }

        public void PreLoad(int limit = 0x4000)
        {
            Console.WriteLine("Preloading lann texmaps");
            for (uint i = 0; i < limit; i++)
            {
                SpriteInfo info = GetTexmap(i);
                if (info.Texture != IntPtr.Zero)
                {
                    // Console.WriteLine($"Loaded {i}");
                }
            }
        }

        public ref readonly SpriteInfo GetTexmap(uint idx)
        {
            if (idx >= _spriteInfos.Length)
                return ref SpriteInfo.Empty;

            ref var spriteInfo = ref _spriteInfos[idx];

            if (spriteInfo.Texture == IntPtr.Zero)
            {
                var texmapInfo = _texmapsLoader.GetTexmap(idx);
                if (!texmapInfo.Pixels.IsEmpty)
                {
                    spriteInfo.Texture = _atlas.AddSprite(
                        texmapInfo.Pixels,
                        texmapInfo.Width,
                        texmapInfo.Height,
                        out spriteInfo.UV
                    );
                }
            }

            return ref spriteInfo;
        }
    }
}