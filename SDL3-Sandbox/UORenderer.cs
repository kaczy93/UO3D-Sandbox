using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    
    private IntPtr cmdBuffer;
    private IntPtr swapchainTexture;
    private uint swapchainWidth;
    private uint swapchainHeight;
    private IntPtr depthStencilTexture;

    private IntPtr computeInputBuffer;
    private IntPtr terrainMeshBuffer;
    
    private IntPtr myTexture;
    private IntPtr sampler;
    
    private IntPtr vertexBuffer;
    private IntPtr indexBuffer;

    private IntPtr graphicsPipeline;
    private IntPtr renderPass;

    public UORenderer(IntPtr windowHandle, IntPtr gpuDevice)
    {
        this.windowHandle = windowHandle;
        this.gpuDevice = gpuDevice;
    }

    public void Init()
    {
        var vertexShader = LoadShader("Shaders/TerrainVertex.spv", new()
        {
            format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
            num_uniform_buffers = 1,
            num_storage_buffers = 2
        });
        var fragmentShader = LoadShader("Shaders/TerrainFragment.spv", new()
        {
            format=SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
            num_samplers = 1
        });
        
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
            inputData[i] = new TerrainTile(3, (i % 8), (i / 8), Random.Shared.Next(5));
        }

        var computeInputSize = (uint)(Unsafe.SizeOf<TerrainTile>() * INPUT_LENGTH);
        computeInputBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = computeInputSize,
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

        var verticesSize = terrainMeshSize * 4; //each tile has 4 vertices
        vertexBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = verticesSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX |
                    SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_WRITE
        });

        var indexSize = (uint)(sizeof(uint) * 6 * INPUT_LENGTH);
        indexBuffer = SDL_CreateGPUBuffer(gpuDevice, new SDL_GPUBufferCreateInfo()
        {
            size = indexSize,
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX |
                    SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_WRITE
        });
        
        //Copy input for compute
        var transferBuffer = SDL_CreateGPUTransferBuffer(gpuDevice, new SDL_GPUTransferBufferCreateInfo
            {
                size = computeInputSize,
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD
            }
        );

        IntPtr transferBufferPtr = SDL_MapGPUTransferBuffer(gpuDevice, transferBuffer, false);

        unsafe
        {
            fixed (TerrainTile* inputPtr = &inputData[0])
            {
                Buffer.MemoryCopy(inputPtr, (void*)transferBufferPtr, computeInputSize, computeInputSize);
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
            buffer = computeInputBuffer,
            size = computeInputSize,
            offset = 0
        };

        SDL_UploadToGPUBuffer(copyPass, location, region, true);

        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(copyCmdBuffer);
        
        SDL_ReleaseGPUTransferBuffer(gpuDevice, transferBuffer);
        
        //Upload texture
        sampler = SDL_CreateGPUSampler(gpuDevice, new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        });
        
        unsafe
        {
            var imageData = SDL_LoadBMP("Texture.bmp");
            uint imageBytes = (uint)(imageData->w * imageData->h * 4);
            myTexture = SDL_CreateGPUTexture(gpuDevice, new SDL_GPUTextureCreateInfo()
            {
                type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
                format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
                width = (uint)imageData->w,
                height = (uint)imageData->h,
                layer_count_or_depth = 1,
                num_levels = 1,
                usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
            });

            var texTransferBuffer = SDL_CreateGPUTransferBuffer(gpuDevice, 
                new SDL_GPUTransferBufferCreateInfo
                {
                size = imageBytes,
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD
                }
            );

            var texData = SDL_MapGPUTransferBuffer(gpuDevice, texTransferBuffer, false);
            
            Buffer.MemoryCopy(imageData, (void*)texData, imageBytes, imageBytes);
            
            SDL_UnmapGPUTransferBuffer(gpuDevice, texTransferBuffer);

            var texCmdBuf = SDL_AcquireGPUCommandBuffer(gpuDevice);
            var texCopyPass = SDL_BeginGPUCopyPass(texCmdBuf);
            
            SDL_UploadToGPUTexture(texCopyPass, 
                new SDL_GPUTextureTransferInfo
                {
                    transfer_buffer = texTransferBuffer,
                    offset = 0
                },
                new SDL_GPUTextureRegion
                {
                    texture = myTexture,
                    w = (uint)imageData->w,
                    h = (uint)imageData->h,
                    d = 1
                },
                false);
            
            SDL_EndGPUCopyPass(texCopyPass);
            SDL_SubmitGPUCommandBuffer(texCmdBuf);
            
            SDL_ReleaseGPUTransferBuffer(gpuDevice, texTransferBuffer);
            SDL_DestroySurface((IntPtr)imageData);
        }
        
        //Compute terrain
        
        //Stage 1
        Console.WriteLine("Compute stage 1");
        var terrainComputeStage1 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute1Pos.spv",
            new() {
                num_readonly_storage_buffers = 1,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        
        var localCmdBuffer1 = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var computePass1 = SDL_BeginGPUComputePass(
            localCmdBuffer1,
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
        SDL_SubmitGPUCommandBuffer(localCmdBuffer1);
        SDL_ReleaseGPUComputePipeline(gpuDevice, computePass1);
        
        //Stage2
        Console.WriteLine("Compute stage 2");
        var terrainComputeStage2 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute2Normals.spv",
            new() {
                num_readonly_storage_buffers = 1,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        
        var localCmdBuffer2 = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var computePass2 = SDL_BeginGPUComputePass(
            localCmdBuffer2,
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
        SDL_SubmitGPUCommandBuffer(localCmdBuffer2);
        SDL_ReleaseGPUComputePipeline(gpuDevice, computePass2);
        
        //Stage3
        Console.WriteLine("Compute stage 3");
        var terrainComputeStage3 = CreateComputePipelineFromShader(
            "Shaders/TerrainCompute3Vertices.spv",
            new() {
                num_readonly_storage_buffers = 2,
                num_readwrite_storage_buffers = 1,
                threadcount_x = 64,
                threadcount_y = 1,
                threadcount_z = 1,
            }
        );
        
        var localCmdBuffer3 = SDL_AcquireGPUCommandBuffer(gpuDevice);
        var computePass3 = SDL_BeginGPUComputePass(
            localCmdBuffer3,
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
        SDL_BindGPUComputeStorageBuffers(computePass3, 0, [computeInputBuffer, terrainMeshBuffer], 2);
        SDL_DispatchGPUCompute(computePass3, 1, 1, 1);
        
        SDL_EndGPUComputePass(computePass3);
        SDL_SubmitGPUCommandBuffer(localCmdBuffer3);
        SDL_ReleaseGPUComputePipeline(gpuDevice, computePass3);
        
        //Graphics pipeline
        
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

        return true;
    }

    public void Update()
    {
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
        // SDL_BindGPUIndexBuffer(renderPass, new SDL_GPUBufferBinding()
        // {
        //     buffer = indexBuffer,
        //     offset = 0
        // }, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT);
        
        float[] mat4 =
        {
            0.01f, 0, 0, 0,
            0, 0.01f, 0, 0,
            0, 0, 0.1f, 0,
            0, 0, 0, 1f
        };

        unsafe
        {
            fixed (float* pMat4 = mat4)
            {
                SDL_PushGPUVertexUniformData(cmdBuffer, 0, (IntPtr)pMat4, sizeof(float) * 16);
            }
        }
        SDL_BindGPUVertexStorageBuffers(renderPass, 0, [computeInputBuffer, terrainMeshBuffer], 2);
        
        SDL_BindGPUFragmentSamplers(renderPass, 0, [new SDL_GPUTextureSamplerBinding(){sampler = sampler, texture = myTexture}], 1);
        
        SDL_DrawGPUPrimitives(renderPass, 64 * 6, 64*2, 0, 0);
        
        // SDL_DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
    }

    public void EndDraw()
    {
        SDL_EndGPURenderPass(renderPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);
    }

    public void Dispose()
    {
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