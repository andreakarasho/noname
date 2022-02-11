using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using noname;
using SDL2;
using Sample;

using static bottlenoselabs.sokol;
using static bottlenoselabs.imgui;

unsafe
{    
    Backend.Run(new Backend.BackendDescription()
    {
        WindowTitle = "Sample with SDL + sokol",
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


        State.Bindings.vertex_buffers[0] = CreateVertexBuffer();
        State.Bindings.index_buffer = CreateIndexBuffer();

        var shaderDesc = GetShaderDesc();
        var shd = sg_make_shader(&shaderDesc);
        var pipelineDesc = new sg_pipeline_desc()
        {
            shader = shd,
            label = "cube-pipeline",
        };
        pipelineDesc.index_type = sg_index_type.SG_INDEXTYPE_UINT16;
        pipelineDesc.cull_mode = sg_cull_mode.SG_CULLMODE_BACK;
        pipelineDesc.layout.attrs[0].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        pipelineDesc.layout.attrs[1].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4;
        State.Pipeline = sg_make_pipeline(&pipelineDesc);
     
        State.Camera = new Camera();
        State.Camera.Position = new Vector3(-6.0f, 4.0f, 6.0f);
        State.Camera.Yaw = -MathF.PI / 4;
        State.Camera.Pitch = -MathF.PI / 9;

        State.Camera.WindowResized(Backend.Width, Backend.Height);

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

        ref var colorAttachment = ref State.Pass.colors[0];
        colorAttachment.action = sg_action.SG_ACTION_CLEAR;

        if (igBegin("Camera info", null, ImGuiWindowFlags_None))
        {
            //igColorEdit4("color", (float*)colorAttachment.value.GetPointer(), ImGuiColorEditFlags_None);

            if (igButton("Reset camera", Vector2.Zero))
            {
                State.Camera.Position = new Vector3(-6.0f, 4.0f, 6.0f);
                State.Camera.Yaw = -MathF.PI / 4;
                State.Camera.Pitch = -MathF.PI / 9;
            }

            var pos = State.Camera.Position;

            if (igDragFloat3("Position", (float*)pos.GetPointer(), 0.1f, 0,0, "%.3f", 0))
            {
                State.Camera.Position = pos;
            }

            var yaw = State.Camera.Yaw;
            if (igDragFloat("Yaw", &yaw, 0.1f, 0, 0, "%.3f", 0))
            {
                State.Camera.Yaw = yaw;
            }

            var pitch = State.Camera.Pitch;
            if (igDragFloat("Pitch", &pitch, 0.1f, 0, 0, "%.3f", 0))
            {
                State.Camera.Pitch = pitch;
            }

            igNewLine();
        }
        igEnd();

        RotateCube();
        UpdateCamera();

        sg_begin_default_pass(State.Pass.GetPointer(), Backend.Width, Backend.Height);
        sg_apply_pipeline(State.Pipeline);
        sg_apply_bindings(State.Bindings.GetPointer());

        var uniforms = default(sg_range);
        uniforms.ptr = State.VertexShaderParams.GetPointer();
        uniforms.size = (ulong)sizeof(VertexShaderParams);
        sg_apply_uniforms(sg_shader_stage.SG_SHADERSTAGE_VS, 0, &uniforms);

        sg_draw(0, 36, 1);

        ImGuiRenderer.Render();

        sg_end_pass();
        sg_commit();

        Thread.Sleep(1);
    }

    [UnmanagedCallersOnly]
    static void OnEvent(SDL.SDL_Event* ev, void* userdata)
    {
        ImGuiRenderer.HandleEvent(ev);

        ref var e = ref ev->GetRef();
    
        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_KEYDOWN:
            case SDL.SDL_EventType.SDL_KEYUP:

                bool isDown = e.type == SDL.SDL_EventType.SDL_KEYDOWN;

                State.Keyboard.KeyDown[(int)e.key.keysym.sym & ~SDL.SDLK_SCANCODE_MASK] = isDown;

                State.Keyboard.Ctrl = (e.key.keysym.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
                State.Keyboard.Alt = (e.key.keysym.mod & SDL.SDL_Keymod.KMOD_ALT) != 0;
                State.Keyboard.Shift = (e.key.keysym.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0;

                break;

            case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
            case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:

                isDown = e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN;

                if (isDown && igGetIO()->WantCaptureMouse)
                {
                    break;
                }

                if (e.button.button == SDL.SDL_BUTTON_LEFT)
                {
                    State.Mouse.LeftDown = isDown;
                }

                if (e.button.button == SDL.SDL_BUTTON_RIGHT)
                {
                    State.Mouse.RightDown = isDown;
                }

                if (e.button.button == SDL.SDL_BUTTON_MIDDLE)
                {
                    State.Mouse.MiddleDown = isDown;
                }

                break;

            case SDL.SDL_EventType.SDL_MOUSEMOTION:

                var pos = new Vector2(e.motion.x, e.motion.y);
                
                if (State.Mouse.LeftDown)
                {
                    State.Camera.LookAround(State.Mouse.Position - pos);
                }

                State.Mouse.Position = pos;

                break;

            case SDL.SDL_EventType.SDL_WINDOWEVENT:

                switch (e.window.windowEvent)
                {
                    case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                        State.Camera.WindowResized(Backend.Width, Backend.Height);
                        break;
                }

                break;
        }
    }

    static sg_shader_desc GetShaderDesc()
    {
        var desc = default(sg_shader_desc);
        ref var uniformBlock = ref desc.vs.uniform_blocks[0];
        uniformBlock.size = (ulong)sizeof(VertexShaderParams);
        ref var mvpUniform = ref uniformBlock.uniforms[0];
        mvpUniform.name = "mvp";
        mvpUniform.type = sg_uniform_type.SG_UNIFORMTYPE_MAT4;

        switch (sg_query_backend())
        {
            case sg_backend.SG_BACKEND_D3D11:
                desc.attrs[0].sem_name = "POSITION";
                desc.attrs[1].sem_name = "COLOR";
                desc.attrs[0].sem_index = 0;
                desc.attrs[1].sem_index = 1;
                desc.vs.source = @"
                cbuffer params: register(b0)
                {
                    float4x4 mvp;
                };
                struct vs_in
                {
                    float4 pos: POSITION;
                    float4 color: COLOR1;
                };
                struct vs_out
                {
                    float4 color: COLOR0;
                    float4 pos: SV_Position;
                };
                vs_out main(vs_in inp)
                {
                    vs_out outp;
                    outp.pos = mul(mvp, inp.pos);
                    outp.color = inp.color;
                    return outp;
                };";
                desc.fs.source = @"
                float4 main(float4 color: COLOR0): SV_Target0
                {
                    return color;
                };";
                break;
            case sg_backend.SG_BACKEND_GLCORE33:
                desc.attrs[0].name = "position";
                desc.attrs[1].name = "color0";
                desc.vs.source = @"
                #version 330
                uniform mat4 mvp;
                layout(location=0) in vec4 position;
                layout(location=1) in vec4 color0;
                out vec4 color;
                void main()
                {
                    gl_Position = mvp * position;
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
                #include <metal_stdlib>
                using namespace metal;
                struct params_t {
                    float4x4 mvp;
                };
                struct vs_in {
                    float4 position [[attribute(0)]];
                    float4 color [[attribute(1)]];
                };
                struct vs_out {
                    float4 pos [[position]];
                    float4 color;
                };
                vertex vs_out _main(vs_in in [[stage_in]], constant params_t& params [[buffer(0)]]) {
                    vs_out out;
                    out.pos = params.mvp * in.position;
                    out.color = in.color;
                    return out;
                }";
                desc.fs.source = @"
                #include <metal_stdlib>
                using namespace metal;
                fragment float4 _main(float4 color [[stage_in]]) {
                   return color;
                };";
                break;
        }
        return desc;
    }

    static void RotateCube()
    {
        const float deltaSeconds = 1 / 60f;

        //State.CubeRotationX += 1.0f * deltaSeconds * 0.5f;
        //State.CubeRotationY += 1.0f * deltaSeconds * 0.5f;
        var rotationMatrixX = Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, State.CubeRotationX);
        var rotationMatrixY = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, State.CubeRotationY);
        var modelMatrix = rotationMatrixX * rotationMatrixY;

        //var width = Backend.Width;
        //var height = Backend.Height;

        //var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
        //    (float)(60.0f * Math.PI / 180),
        //    width / height,
        //    0.01f,
        //    10.0f);
        //var viewMatrix = Matrix4x4.CreateLookAt(
        //    State.Camera.Position,
        //    Vector3.Zero,
        //    Vector3.UnitY);

        //State.VertexShaderParams.ModelViewProjection = modelMatrix * viewMatrix * projectionMatrix;

        State.VertexShaderParams.ModelViewProjection = modelMatrix * State.Camera.ViewMatrix * State.Camera.ProjectionMatrix ;
    }

    static sg_buffer CreateVertexBuffer()
    {
        var vertices = stackalloc Vertex[24];

        // model vertices of the cube using standard cartesian coordinate system:
        //    +Z is towards your eyes, -Z is towards the screen
        //    +X is to the right, -X to the left
        //    +Y is towards the sky (up), -Y is towards the floor (down)
        const float leftX = -1.0f;
        const float rightX = 1.0f;
        const float bottomY = -1.0f;
        const float topY = 1.0f;
        const float backZ = -1.0f;
        const float frontZ = 1.0f;

        // each face of the cube is a rectangle (two triangles), each rectangle is 4 vertices
        // rectangle 1; back
        var color1 = Rgba32F.Red; // #FF0000
        vertices[0].Position = new Vector3(leftX, bottomY, backZ);
        vertices[0].Color = color1;
        vertices[1].Position = new Vector3(rightX, bottomY, backZ);
        vertices[1].Color = color1;
        vertices[2].Position = new Vector3(rightX, topY, backZ);
        vertices[2].Color = color1;
        vertices[3].Position = new Vector3(leftX, topY, backZ);
        vertices[3].Color = color1;
        // rectangle 2; front
        var color2 = Rgba32F.Lime; // NOTE: "lime" is #00FF00; "green" is actually #008000
        vertices[4].Position = new Vector3(leftX, bottomY, frontZ);
        vertices[4].Color = color2;
        vertices[5].Position = new Vector3(rightX, bottomY, frontZ);
        vertices[5].Color = color2;
        vertices[6].Position = new Vector3(rightX, topY, frontZ);
        vertices[6].Color = color2;
        vertices[7].Position = new Vector3(leftX, topY, frontZ);
        vertices[7].Color = color2;
        // rectangle 3; left
        var color3 = Rgba32F.Blue; // #0000FF
        vertices[8].Position = new Vector3(leftX, bottomY, backZ);
        vertices[8].Color = color3;
        vertices[9].Position = new Vector3(leftX, topY, backZ);
        vertices[9].Color = color3;
        vertices[10].Position = new Vector3(leftX, topY, frontZ);
        vertices[10].Color = color3;
        vertices[11].Position = new Vector3(leftX, bottomY, frontZ);
        vertices[11].Color = color3;
        // rectangle 4; right
        var color4 = Rgba32F.Yellow; // #FFFF00
        vertices[12].Position = new Vector3(rightX, bottomY, backZ);
        vertices[12].Color = color4;
        vertices[13].Position = new Vector3(rightX, topY, backZ);
        vertices[13].Color = color4;
        vertices[14].Position = new Vector3(rightX, topY, frontZ);
        vertices[14].Color = color4;
        vertices[15].Position = new Vector3(rightX, bottomY, frontZ);
        vertices[15].Color = color4;
        // rectangle 5; bottom
        var color5 = Rgba32F.Aqua; // #00FFFF
        vertices[16].Position = new Vector3(leftX, bottomY, backZ);
        vertices[16].Color = color5;
        vertices[17].Position = new Vector3(leftX, bottomY, frontZ);
        vertices[17].Color = color5;
        vertices[18].Position = new Vector3(rightX, bottomY, frontZ);
        vertices[18].Color = color5;
        vertices[19].Position = new Vector3(rightX, bottomY, backZ);
        vertices[19].Color = color5;
        // rectangle 6; top
        var color6 = Rgba32F.Fuchsia; // #FF00FF
        vertices[20].Position = new Vector3(leftX, topY, backZ);
        vertices[20].Color = color6;
        vertices[21].Position = new Vector3(leftX, topY, frontZ);
        vertices[21].Color = color6;
        vertices[22].Position = new Vector3(rightX, topY, frontZ);
        vertices[22].Color = color6;
        vertices[23].Position = new Vector3(rightX, topY, backZ);
        vertices[23].Color = color6;

        var desc = new sg_buffer_desc
        {
            usage = sg_usage.SG_USAGE_IMMUTABLE,
            type = sg_buffer_type.SG_BUFFERTYPE_VERTEXBUFFER,
            data =
                {
                    ptr = vertices,
                    size = (uint) (sizeof(Vertex) * 24)
                }
        };


        return sg_make_buffer(&desc);
    }

    static sg_buffer CreateIndexBuffer()
    {
        var indices = stackalloc ushort[]
        {
            0,
            1,
            2,
            0,
            2,
            3, // rectangle 1 of cube, back, clockwise, base vertex: 0
            6,
            5,
            4,
            7,
            6,
            4, // rectangle 2 of cube, front, counter-clockwise, base vertex: 4
            8,
            9,
            10,
            8,
            10,
            11, // rectangle 3 of cube, left, clockwise, base vertex: 8
            14,
            13,
            12,
            15,
            14,
            12, // rectangle 4 of cube, right, counter-clockwise, base vertex: 12
            16,
            17,
            18,
            16,
            18,
            19, // rectangle 5 of cube, bottom, clockwise, base vertex: 16
            22,
            21,
            20,
            23,
            22,
            20 // rectangle 6 of cube, top, counter-clockwise, base vertex: 20
        };

        var desc = new sg_buffer_desc
        {
            usage = sg_usage.SG_USAGE_IMMUTABLE,
            type = sg_buffer_type.SG_BUFFERTYPE_INDEXBUFFER,
            data =
                {
                    ptr = indices,
                    size = (uint) sizeof(ushort) * 36
                }
        };


        return sg_make_buffer(&desc);
    }

    static void UpdateCamera()
    { 
        float sprintFactor = State.Keyboard.Ctrl ? 0.1f : State.Keyboard.Shift ? 2.5f : 1f;
        Vector3 motionDir = Vector3.Zero;


        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_w])
        {
            motionDir += -Vector3.UnitZ;
        }
        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_a])
        {
            motionDir += -Vector3.UnitX;
        }
        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_s])
        {
            motionDir += Vector3.UnitZ;
        }
        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_d])
        {
            motionDir += Vector3.UnitX;
        }
        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_q])
        {
            motionDir += -Vector3.UnitY;
        }
        if (State.Keyboard.KeyDown[(int)SDL.SDL_Keycode.SDLK_e])
        {
            motionDir += Vector3.UnitY;
        }

        State.Camera.Move(motionDir * sprintFactor * 5 * (1f / 144f));
    }
}

static class State
{
    public static sg_pass_action Pass;
    public static sg_pipeline Pipeline;
    public static sg_bindings Bindings;
    public static VertexShaderParams VertexShaderParams;

    public static float CubeRotationX;
    public static float CubeRotationY;

    public static Camera Camera;
    public static Mouse Mouse;
    public static Keyboard Keyboard;
}

struct VertexShaderParams
{
    public Matrix4x4 ModelViewProjection;
}

struct Vertex
{
    public Vector3 Position;
    public Rgba32F Color;
}

struct Mouse
{
    public bool LeftDown, RightDown, MiddleDown;
    public Vector2 Position;
}

struct Keyboard
{
    public unsafe fixed bool KeyDown[512];

    public bool Ctrl, Alt, Shift;
}