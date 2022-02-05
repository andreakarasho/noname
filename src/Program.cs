using System;
using System.Runtime.InteropServices;
using System.Threading;
using SDL2;
using Sokol;

namespace noname
{
    unsafe class Program
    {
        static void Main(string[] args)
        {
            Backend.Run(new Backend.BackendDescription()
            {
                BackendType = Backend.BackendType.D3D11,
                WindowTitle = "backend d3d11 test",
                OnInit = &OnInit,
                OnShutdown = &OnShutdown,
                OnFrame = &OnFrame,
                OnEvent = &OnEvent,
                WindowSetupFlags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN |
                                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE |
                                SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS,
            });
        }


        [UnmanagedCallersOnly]
        private static void OnInit(void* userdata)
        {
            Gfx.Setup(new Gfx.Desc()
            {
                Context = Backend.GetContext()
            });

            /* a vertex buffer with 3 vertices */
            ReadOnlySpan<float> vertices = stackalloc float[] 
            {
                // positions            // colors
                 0.0f,  0.5f, 0.5f,     1.0f, 0.0f, 0.0f, 1.0f,
                 0.5f, -0.5f, 0.5f,     0.0f, 1.0f, 0.0f, 1.0f,
                -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 1.0f, 1.0f
            };

            State.Bindings.VertexBuffers[0] = Gfx.MakeBuffer(
                vertices,
                "triangle-vertices");

            Gfx.Shader shd = Gfx.MakeShader(GetShaderDesc());
            Gfx.PipelineDesc pipelineDesc = new()
            {
                Shader = shd,
                Label = "triangle-pipeline",
            };
            pipelineDesc.Layout.Attrs[0].Format = Gfx.VertexFormat.Float3;
            pipelineDesc.Layout.Attrs[1].Format = Gfx.VertexFormat.Float4;
            State.Pipeline = Gfx.MakePipeline(pipelineDesc);



            var dbgText = new DebugText.Desc();
            ref var d = ref dbgText.Fonts[0];
            d = DebugText.FontC64();
            DebugText.Setup(dbgText);
        }

        [UnmanagedCallersOnly]
        private static void OnShutdown(void* userdata)
        {

        }

        [UnmanagedCallersOnly]
        private static void OnFrame(void* userdata)
        {
            string text = "SOKOL + SDL TEST";
            DebugText.Font(0);
            DebugText.Canvas(Backend.Width, Backend.Height);
            DebugText.Origin(0f, 0f);
            DebugText.Pos((Backend.Width / 8f) * 0.5f, (Backend.Height / 8f) * 0.5f);
            DebugText.Puts(text);

            Gfx.BeginDefaultPass(default, Backend.Width, Backend.Height);
            Gfx.ApplyPipeline(State.Pipeline);
            Gfx.ApplyBindings(State.Bindings);
            Gfx.Draw(0, 3, 1);

            DebugText.Draw();

            Gfx.EndPass();
            Gfx.Commit();

            Thread.Sleep(1);
        }

        [UnmanagedCallersOnly]
        private static void OnEvent(SDL.SDL_Event* ev, void* userdata)
        {

        }

        static Gfx.ShaderDesc GetShaderDesc()
        {
            Gfx.ShaderDesc desc = default;
            switch (Gfx.QueryBackend())
            {
                case Gfx.Backend.D3d11:
                    desc.Attrs[0].SemName = "POS";
                    desc.Attrs[1].SemName = "COLOR";
                    desc.Vs.Source = @"
                struct vs_in {
                  float4 pos: POS;
                  float4 color: COLOR;
                };
                struct vs_out {
                  float4 color: COLOR0;
                  float4 pos: SV_Position;
                };
                vs_out main(vs_in inp) {
                  vs_out outp;
                  outp.pos = inp.pos;
                  outp.color = inp.color;
                  return outp;
                }";
                    desc.Fs.Source = @"
                float4 main(float4 color: COLOR0): SV_Target0 {
                  return color;
                }";
                    break;
                case Gfx.Backend.Glcore33:
                    desc.Attrs[0].Name = "position";
                    desc.Attrs[1].Name = "color0";
                    desc.Vs.Source = @"
# version 330
                in vec4 position;
                in vec4 color0;
                out vec4 color;
                void main() {
                  gl_Position = position;
                  color = color0;
                }";

                    desc.Fs.Source = @"
# version 330
                in vec4 color;
                out vec4 frag_color;
                void main() {
                  frag_color = color;
                }";
                    break;
                case Gfx.Backend.MetalMacos:
                    desc.Vs.Source = @"
# include <metal_stdlib>
                using namespace metal;
                struct vs_in {
                  float4 position [[attribute(0)]];
                  float4 color [[attribute(1)]];
                };
                struct vs_out {
                  float4 position [[position]];
                  float4 color;
                };
                vertex vs_out _main(vs_in inp [[stage_in]]) {
                  vs_out outp;
                  outp.position = inp.position;
                  outp.color = inp.color;
                  return outp;
                }";
                    desc.Fs.Source = @"
# include <metal_stdlib>
                using namespace metal;
                fragment float4 _main(float4 color [[stage_in]]) {
                   return color;
                };";
                    break;
            }
            return desc;
        }

        static class State
        {
            public static Gfx.Pipeline Pipeline;
            public static Gfx.Bindings Bindings;
        }
    }
}