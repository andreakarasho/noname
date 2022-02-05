using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using Sokol;

using static SDL2.SDL;
using SDL2;

namespace noname
{
    public unsafe static class Backend
    {
        const int WINDOW_DEFAULT_WIDTH = 640;
        const int WINDOW_DEFAULT_HEIGHT = 480;
        const string WINDOW_DEFAULT_TITLE = "App";

        private static BackendDescription _desc;


        public static void Run(in BackendDescription desc)
        {
            _desc = desc;

            // sanitize stuff
            _desc.WindowTitle = !string.IsNullOrEmpty(_desc.WindowTitle) ? _desc.WindowTitle : WINDOW_DEFAULT_TITLE;
            _desc.WindowWidth = _desc.WindowWidth > 0 ? _desc.WindowWidth : WINDOW_DEFAULT_WIDTH;
            _desc.WindowHeight = _desc.WindowHeight > 0 ? _desc.WindowHeight : WINDOW_DEFAULT_HEIGHT;

            
            SDL_SetMainReady();
            _ = SDL_Init(SDL_INIT_VIDEO);

            PresetupBackend();

            var window = SDL_CreateWindow
            (
                _desc.WindowTitle,
                _desc.WindowX > 0 ? _desc.WindowX : SDL_WINDOWPOS_CENTERED,
                _desc.WindowY > 0 ? _desc.WindowY : SDL_WINDOWPOS_CENTERED,
                _desc.WindowWidth,
                _desc.WindowHeight,
                _desc.WindowSetupFlags
            );

            var ctx = CreateContext(_desc.BackendType, window);

            if (_desc.OnInit != null)
            {
                _desc.OnInit(_desc.Userdata);
            }

            bool done = false;

            while (!done)
            {
                while (SDL_PollEvent(out var ev) > 0)
                {
                    if (ev.type == SDL_EventType.SDL_QUIT)
                    {
                        done = true;

                        break;
                    }

                    HandleEvents(ctx, ref ev);

                    if (_desc.OnEvent != null)
                    {
                        _desc.OnEvent((SDL_Event*)Unsafe.AsPointer(ref ev), _desc.Userdata);
                    }
                }

                if (done)
                {
                    break;
                }

                if (_desc.OnFrame != null)
                {
                    _desc.OnFrame(_desc.Userdata);
                }

                if (ctx.PresentCB != null)
                {
                    ctx.PresentCB();
                }
            }

            if (_desc.OnShutdown != null)
            {
                _desc.OnShutdown(desc.Userdata);
            }

            SDL_DestroyWindow(window);

            if (ctx.ShutdownCB != null)
            {
                ctx.ShutdownCB();
            }    
        }

        public static int Width => _desc.WindowWidth > 0 ? _desc.WindowWidth : 1;
        public static int Height => _desc.WindowHeight > 0 ? _desc.WindowHeight : 1;

        public static Gfx.ContextDesc GetContext()
        {
            switch (_desc.BackendType)
            {
                case BackendType.D3D11: return D3D11.GetContext();
                case BackendType.OpenGL: return OpenGL.GetContext();
            }

            return default;
        }

        private static void PresetupBackend()
        {
            if (_desc.BackendType == BackendType.Detect)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _desc.BackendType = BackendType.D3D11;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                         RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    _desc.BackendType = BackendType.OpenGL;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _desc.BackendType = BackendType.Metal;
                }
                else
                {
                    throw new PlatformNotSupportedException("OS is not supported");
                }
            }

            switch (_desc.BackendType)
            {
                case BackendType.D3D11:

                    break;

                case BackendType.OpenGL:
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_FLAGS, 0);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);

                    _desc.WindowSetupFlags |= SDL_WindowFlags.SDL_WINDOW_OPENGL;

                    break;
            }
        }

        private static BackendContext CreateContext(BackendType backend, IntPtr window)
        {
            BackendContext ctx = default;

            if (backend == BackendType.D3D11)
            {
                var info = new SDL_SysWMinfo();
                SDL_VERSION(out info.version);
                SDL_GetWindowWMInfo(window, ref info);

                if (info.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS)
                {
                    ctx = D3D11.Setup(info.info.win.window, _desc.WindowWidth, _desc.WindowHeight);
                }
            }
            else if (backend == BackendType.OpenGL)
            {
                ctx = OpenGL.Setup(window, _desc.WindowWidth, _desc.WindowHeight);
            }

            return ctx;
        }

        private static void HandleEvents(in BackendContext ctx, ref SDL_Event ev)
        {
            switch (ev.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:

                    switch (ev.window.windowEvent)
                    {
                        case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:

                            _desc.WindowWidth = ev.window.data1;
                            _desc.WindowHeight = ev.window.data2;

                            if (ctx.UpdateRenderTargetCB != null)
                            {
                                ctx.UpdateRenderTargetCB(_desc.WindowWidth, _desc.WindowHeight);
                            }

                            break;
                    }

                    break;
            }
        }

        public struct BackendDescription
        {
            public string WindowTitle;
            public int WindowX, WindowY, WindowWidth, WindowHeight;
            public SDL_WindowFlags WindowSetupFlags;

            public BackendType BackendType;

            public delegate* unmanaged<void*, void> OnInit;
            public delegate* unmanaged<void*, void> OnShutdown;
            public delegate* unmanaged<void*, void> OnFrame;
            public delegate* unmanaged<SDL_Event*, void*, void> OnEvent;

            public void* Userdata;
        }

        public enum BackendType
        {
            Detect,
            D3D11,
            OpenGL,
            Metal,
            WebGL
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BackendContext
    {
        public D3D11Context D3D11;
        public MetalContext Metal;
        public OpenGLContext OpenGL;
        public WebGLContext WebGL;

        public delegate* unmanaged<void> PresentCB;
        public delegate* unmanaged<int, int, void> UpdateRenderTargetCB;
        public delegate* unmanaged<void> ShutdownCB;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct D3D11Context
    {
        public IntPtr Device;
        public IntPtr Context;
        public IntPtr SwapChain;
        public void* RenderTargetViewCB;
        public void* DepthStencilViewCB;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MetalContext
    {

    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct OpenGLContext
    {
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WebGLContext
    {

    }
}
