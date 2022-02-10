#if DEBUG || BFLAT
#define SOKOL_D3D11
#endif

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using bottlenoselabs;
using SDL2;

using static SDL2.SDL;
using static bottlenoselabs.sokol;

namespace noname
{
    public unsafe static class Backend
    {
        const int WINDOW_DEFAULT_WIDTH = 640;
        const int WINDOW_DEFAULT_HEIGHT = 480;
        const string WINDOW_DEFAULT_TITLE = "App";

        private static BackendDescription _desc;


        public static IntPtr Window { get; private set; }
        public static int Width => _desc.WindowWidth > 0 ? _desc.WindowWidth : 1;
        public static int Height => _desc.WindowHeight > 0 ? _desc.WindowHeight : 1;


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

            Window = SDL_CreateWindow
            (
                _desc.WindowTitle,
                _desc.WindowX > 0 ? _desc.WindowX : SDL_WINDOWPOS_CENTERED,
                _desc.WindowY > 0 ? _desc.WindowY : SDL_WINDOWPOS_CENTERED,
                _desc.WindowWidth,
                _desc.WindowHeight,
                _desc.WindowSetupFlags
            );

            var ctx = CreateContext(Window);

            SDL.SDL_SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");

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

            if (ctx.ShutdownCB != null)
            {
                ctx.ShutdownCB();
            }

            SDL_DestroyWindow(Window);
            Window = IntPtr.Zero;
        }

        
        public static sg_context_desc GetContext()
        {
#if SOKOL_D3D11
            return D3D11.GetContext();
#elif SOKOL_GLES2 || SOKOL_GLES3 || SOKOL_GLCORE33
            return OpenGL.GetContext();
#else
            throw new NotImplementedException();
#endif
        }

        private static void PresetupBackend()
        {
#if SOKOL_D3D11

#elif SOKOL_GLES2
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_ES);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_FLAGS, 0);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 2);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 0);

            _desc.WindowSetupFlags |= SDL_WindowFlags.SDL_WINDOW_OPENGL;
#elif SOKOL_GLES3
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_ES);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_FLAGS, 0);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 0);

            _desc.WindowSetupFlags |= SDL_WindowFlags.SDL_WINDOW_OPENGL;
#elif SOKOL_GLCORE33
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_FLAGS, 0);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);

            _desc.WindowSetupFlags |= SDL_WindowFlags.SDL_WINDOW_OPENGL;
#else
            throw new NotImplementedException();
#endif
        }

        private static BackendContext CreateContext( IntPtr window)
        {
#if SOKOL_D3D11
            var info = new SDL_SysWMinfo();
            SDL_VERSION(out info.version);
            SDL_GetWindowWMInfo(window, ref info);

            if (info.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS)
            {
                return D3D11.Setup(info.info.win.window, _desc.WindowWidth, _desc.WindowHeight);
            }

            throw new NotImplementedException();
#elif SOKOL_GLES2 || SOKOL_GLES3 || SOKOL_GLCORE33
            return OpenGL.Setup(window, _desc.WindowWidth, _desc.WindowHeight);
#else
            throw new NotImplementedException();
#endif  
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
            public void* Userdata;

            public delegate* unmanaged<void*, void> OnInit;
            public delegate* unmanaged<void*, void> OnShutdown;
            public delegate* unmanaged<void*, void> OnFrame;
            public delegate* unmanaged<SDL_Event*, void*, void> OnEvent;
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
