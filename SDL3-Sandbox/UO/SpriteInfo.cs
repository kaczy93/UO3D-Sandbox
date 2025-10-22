using static SDL3.SDL;

namespace ClassicUO.Renderer
{
    public struct SpriteInfo
    {
        public IntPtr Texture;
        public SDL_Rect UV;
        public SDL_Point Center;

        public static readonly SpriteInfo Empty = new SpriteInfo { Texture = IntPtr.Zero };
    }
}