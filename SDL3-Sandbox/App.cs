using System.Diagnostics;
using SDL3_Sandbox.UO;
using static SDL3.SDL;

namespace SDL3_Sandbox;

public class App : IDisposable
{
    private bool runApplication = true;
    private IntPtr windowHandle;
    private IntPtr gpuDevice;
    private UORenderer renderer;

    public void Init(ClientVersion clientVersion, string uoPath)
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new Exception("SDL_Init failed");
        }

        windowHandle = SDL_CreateWindow("SDL3-Sandbox", 1920, 1070, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        gpuDevice = SDL_CreateGPUDevice(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, true, null);
        if (gpuDevice == IntPtr.Zero)
            throw new Exception("SDL_CreateGPUDevice failed");
        SDL_ClaimWindowForGPUDevice(gpuDevice, windowHandle);
        
        renderer = new UORenderer(windowHandle, gpuDevice);
        renderer.Init(clientVersion,uoPath);
    }

    private long prevTimestamp;
    
    public void Run()
    {
        while (runApplication)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var elapsed = timestamp - prevTimestamp;
            prevTimestamp = timestamp;
            if (PollEvents(elapsed))
                runApplication = false;

            Update(elapsed);
            if (BeginDraw())
            {
                Draw();
                EndDraw();
            }
        }
    }

    private bool PollEvents(long elapsedTime)
    {
        SDL_Event evt;

        while (SDL_PollEvent(out evt))
        {
            switch ((SDL_EventType)evt.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    return true;
                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                    renderer.HandleKeyDown(elapsedTime, (SDL_Keycode)evt.key.key);
                    break;
                default:
                    break;
            }
            
        }

        return false;
    }

    private void Update(long elapsedTime)
    {
        renderer.Update();
    }

    private bool BeginDraw()
    {
        return renderer.BeginDraw();
    }

    private void Draw()
    {
       renderer.Draw();
    }

    private void EndDraw()
    {
        renderer.EndDraw();
    }

    public void Dispose()
    {
        renderer.Dispose();
        SDL_DestroyGPUDevice(gpuDevice);
        SDL_DestroyWindow(windowHandle);
        SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
    }
}