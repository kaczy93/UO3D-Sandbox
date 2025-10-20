using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SDL3.SDL;

namespace SDL3_Sandbox;

public class App : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    record struct Vertex(float x, float y, float z, float r, float g, float b, float a);

    private static Vertex[] vertices =
    [
        new(0.0f, 0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f), // top vertex
        new(-0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, 1.0f), // bottom left vertex
        new(0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 1.0f, 1.0f) // bottom right vertex
    ];


    private static uint verticesSize = (uint)(Unsafe.SizeOf<Vertex>() * vertices.Length);

    private bool runApplication = true;
    private IntPtr windowHandle;
    private IntPtr gpuDevice;
    private IntPtr cmdBuffer;
    private IntPtr swapchainTexture;
    private uint swapchainWidth;
    private uint swapchainHeight;
    private IntPtr renderPass;
    private IntPtr depthStencilTexture;

    private IntPtr vertexBuffer;
    private IntPtr transferBuffer;

    private IntPtr vertexShader;
    private IntPtr fragmentShader;

    private IntPtr graphicsPipeline;

    public void Init()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new Exception("SDL_Init failed");
        }

        windowHandle = SDL_CreateWindow("SDL3-Sandbox", 800, 600, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        gpuDevice = SDL_CreateGPUDevice(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, true, null);
        if (gpuDevice == IntPtr.Zero)
            throw new Exception("SDL_CreateGPUDevice failed");
        SDL_ClaimWindowForGPUDevice(gpuDevice, windowHandle);

        depthStencilTexture = SDL_CreateGPUTexture(
            gpuDevice,
            new SDL_GPUTextureCreateInfo
            {
                type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
                width = 800,
                height = 600,
                layer_count_or_depth = 1,
                num_levels = 1,
                sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
                format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM_S8_UINT,
                usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET
            }
        );

        SDL_GPUBufferCreateInfo vertexBufInfo = new()
        {
            size = verticesSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX
        };

        vertexBuffer = SDL_CreateGPUBuffer(gpuDevice, vertexBufInfo);

        SDL_GPUTransferBufferCreateInfo transferBufInfo = new()
        {
            size = verticesSize,
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD
        };
        transferBuffer = SDL_CreateGPUTransferBuffer(gpuDevice, transferBufInfo);

        IntPtr data = SDL_MapGPUTransferBuffer(gpuDevice, transferBuffer, false);

        unsafe
        {
            fixed (Vertex* vertexPtr = &vertices[0])
            {
                Buffer.MemoryCopy(vertexPtr, (void*)data, verticesSize, verticesSize);
            }
        }

        SDL_UnmapGPUTransferBuffer(gpuDevice, transferBuffer);
        
        var copyCmdBuffer = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var copyPass = SDL_BeginGPUCopyPass(copyCmdBuffer);

        SDL_GPUTransferBufferLocation location = new()
        {
            transfer_buffer = transferBuffer,
            offset = 0
        };

        SDL_GPUBufferRegion region = new()
        {
            buffer = vertexBuffer,
            size = verticesSize,
            offset = 0
        };

        SDL_UploadToGPUBuffer(copyPass, location, region, true);

        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(copyCmdBuffer);

        vertexShader = LoadShader("Shaders/vertex.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX);
        fragmentShader = LoadShader("Shaders/fragment.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT);

        SDL_GPUVertexBufferDescription vertexDesc = new()
        {
            slot = 0,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0,
            pitch = (uint)Unsafe.SizeOf<Vertex>()
        };

        SDL_GPUVertexAttribute[] vertexAttrs =
        [
            new()
            {
                buffer_slot = 0,
                location = 0,
                format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
                offset = 0
            },
            new()
            {
                buffer_slot = 0,
                location = 1,
                format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
                offset = sizeof(float) * 3
            }
        ];

        SDL_GPUColorTargetDescription colorTargetDesc = new()
        {
            format = SDL_GetGPUSwapchainTextureFormat(gpuDevice, windowHandle)
        };
        
        unsafe
        {
            fixed (SDL_GPUVertexAttribute* vertexAttrsPtr = &vertexAttrs[0])
            {
                SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    vertex_shader = vertexShader,
                    fragment_shader = fragmentShader,

                    primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
                    vertex_input_state = new()
                    {
                        num_vertex_buffers = 1,
                        vertex_buffer_descriptions = &vertexDesc,
                        num_vertex_attributes = 2,
                        vertex_attributes = vertexAttrsPtr
                    },
                    target_info = new()
                    {
                        num_color_targets = 1,
                        color_target_descriptions = &colorTargetDesc
                    }
                };

                graphicsPipeline = SDL_CreateGPUGraphicsPipeline(gpuDevice, pipelineInfo);
            }
        }
        
        //We don't need these anymore
        SDL_ReleaseGPUShader(gpuDevice, fragmentShader);
        SDL_ReleaseGPUShader(gpuDevice, vertexShader);
    }

    private IntPtr LoadShader(string fileName, SDL_GPUShaderStage stage)
    {
        IntPtr result;
        var vertexCode = SDL_LoadFile(fileName, out UIntPtr datasize);

        unsafe
        {
            fixed (byte* entryPoint = "main"u8)
            {
                SDL_GPUShaderCreateInfo vertexInfo = new()
                {
                    code = (byte*)vertexCode,
                    code_size = datasize,
                    entrypoint = entryPoint,
                    format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                    stage = stage,
                    num_samplers = 0,
                    num_storage_buffers = 0,
                    num_storage_textures = 0,
                    num_uniform_buffers = 0
                };

                result = SDL_CreateGPUShader(gpuDevice, vertexInfo);
            }
        }

        SDL_free(vertexCode);
        return result;
    }

    public void Run()
    {
        while (runApplication)
        {
            if (PollEvents())
                runApplication = false;

            Update();
            if (BeginDraw())
            {
                Draw();
                EndDraw();
            }
        }
    }

    private bool PollEvents()
    {
        SDL_Event evt;

        while (SDL_PollEvent(out evt))
        {
            switch ((SDL_EventType)evt.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    return true;
                default:
                    break;
            }
        }

        return false;
    }

    private bool vertexChanged = true;

    private void Update()
    {
        // if (vertexChanged)
        // {
        //     vertexChanged = false;
        // }
    }

    private bool BeginDraw()
    {
        //SHould this be here?
        cmdBuffer = SDL_AcquireGPUCommandBuffer(gpuDevice);
        SDL_WaitAndAcquireGPUSwapchainTexture(cmdBuffer, windowHandle, out swapchainTexture, out swapchainWidth,
            out swapchainHeight);
        if (swapchainTexture == IntPtr.Zero)
        {
            SDL_SubmitGPUCommandBuffer(cmdBuffer);
            return false; //Window minimized, don't draw
        }

        SDL_GPUColorTargetInfo info = new()
        {
            clear_color = new SDL_FColor { r = 240 / 255f, g = 240 / 255f, b = 240 / 255f, a = 1f },
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            texture = swapchainTexture
        };

        SDL_GPUDepthStencilTargetInfo dsInfo = new()
        {
            texture = depthStencilTexture,
            cycle = true,
            clear_depth = 0,
            clear_stencil = 0,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE
        };

        renderPass = SDL_BeginGPURenderPass(cmdBuffer, [info], 1, dsInfo);
        SDL_BindGPUGraphicsPipeline(renderPass, graphicsPipeline);
        return true;
    }

    private void Draw()
    {
        SDL_GPUBufferBinding binding = new()
        {
            buffer = vertexBuffer,
            offset = 0
        };
        
        SDL_BindGPUVertexBuffers(renderPass, 0, [binding], 1);
        
        SDL_DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
    }

    private void EndDraw()
    {
        SDL_EndGPURenderPass(renderPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);
    }

    public void Dispose()
    {
        SDL_ReleaseGPUGraphicsPipeline(gpuDevice, graphicsPipeline);
        SDL_ReleaseGPUTransferBuffer(gpuDevice, transferBuffer);
        SDL_ReleaseGPUBuffer(gpuDevice, vertexBuffer);
        SDL_DestroyGPUDevice(gpuDevice);
        SDL_DestroyWindow(windowHandle);
        SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
    }
}