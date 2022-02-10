using System;
using System.Runtime.InteropServices;
using System.Threading;
using SDL2;
using noname;

using static bottlenoselabs.sokol;
using static bottlenoselabs.imgui;
using System.Runtime.CompilerServices;

unsafe
{    
    Backend.Run(new Backend.BackendDescription()
    {
        WindowTitle = "backend test",
        OnInit = &OnInit,
        OnShutdown = &OnShutdown,
        OnFrame = &OnFrame,
        OnEvent = &OnEvent,
        WindowSetupFlags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN |
                           SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE |
                           SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS,
    });


    [UnmanagedCallersOnly]
    static void OnInit(void* userdata)
    {
        var desc = new sg_desc()
        {
            context = Backend.GetContext()
        };

        sg_setup(&desc);

        /* a vertex buffer with 3 vertices */
        Span<float> vertices = stackalloc float[]
        {
            // positions            // colors
            0.0f,
            0.5f,
            0.5f,
            1.0f,
            0.0f,
            0.0f,
            1.0f,
            0.5f,
            -0.5f,
            0.5f,
            0.0f,
            1.0f,
            0.0f,
            1.0f,
            -0.5f,
            -0.5f,
            0.5f,
            0.0f,
            0.0f,
            1.0f,
            1.0f
        };

        var bufferDesc = new sg_buffer_desc();
        bufferDesc.data = new sg_range() { ptr = Unsafe.AsPointer(ref vertices[0]), size = (nuint)(sizeof(float) * vertices.Length) };
        State.Bindings.vertex_buffers[0] = sg_make_buffer(&bufferDesc);

        var shaderDesc = GetShaderDesc();
        var shd = sg_make_shader(&shaderDesc);
        var pipelineDesc = new sg_pipeline_desc()
        {
            shader = shd,
            label = "triangle-pipeline",
        };
        pipelineDesc.layout.attrs[0].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        pipelineDesc.layout.attrs[1].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4;
        State.Pipeline = sg_make_pipeline(&pipelineDesc);



        ImGuiRenderer.Setup(default);
    }

    [UnmanagedCallersOnly]
    static void OnShutdown(void* userdata)
    {
        ImGuiRenderer.Shutdown();
    }

    [UnmanagedCallersOnly]
    static void OnFrame(void* userdata)
    {
        ImGuiRenderer.NewFrame(new ImGuiRenderer.ImGuiFrameDesc()
        {
            Window = Backend.Window,
            DpiScale = 1.0f,
            Width = Backend.Width,
            Height = Backend.Height,
            GlobalMouseState = true
        });


        igShowDemoWindow(null);

        var action = default(sg_pass_action);
        ref var colorAttachment = ref action.colors[0];
        colorAttachment.action = sg_action.SG_ACTION_CLEAR;
        colorAttachment.value = Rgba32F.Black;

        sg_begin_default_pass(&action, Backend.Width, Backend.Height);
        sg_apply_pipeline(State.Pipeline);
        sg_apply_bindings((sg_bindings*) Unsafe.AsPointer(ref State.Bindings));
        sg_draw(0, 3, 1);

        ImGuiRenderer.Render();

        sg_end_pass();
        sg_commit();

        Thread.Sleep(1);
    }

    [UnmanagedCallersOnly]
    static void OnEvent(SDL.SDL_Event* ev, void* userdata)
    {
        ImGuiRenderer.HandleEvent(ev);
    }

    static sg_shader_desc GetShaderDesc()
    {
        var desc = default(sg_shader_desc);
        switch (sg_query_backend())
        {
            case sg_backend.SG_BACKEND_D3D11:
                desc.attrs[0].sem_name = "POS";
                desc.attrs[1].sem_name = "COLOR";
                desc.vs.source = @"
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
                desc.fs.source = @"
                float4 main(float4 color: COLOR0): SV_Target0 {
                  return color;
                }";
                break;
            case sg_backend.SG_BACKEND_GLCORE33:
                desc.attrs[0].name = "position";
                desc.attrs[1].name = "color0";
                desc.vs.source = @"
                # version 330
                in vec4 position;
                in vec4 color0;
                out vec4 color;
                void main() {
                  gl_Position = position;
                  color = color0;
                }";

                desc.fs.source = @"
                # version 330
                in vec4 color;
                out vec4 frag_color;
                void main() {
                  frag_color = color;
                }";
                break;
            case sg_backend.SG_BACKEND_METAL_MACOS:
                desc.vs.source = @"
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
                desc.fs.source = @"
# include <metal_stdlib>
                using namespace metal;
                fragment float4 _main(float4 color [[stage_in]]) {
                   return color;
                };";
                break;
        }
        return desc;
    }  
}

static class State
{
    public static sg_pipeline Pipeline;
    public static sg_bindings Bindings;
}