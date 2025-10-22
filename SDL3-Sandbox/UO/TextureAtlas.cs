using SDL3_Sandbox.UO;
using StbRectPackSharp;
using static SDL3.SDL;

namespace ClassicUO.Renderer
{
    public class TextureAtlas : IDisposable
    {
        public const int PREDEFINED_SIZE = 4096;
        
        private readonly uint _width, _height;
        private readonly SDL_PixelFormat _format = SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888;
        private readonly IntPtr gpuDevice;
        private readonly IntPtr transferBuffer;
        private readonly List<IntPtr> _textureList;
        private Packer _packer;

        public TextureAtlas(IntPtr device)
        {
            gpuDevice = device;
            // _width = width;
            // _height = height;
            _width = PREDEFINED_SIZE;
            _height = PREDEFINED_SIZE;

            _textureList = new List<IntPtr>();
            transferBuffer = SDL_CreateGPUTransferBuffer(gpuDevice, new SDL_GPUTransferBufferCreateInfo()
            {
                size = _width * _height * 4,
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD
            });
        }

        public int TexturesCount => _textureList.Count;

        public unsafe IntPtr AddSprite(
            ReadOnlySpan<uint> pixels,
            int width,
            int height,
            out SDL_Rect spriteBounds
        )
        {
            var index = _textureList.Count - 1;

            if (index < 0)
            {
                index = 0;
                CreateNewTexture2D();
            }

            while (!_packer.PackRect(width, height, out spriteBounds))
            {
                CreateNewTexture2D();
                index = _textureList.Count - 1;
            }

            IntPtr texture = _textureList[index];

            UploadTextureData2D(texture, pixels, spriteBounds);

            return texture;
        }

        private void CreateNewTexture2D()
        {
            Log.Trace($"creating texture: {_width}x{_height} {_format}");
            IntPtr texture = SDL_CreateGPUTexture(gpuDevice, new SDL_GPUTextureCreateInfo()
            {
                format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
                height = _height,
                width = _width,
                num_levels = 1,
                layer_count_or_depth = 1,
                sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
                type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
                usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER

            });
            _textureList.Add(texture);

            _packer?.Dispose();
            _packer = new Packer((int)_width, (int)_height);
        }

        private unsafe void UploadTextureData2D(IntPtr texture, ReadOnlySpan<uint> pixels, SDL_Rect rect)
        {
            var texData = SDL_MapGPUTransferBuffer(gpuDevice, transferBuffer, false);

            fixed (uint* pixelsPtr = pixels)
            {
                Buffer.MemoryCopy(pixelsPtr, (void*)texData, pixels.Length * 4, pixels.Length * 4);
            }

            SDL_UnmapGPUTransferBuffer(gpuDevice, transferBuffer);

            var cmdBuffer = SDL_AcquireGPUCommandBuffer(gpuDevice);
            var copyPass = SDL_BeginGPUCopyPass(cmdBuffer);
            
            SDL_UploadToGPUTexture(copyPass, 
                new SDL_GPUTextureTransferInfo
                {
                    transfer_buffer = transferBuffer,
                    offset = 0
                },
                new SDL_GPUTextureRegion
                {
                    texture = texture,
                    x = (uint)rect.x,
                    y = (uint)rect.y,
                    w = (uint)rect.w,
                    h = (uint)rect.h,
                    d = 1
                },
                false);
            
            SDL_EndGPUCopyPass(copyPass);
            SDL_SubmitGPUCommandBuffer(cmdBuffer);
        }
        
        public void Dispose()
        {
            foreach (IntPtr texture in _textureList)
            {
                SDL_ReleaseGPUTexture(gpuDevice, texture);
            }
            SDL_ReleaseGPUTransferBuffer(gpuDevice, transferBuffer);
            _packer.Dispose();
            _textureList.Clear();
        }
    }
}
