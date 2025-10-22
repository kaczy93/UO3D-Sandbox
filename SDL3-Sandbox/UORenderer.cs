using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CentrED;
using ClassicUO.Assets;
using ClassicUO.Renderer.Arts;
using ClassicUO.Renderer.Texmaps;
using SDL3_Sandbox.UO;
using static SDL3.SDL;

namespace SDL3_Sandbox;

public class UORenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    record struct Vertex(
        float x, float y, float z, //POS
        float nx, float ny, float nz, //Normals
        float u, float v); //TextureUV

    private IntPtr windowHandle;
    private IntPtr gpuDevice;

    private UOFileManager manager;
    private Art art;
    private Texmap texmap;

    private IntPtr terrainComputeStage1;
    private IntPtr terrainComputeStage2;
    private IntPtr terrainComputeStage3;
    
    private IntPtr cmdBuffer;
    private IntPtr swapchainTexture;
    private uint swapchainWidth;
    private uint swapchainHeight;
    private IntPtr depthStencilTexture;

    private IntPtr computeInputBuffer;
    private IntPtr texInfoBuffer;
    private IntPtr terrainMeshBuffer;
    
    private IntPtr artSampler;
    private IntPtr texSampler;
    
    private IntPtr vertexBuffer;

    private IntPtr graphicsPipeline;
    private IntPtr renderPass;

    private Camera camera;

    public UORenderer(IntPtr windowHandle, IntPtr gpuDevice)
    {
        this.windowHandle = windowHandle;
        this.gpuDevice = gpuDevice;
    }

    public void Init()
    {
        camera = new Camera();
        Console.WriteLine("Loading UO Assets");
        int maxLandId = 10; //We only need that much for testing
        manager = new UOFileManager(ClientVersion.CV_706400, "/home/kaczy/nel/Ultima Online Classic_7_0_95_0_modified");
        manager.Load();
        art = new Art(gpuDevice, manager.Arts);
        art.PreLoadLand(maxLandId);
        texmap = new Texmap(gpuDevice, manager.Texmaps);
        texmap.PreLoad(maxLandId);
        
        Console.WriteLine("Creating shaders, samplers, buffers");
        //LoadShaders
        var vertexShader = LoadShader("Shaders/TerrainVertex.spv", new()
        {
            format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
            num_uniform_buffers = 1,
            num_storage_buffers = 3
        });
        var fragmentShader = LoadShader("Shaders/TerrainFragment.spv", new()
        {
            format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
            num_samplers = 2
        });
        
        //Sampler
        artSampler = SDL_CreateGPUSampler(gpuDevice, new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        });
        texSampler = SDL_CreateGPUSampler(gpuDevice, new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        });
        
        //Create pipelines
        terrainComputeStage1 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute1Pos.spv",
            new() {
                num_readonly_storage_buffers = 1,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        terrainComputeStage2 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute2Normals.spv",
            new() {
                num_readonly_storage_buffers = 1,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        var terrainComputeStage3 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute3Vertices.spv",
            new() {
                num_readonly_storage_buffers = 3,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        
        //Create depth stencil
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
        
        //Prepare buffers
        const int INPUT_LENGTH = 64;

        var inputData = new TerrainTile[INPUT_LENGTH];
        for (int i = 0; i < INPUT_LENGTH; i++)
        {
            inputData[i] = new TerrainTile(3 + Random.Shared.Next(4), i % 8, i / 8, Random.Shared.Next(5));
        }

        var computeInputSize = (uint)(Unsafe.SizeOf<TerrainTile>() * INPUT_LENGTH);
        computeInputBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = computeInputSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_READ
        });

        var texInfoBufferSize = (uint)(sizeof(float) * 4 * 0x4000 * 2);
        texInfoBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = texInfoBufferSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_READ
        });

        var terrainMeshSize = (uint)(Unsafe.SizeOf<Vertex>() * INPUT_LENGTH);
        terrainMeshBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = terrainMeshSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ |
                    SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_READ |
                    SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_WRITE
        });

        var verticesSize = terrainMeshSize * 6; //each tile has 6 vertices
        vertexBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = verticesSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX |
                    SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_WRITE
        });
        
        //Copy
        var transferBuffer = SDL_CreateGPUTransferBuffer(gpuDevice, new SDL_GPUTransferBufferCreateInfo
            {
                size = Math.Max(computeInputSize, texInfoBufferSize),
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD
            }
        );

        Console.WriteLine("Preparing terrain mesh input");
        IntPtr transferBufferPtr = SDL_MapGPUTransferBuffer(gpuDevice, transferBuffer, false);
        unsafe
        {
            fixed (TerrainTile* inputPtr = &inputData[0])
            {
                Buffer.MemoryCopy(inputPtr, (void*)transferBufferPtr, computeInputSize, computeInputSize);
            }
        }
        SDL_UnmapGPUTransferBuffer(gpuDevice, transferBuffer);
        
        var copyCmdBuffer1 = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var copyPass = SDL_BeginGPUCopyPass(copyCmdBuffer1);

        SDL_GPUTransferBufferLocation location = new()
        {
            transfer_buffer = transferBuffer,
            offset = 0
        };

        SDL_GPUBufferRegion region = new()
        {
            buffer = computeInputBuffer,
            size = computeInputSize,
            offset = 0
        };

        SDL_UploadToGPUBuffer(copyPass, location, region, true);
        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(copyCmdBuffer1);
        
        Console.WriteLine("Preparing tex info buffer");
        transferBufferPtr = SDL_MapGPUTransferBuffer(gpuDevice, transferBuffer, false);
        float atlasXY = 16384f;
        unsafe
        {
            float* fTxBufPtr = (float*)transferBufferPtr;
            for (uint  i = 0; i < maxLandId; i++)
            {
                var ti = i * 8;
                var a = art.GetArt(i);
                if (a.Texture != IntPtr.Zero)
                {
                    fTxBufPtr[ti] = a.UV.x / atlasXY;
                    fTxBufPtr[ti + 1] = a.UV.y / atlasXY;
                    fTxBufPtr[ti + 2] = a.UV.w / atlasXY;
                    fTxBufPtr[ti + 3] = a.UV.w / atlasXY;
                }

                var t = texmap.GetTexmap(i);
                if (t.Texture != IntPtr.Zero)
                {
                    fTxBufPtr[ti + 4] = 1 + t.UV.x / atlasXY;
                    fTxBufPtr[ti + 5] = 1 + t.UV.y / atlasXY;
                    fTxBufPtr[ti + 6] = t.UV.w / atlasXY;
                    fTxBufPtr[ti + 7] = t.UV.h / atlasXY;
                }
            }
        }
        SDL_UnmapGPUTransferBuffer(gpuDevice, transferBuffer);
        
        var copyCmdBuffer2 = SDL_AcquireGPUCommandBuffer(gpuDevice);
        copyPass = SDL_BeginGPUCopyPass(copyCmdBuffer2);

        location = new()
        {
            transfer_buffer = transferBuffer,
            offset = 0
        };

        region = new()
        {
            buffer = texInfoBuffer,
            size = texInfoBufferSize,
            offset = 0
        };

        SDL_UploadToGPUBuffer(copyPass, location, region, true);
        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(copyCmdBuffer2);
        
        SDL_ReleaseGPUTransferBuffer(gpuDevice, transferBuffer);
        
        Console.WriteLine("Computing terrain mesh");
        //Stage 1
        var computeCmdBuffer = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var computePass1 = SDL_BeginGPUComputePass(
            computeCmdBuffer,
            [],
            0,
            [
                new()
                {
                    buffer = terrainMeshBuffer,
                }
            ],
            1);
        
        SDL_BindGPUComputePipeline(computePass1, terrainComputeStage1);
        SDL_BindGPUComputeStorageBuffers(computePass1, 0, [computeInputBuffer], 1);
        SDL_DispatchGPUCompute(computePass1, 1, 1, 1);
        SDL_EndGPUComputePass(computePass1);
        
        //Stage2
        var computePass2 = SDL_BeginGPUComputePass(
            computeCmdBuffer,
            [],
            0,
            [
                new()
                {
                    buffer = terrainMeshBuffer,
                }
            ],
            1);
        
        SDL_BindGPUComputePipeline(computePass2, terrainComputeStage2);
        SDL_BindGPUComputeStorageBuffers(computePass2, 0, [computeInputBuffer], 1);
        SDL_DispatchGPUCompute(computePass2, 1, 1, 1);
        SDL_EndGPUComputePass(computePass2);
        
        //Stage3
        var computePass3 = SDL_BeginGPUComputePass(
            computeCmdBuffer,
            [],
            0,
            [
                new ()
                {
                    buffer = vertexBuffer,
                },
                // new ()
                // {
                //     buffer = indexBuffer,
                // }
            ],
            1);
        
        SDL_BindGPUComputePipeline(computePass3, terrainComputeStage3);
        SDL_BindGPUComputeStorageBuffers(computePass3, 0, [computeInputBuffer, terrainMeshBuffer, texInfoBuffer], 3);
        SDL_DispatchGPUCompute(computePass3, 1, 1, 1);
        SDL_EndGPUComputePass(computePass3);
        
        SDL_SubmitGPUCommandBuffer(computeCmdBuffer);
        
        Console.WriteLine("Preparing graphics pipeline");
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
                format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
                offset = sizeof(float) * 3
            },
            new()
            {
                buffer_slot = 0,
                location = 2,
                format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
                offset = sizeof(float) * 6
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
                        num_vertex_attributes = (uint)vertexAttrs.Length,
                        vertex_attributes = vertexAttrsPtr
                    },
                    target_info = new()
                    {
                        num_color_targets = 1,
                        color_target_descriptions = &colorTargetDesc,
                        has_depth_stencil_target = true,
                        depth_stencil_format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM_S8_UINT,
                    }
                };

                graphicsPipeline = SDL_CreateGPUGraphicsPipeline(gpuDevice, pipelineInfo);
            }
        }
        
        //We don't need these anymore
        SDL_ReleaseGPUShader(gpuDevice, fragmentShader);
        SDL_ReleaseGPUShader(gpuDevice, vertexShader);
    }

    public bool NewFrame()
    {
        //Should this be here?
        cmdBuffer = SDL_AcquireGPUCommandBuffer(gpuDevice);
        SDL_WaitAndAcquireGPUSwapchainTexture(cmdBuffer, windowHandle, out swapchainTexture, out swapchainWidth,
            out swapchainHeight);
        if (swapchainTexture == IntPtr.Zero)
        {
            SDL_SubmitGPUCommandBuffer(cmdBuffer);
            return false; //Window minimized, don't draw
        }

        camera.ScreenSize = new SDL_Rect()
        {
            x = 0,
            y = 0,
            w = (int)swapchainWidth,
            h = (int)swapchainHeight
        };

        return true;
    }

    public void Update()
    {
        camera.Update();
    }

    public bool BeginDraw()
    {
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
        return true;
    }

    public void Draw()
    {
        SDL_BindGPUGraphicsPipeline(renderPass, graphicsPipeline);
        
        SDL_GPUBufferBinding binding = new()
        {
            buffer = vertexBuffer,
            offset = 0
        };
        SDL_BindGPUVertexBuffers(renderPass, 0, [binding], 1);

        var x = camera.WorldViewProj;
        float[] mat4 =
        {
            x.M11, x.M12, x.M13, x.M14,
            x.M21, x.M22, x.M23, x.M24,
            x.M31, x.M32, x.M33, x.M34,
            x.M41, x.M42, x.M43, x.M44
        };

        unsafe
        {
            fixed (float* pMat4 = mat4)
            {
                SDL_PushGPUVertexUniformData(cmdBuffer, 0, (IntPtr)pMat4, sizeof(float) * 16);
            }
        }
        SDL_BindGPUVertexStorageBuffers(renderPass, 0, [computeInputBuffer, terrainMeshBuffer, texInfoBuffer], 3);

        var artTex = art.GetArt(3).Texture;
        if (artTex == IntPtr.Zero)
        {
            Console.WriteLine("Invalid art texture");
        }

        var texTex = texmap.GetTexmap(3).Texture;
        if (texTex == IntPtr.Zero)
        {
            Console.WriteLine("Invalid texmap");
        }
        SDL_BindGPUFragmentSamplers(renderPass, 0, [
            new SDL_GPUTextureSamplerBinding(){sampler = artSampler, texture = artTex},
            new SDL_GPUTextureSamplerBinding(){sampler = texSampler, texture = texTex}
        ], 2);
        
        SDL_DrawGPUPrimitives(renderPass, 64 * 6, 64 * 2, 0, 0);
        
        // SDL_DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
    }        

    public void EndDraw()
    {
        SDL_EndGPURenderPass(renderPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);
    }

    public void Dispose()
    {
        SDL_ReleaseGPUComputePipeline(gpuDevice, terrainComputeStage1);
        SDL_ReleaseGPUComputePipeline(gpuDevice, terrainComputeStage2);
        SDL_ReleaseGPUComputePipeline(gpuDevice, terrainComputeStage3);
        SDL_ReleaseGPUGraphicsPipeline(gpuDevice, graphicsPipeline);
        SDL_ReleaseGPUBuffer(gpuDevice, vertexBuffer);
    }
    
    IntPtr CreateComputePipelineFromShader(string shaderFilename, SDL_GPUComputePipelineCreateInfo createInfo) {
        var code = SDL_LoadFile(shaderFilename, out var codeSize);
        if (code == IntPtr.Zero)
        {
            SDL_Log($"Failed to load compute shader from disk! {shaderFilename}");
            return IntPtr.Zero;
        }
        
        unsafe
        {
            fixed (byte* entryPoint = "main"u8)
            {
                createInfo.entrypoint = entryPoint;
                createInfo.code_size = codeSize;
                createInfo.code = (byte*)code;
                createInfo.format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV; //TODO: Support other formats
            }
        }
        
        var pipeline = SDL_CreateGPUComputePipeline(gpuDevice, createInfo);
        if (pipeline == IntPtr.Zero)
        {
            SDL_Log("Failed to create compute pipeline!");
            SDL_free(code);
            return  IntPtr.Zero;
        }

        SDL_free(code);
        return pipeline;
    }

    private IntPtr LoadShader(string fileName, SDL_GPUShaderCreateInfo info)
    {
        IntPtr result;
        var vertexCode = SDL_LoadFile(fileName, out UIntPtr codeSize);

        unsafe
        {
            fixed (byte* entryPoint = "main"u8)
            {
                info.code_size = codeSize;
                info.code = (byte*)vertexCode;
                info.entrypoint = entryPoint;

                result = SDL_CreateGPUShader(gpuDevice, info);
            }
        }

        SDL_free(vertexCode);
        return result;
    }
}