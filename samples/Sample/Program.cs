using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using noname;
using SDL2;
using Sample;

using static bottlenoselabs.sokol;
using static bottlenoselabs.imgui;
using Ecs;


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
        pipelineDesc.depth.compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL;
        pipelineDesc.depth.write_enabled = true;
        State.Pipeline = sg_make_pipeline(&pipelineDesc);
     
        State.Camera = new Camera();
        State.Camera.Position = new Vector3(-6.0f, 4.0f, 6.0f);
        State.Camera.Yaw = -MathF.PI / 4;
        State.Camera.Pitch = -MathF.PI / 9;
        State.Camera.WindowResized(Backend.Width, Backend.Height);

        Vector3 pos = new Vector3();
        const int DIST = 5;

        for (int i = 0; i < State.Cubes.Length; ++i)
        {
            ref var cube = ref State.Cubes[i];
            cube.Scale = new Vector3(1, 1, 1);

            if (pos.X > 100)
            {
                pos.X = 0;
                pos.Z += DIST;
            }

            pos.X += DIST;

            cube.Position = pos;
        }

        EcsState.Init();

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

        UpdateCamera();
       
        EcsState.Process();

        ref var colorAttachment = ref State.Pass.colors[0];
        colorAttachment.action = sg_action.SG_ACTION_CLEAR;

        igSetNextWindowSize(new Vector2(400, 600), ImGuiCond_FirstUseEver);
        if (igBegin("Inspector", null, ImGuiWindowFlags_None))
        {
            igCheckbox("process ESC", (bottlenoselabs.imgui.Runtime.CBool*)State.ProcessECS.GetPointer());
            igSameLine(0, -1);
            igCheckbox("Rotate automatically##auto_rotate_cube", (bottlenoselabs.imgui.Runtime.CBool*)State.AutoRotate.GetPointer());

            igNewLine();

            //igColorEdit4("color", (float*)colorAttachment.value.GetPointer(), ImGuiColorEditFlags_None);

            igTextDisabled("Camera");

            var pos = State.Camera.Position;
            if (igDragFloat3("Position##pos_camera", (float*)pos.GetPointer(), 0.1f, 0, 0, "%.3f", 0))
            {
                State.Camera.Position = pos;
            }

            var yaw = State.Camera.Yaw;
            if (igDragFloat("Yaw##yaw_camera", &yaw, 0.1f, 0, 0, "%.3f", 0))
            {
                State.Camera.Yaw = yaw;
            }

            var pitch = State.Camera.Pitch;
            if (igDragFloat("Pitch##pitch_camera", &pitch, 0.1f, 0, 0, "%.3f", 0))
            {
                State.Camera.Pitch = pitch;
            }

            if (igButton("Reset camera", Vector2.Zero))
            {
                State.Camera.Position = new Vector3(-6.0f, 4.0f, 6.0f);
                State.Camera.Yaw = -MathF.PI / 4;
                State.Camera.Pitch = -MathF.PI / 9;
            }

            var clipper = new ImGuiListClipper();
           
            ImGuiListClipper_Begin(&clipper, State.ProcessECS ? EcsState.Entities.Length : State.Cubes.Length, -1);

            while (ImGuiListClipper_Step(&clipper))
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    igNewLine();
                    igSeparator();
                    igNewLine();

                    igPushID_Int(i);

                    if (State.ProcessECS)
                    {
                        ref var entity = ref EcsState.Entities[i];

                        ref var position = ref EcsState.PositionStorage.GetComponent<Position>(entity);
                        ref var origin = ref EcsState.OriginStorage.GetComponent<Origin>(entity);
                        ref var scale = ref EcsState.ScaleStorage.GetComponent<Scale>(entity);
                        ref var rotation = ref EcsState.RotationStorage.GetComponent<Rotation>(entity);

                        igTextDisabled($"Entity - #{i}");
                        igDragFloat3("Position##pos_cube", (float*)position.Value.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat3("Scale##scale_cube", (float*)scale.Value.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat2("Rotation##rot_cube", (float*)rotation.Value.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat3("Origin##origin_cube", (float*)origin.Value.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                    }
                    else
                    {
                        ref var cube = ref State.Cubes[i];

                        igTextDisabled($"Cube - #{i}");
                        igDragFloat3("Position##pos_cube", (float*)cube.Position.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat3("Scale##scale_cube", (float*)cube.Scale.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat2("Rotation##rot_cube", (float*)cube.Rotation.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                        igDragFloat3("Origin##origin_cube", (float*)cube.Origin.GetPointer(), 0.1f, 0, 0, "%.3f", 0);
                    }

                    igPopID();
                   
                }
            }

            ImGuiListClipper_End(&clipper);
        }
        igEnd();


        sg_begin_default_pass(State.Pass.GetPointer(), Backend.Width, Backend.Height);
        sg_apply_pipeline(State.Pipeline);
        sg_apply_bindings(State.Bindings.GetPointer());


        if (State.ProcessECS)
        {
            ProcessEntities();
        }
        else
        {
            ProcessCubes();
        }
        
        ImGuiRenderer.Render();

        sg_end_pass();
        sg_commit();

        //Thread.Sleep(1);
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


    static void ProcessCubes()
    {
        var uniforms = default(sg_range);
        uniforms.size = (ulong)sizeof(VertexShaderParams);
        var p = default(VertexShaderParams);
        uniforms.ptr = p.GetPointer();

        for (int i = 0; i < State.Cubes.Length; ++i)
        {
            ref var cube = ref State.Cubes[i];

            RotateCube(ref cube, ref p.ModelViewProjection);

            sg_apply_uniforms(sg_shader_stage.SG_SHADERSTAGE_VS, 0, &uniforms);
            sg_draw(0, 36, 1);
        }
    }

    static void ProcessEntities()
    {
        var uniforms = default(sg_range);
        uniforms.size = (ulong)sizeof(VertexShaderParams);
        var p = default(VertexShaderParams);
        uniforms.ptr = p.GetPointer();

        for (int i = 0; i < EcsState.Entities.Length; ++i)
        {
            ref var entity = ref EcsState.Entities[i];

            RotateEntity(ref entity, ref p.ModelViewProjection);

            sg_apply_uniforms(sg_shader_stage.SG_SHADERSTAGE_VS, 0, &uniforms);
            sg_draw(0, 36, 1);
        }
    }


    static void RotateEntity(ref int entity, ref Matrix4x4 modelMatrix)
    {
        ref var position = ref EcsState.PositionStorage.GetComponent<Position>(entity);
        ref var origin = ref EcsState.OriginStorage.GetComponent<Origin>(entity);
        ref var scale = ref EcsState.ScaleStorage.GetComponent<Scale>(entity);
        ref var rotation = ref EcsState.RotationStorage.GetComponent<Rotation>(entity);


        bool autoRotate = State.AutoRotate;

        if (State.AutoRotate)
        {
            rotation.Value.X += 0.5f * State.DELTA_FACTOR;
            rotation.Value.Y += 0.5f * State.DELTA_FACTOR;
        }

        modelMatrix =
           Matrix4x4.CreateTranslation(-origin.Value) *
           Matrix4x4.CreateScale(scale.Value) *
           Matrix4x4.CreateRotationX(rotation.Value.X) *
           Matrix4x4.CreateRotationY(rotation.Value.Y) *
           Matrix4x4.CreateTranslation(position.Value);

        modelMatrix = modelMatrix *
            State.Camera.ViewMatrix *
            State.Camera.ProjectionMatrix;
    }

    static void RotateCube(ref Cube cube, ref Matrix4x4 modelMatrix)
    {
        bool autoRotate = State.AutoRotate;

        if (State.AutoRotate)
        {
            cube.Rotation.X += 0.5f * State.DELTA_FACTOR;
            cube.Rotation.Y += 0.5f * State.DELTA_FACTOR;
        }

        modelMatrix =
           Matrix4x4.CreateTranslation(-cube.Origin) *
           Matrix4x4.CreateScale(cube.Scale) *
           Matrix4x4.CreateRotationX(cube.Rotation.X) *
           Matrix4x4.CreateRotationY(cube.Rotation.Y) *      
           Matrix4x4.CreateTranslation(cube.Position);

        modelMatrix = 
            modelMatrix * 
            State.Camera.ViewMatrix * 
            State.Camera.ProjectionMatrix ;
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

        State.Camera.Move(motionDir * sprintFactor * 5 * State.DELTA_FACTOR);
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
}

static class State
{
    public static sg_pass_action Pass;
    public static sg_pipeline Pipeline;
    public static sg_bindings Bindings;

    public static Camera Camera;
    public static Mouse Mouse;
    public static Keyboard Keyboard;
    public static Cube[] Cubes = new Cube[GLOBAL.ENTITIES_COUNT];

    public static bool AutoRotate;

    public const float DELTA_FACTOR = 1f / 144f;

    public static bool ProcessECS;
}

struct VertexShaderParams
{
    public Matrix4x4 ModelViewProjection;
}

struct Cube
{
    public Vector3 Position;
    public Vector3 Scale;
    public Vector2 Rotation;
    public Vector3 Origin;
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


public struct Position
{
    public Vector3 Value;
}

public struct Origin
{
    public Vector3 Value;
}

public struct Scale
{
    public Vector3 Value;
}

public struct Rotation
{
    public Vector2 Value;
}


public class MatrixSystem : GameSystem<Position, Origin, Scale, Rotation>
{
    public override void ProcessEntity(ref Position position, ref Origin origin, ref Scale scale, ref Rotation rotation)
    {
        var modelMatrix =
          Matrix4x4.CreateTranslation(-origin.Value) *
          Matrix4x4.CreateScale(scale.Value) *
          Matrix4x4.CreateRotationX(rotation.Value.X) *
          Matrix4x4.CreateRotationY(rotation.Value.Y) *
          Matrix4x4.CreateTranslation(position.Value);
    }
}

public abstract class GameSystem<T1, T2, T3, T4> : GameSystem 
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    public override void SetTypeMask(ComponentManager cm)
    {
        TypeMask |= cm.GetMask<T1>();
        TypeMask |= cm.GetMask<T2>();
        TypeMask |= cm.GetMask<T3>();
        TypeMask |= cm.GetMask<T4>();
    }

    public override void ProcessEntity(float deltaTime, int entity)
    {
        ref var t1Ref = ref Storages.GetStorage<T1>().GetComponent<T1>(entity);
        ref var t2Ref = ref Storages.GetStorage<T2>().GetComponent<T2>(entity);
        ref var t3Ref = ref Storages.GetStorage<T3>().GetComponent<T3>(entity);
        ref var t4Ref = ref Storages.GetStorage<T4>().GetComponent<T4>(entity);

        ProcessEntity(ref t1Ref, ref t2Ref, ref t3Ref, ref t4Ref);
    }

    public abstract void ProcessEntity(ref T1 t1Rfef, ref T2 t2Ref, ref T3 t3Rfef, ref T4 t4Ref);
}

public static class Time
{
    public static float DeltaTime = 1f / 60f;
}

static class EcsState
{
    public static int[] Entities = new int[GLOBAL.ENTITIES_COUNT];
    private static SystemProcessor _sp;

    public static ComponentStorage<Position> PositionStorage;
    public static ComponentStorage<Origin> OriginStorage;
    public static ComponentStorage<Scale> ScaleStorage;
    public static ComponentStorage<Rotation> RotationStorage;

    public static void Process()
    {
        //_sp.Process(Time.DeltaTime);
    }

    public static void Init()
    {
        var cm = new ComponentManager();

        _sp = new SystemProcessor(cm);
        _sp.RegisterSystem(new MatrixSystem());

        var em = new EntityManager(_sp, cm);

        PositionStorage = Storages.GetStorage<Position>();
        OriginStorage = Storages.GetStorage<Origin>();
        ScaleStorage = Storages.GetStorage<Scale>();
        RotationStorage = Storages.GetStorage<Rotation>();

        Vector3 pos = new Vector3();
        const int DIST = 5;

        for (int i = 0; i < Entities.Length; ++i)
        {
            if (pos.X > 100)
            {
                pos.X = 0;
                pos.Z += DIST;
            }

            pos.X += DIST;

            var entity = em.CreateEntity();
            em.AddComponent<Position>(entity, new Position() { Value = pos });
            em.AddComponent<Scale>(entity, new Scale() { Value = new Vector3(1f, 1f, 1f) });
            em.AddComponent<Origin>(entity);
            em.AddComponent<Rotation>(entity);

            Entities[i] = entity;
        }  
    }
}

static class GLOBAL
{
    public const int ENTITIES_COUNT = 50000;
}