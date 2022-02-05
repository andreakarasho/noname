using SDL2;
using Sokol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace noname
{
    static unsafe class OpenGL
    {
        private static State _state;

        public static BackendContext Setup(IntPtr windowHandle, int w, int h)
        {
            BackendContext ctx = default;
            ref var ogl = ref ctx.OpenGL;

            ogl.Context = SDL.SDL_GL_CreateContext(windowHandle);
            SDL.SDL_GL_MakeCurrent(windowHandle, ogl.Context);
            SDL.SDL_GL_SetSwapInterval(0);

            ctx.PresentCB = &Present;

            _state.SDLWindow = windowHandle;
            _state.Context = ogl.Context;

            return ctx;
        }

        public static Gfx.ContextDesc GetContext()
        {
            var ctx = new Gfx.ContextDesc();
            ctx.Gl.ForceGles2 = true;

            return ctx;
        }

        [UnmanagedCallersOnly]
        private static void Present() => SDL.SDL_GL_SwapWindow(_state.SDLWindow);


        private struct State
        {
            public IntPtr SDLWindow;
            public IntPtr Context;
        }
    }
}
