﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using bottlenoselabs;
using SDL2;
using Sokol;

namespace noname
{
    unsafe class ImGuiRenderer
    {
        private ImGui.ImGuiContext* _context;
        private ImGuiState _state;
        private ulong _timer;
        private readonly IntPtr[] _cursors = new IntPtr[ImGui.ImGuiMouseCursor_COUNT];


        public struct ImGuiDesc
        {
            public int MaxVertices;
            public Gfx.PixelFormat ColorFormat;
            public Gfx.PixelFormat DepthFormat;
            public int SampleCount;
            public string IniFilename;
        }

        public struct ImGuiFrameDesc
        {
            public IntPtr Window;
            public int Width, Height;
            public float DeltaTime;
            public float DpiScale;
            public bool GlobalMouseState;
        }

        unsafe struct ImGuiState
        {
            public ImGuiDesc Desc;
            public float DpiScale;
            public Gfx.Buffer VBuf;
            public Gfx.Buffer IBuf;
            public Gfx.Image Img;
            public Gfx.Shader Shd;
            public Gfx.Pipeline Pip;

            public bool IsOSX;

            public Gfx.Range Vertices;
            public Gfx.Range Indices;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ImGuiVsParam
        {
            public Vector2 DispSize;
            private fixed byte _pad8[8];
        }

        public void Setup(in ImGuiDesc desc)
        {
            _state.Desc = desc;

            _context = ImGui.igCreateContext(null);
            ImGui.igSetCurrentContext(_context);
            ImGui.igStyleColorsDark(ImGui.igGetStyle());

            ref var io = ref Unsafe.AsRef<ImGui.ImGuiIO>(ImGui.igGetIO());
            io.BackendFlags |= ImGui.ImGuiBackendFlags_RendererHasVtxOffset;
            //io.BackendFlags |= ImGui.ImGuiBackendFlags_HasMouseCursors | ImGui.ImGuiBackendFlags_HasSetMousePos;

            io.KeyMap[ImGui.ImGuiKey_Tab] = (int) SDL.SDL_Scancode.SDL_SCANCODE_TAB;
            io.KeyMap[ImGui.ImGuiKey_LeftArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_LEFT;
            io.KeyMap[ImGui.ImGuiKey_RightArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_RIGHT;
            io.KeyMap[ImGui.ImGuiKey_UpArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_UP;
            io.KeyMap[ImGui.ImGuiKey_DownArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_DOWN;
            io.KeyMap[ImGui.ImGuiKey_Home] = (int)SDL.SDL_Scancode.SDL_SCANCODE_HOME;
            io.KeyMap[ImGui.ImGuiKey_End] = (int)SDL.SDL_Scancode.SDL_SCANCODE_END;
            io.KeyMap[ImGui.ImGuiKey_Delete] = (int)SDL.SDL_Scancode.SDL_SCANCODE_DELETE;
            io.KeyMap[ImGui.ImGuiKey_Backspace] = (int)SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE;
            io.KeyMap[ImGui.ImGuiKey_Enter] = (int)SDL.SDL_Scancode.SDL_SCANCODE_RETURN;
            io.KeyMap[ImGui.ImGuiKey_Escape] = (int)SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE; 
            io.KeyMap[ImGui.ImGuiKey_A] = (int)SDL.SDL_Scancode.SDL_SCANCODE_A;
            io.KeyMap[ImGui.ImGuiKey_C] = (int)SDL.SDL_Scancode.SDL_SCANCODE_C;
            io.KeyMap[ImGui.ImGuiKey_V] = (int)SDL.SDL_Scancode.SDL_SCANCODE_V; 
            io.KeyMap[ImGui.ImGuiKey_X] = (int)SDL.SDL_Scancode.SDL_SCANCODE_X;
            io.KeyMap[ImGui.ImGuiKey_Y] = (int)SDL.SDL_Scancode.SDL_SCANCODE_Y;
            io.KeyMap[ImGui.ImGuiKey_Z] = (int)SDL.SDL_Scancode.SDL_SCANCODE_Z;


            _cursors[ImGui.ImGuiMouseCursor_Arrow] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
            _cursors[ImGui.ImGuiMouseCursor_TextInput] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
            _cursors[ImGui.ImGuiMouseCursor_ResizeAll] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL);
            _cursors[ImGui.ImGuiMouseCursor_ResizeNS] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS);
            _cursors[ImGui.ImGuiMouseCursor_ResizeEW] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE);
            _cursors[ImGui.ImGuiMouseCursor_ResizeNESW] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW);
            _cursors[ImGui.ImGuiMouseCursor_ResizeNWSE] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE);
            _cursors[ImGui.ImGuiMouseCursor_Hand] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
            _cursors[ImGui.ImGuiMouseCursor_NotAllowed] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO);



            io.IniFilename = _state.Desc.IniFilename ?? string.Empty;

            _state.Desc.MaxVertices = _state.Desc.MaxVertices == 0 ? ushort.MaxValue + 1 : _state.Desc.MaxVertices;
            _state.DpiScale = 1.0f;

            _state.Vertices.Size = (nuint)(_state.Desc.MaxVertices * sizeof(ImGui.ImDrawVert));
            _state.Vertices.Ptr = (void*) Marshal.AllocHGlobal((int) _state.Vertices.Size);

            _state.Indices.Size = (nuint)(_state.Desc.MaxVertices * 3 * sizeof(ImGui.ImDrawIdx));
            _state.Indices.Ptr = (void*)Marshal.AllocHGlobal((int)_state.Indices.Size);


            var vbDesc = new Gfx.BufferDesc();
            vbDesc.Usage = Gfx.Usage.Stream;
            vbDesc.Size = _state.Vertices.Size;
            _state.VBuf = Gfx.MakeBuffer(vbDesc);

            var ibDesc = new Gfx.BufferDesc();
            ibDesc.Type = Gfx.BufferType.Indexbuffer;
            ibDesc.Usage = Gfx.Usage.Stream;
            ibDesc.Size = _state.Indices.Size;
            _state.IBuf = Gfx.MakeBuffer(ibDesc);



            _ = ImGui.ImFontAtlas_AddFontDefault(io.Fonts, null);

            ulong* fontPixels = null;
            long fontWidth = 0, fontHeight = 0, bytesPerPixels = 0;
            ImGui.ImFontAtlas_GetTexDataAsRGBA32
            (
                io.Fonts,
                &fontPixels,
                (long*) Unsafe.AsPointer(ref fontWidth),
                (long*) Unsafe.AsPointer(ref fontHeight),
                (long*) Unsafe.AsPointer(ref bytesPerPixels)
            );

            var imgDesc = new Gfx.ImageDesc();
            imgDesc.Width = (int) fontWidth;
            imgDesc.Height = (int) fontHeight;
            imgDesc.PixelFormat = Gfx.PixelFormat.Rgba8;
            imgDesc.WrapU = Gfx.Wrap.ClampToEdge;
            imgDesc.WrapV = Gfx.Wrap.ClampToEdge;
            imgDesc.Data.Subimage[0, 0] = new Gfx.Range()
            {
                Ptr = (byte*)fontPixels,
                Size = (nuint)(fontWidth * fontHeight * 4)
            };
            _state.Img = Gfx.MakeImage(imgDesc);
            io.Fonts->TexID = (ImGui.ImTextureID)(void*)_state.Img.Id;

            var shaderDesc = new Gfx.ShaderDesc();
            shaderDesc.Attrs[0].Name = "position";
            shaderDesc.Attrs[1].Name = "texcoord0";
            shaderDesc.Attrs[2].Name = "color0";
            shaderDesc.Attrs[0].SemName = "TEXCOORD";
            shaderDesc.Attrs[1].SemName = "TEXCOORD";
            shaderDesc.Attrs[2].SemName = "TEXCOORD";
            shaderDesc.Attrs[0].SemIndex = 0;
            shaderDesc.Attrs[1].SemIndex = 1;
            shaderDesc.Attrs[2].SemIndex = 2;

            ref var ub = ref shaderDesc.Vs.UniformBlocks[0];
            ub.Size = (nuint) sizeof(ImGuiVsParam);
            ub.Uniforms[0].Name = "vs_params";
            ub.Uniforms[0].Type = Gfx.UniformType.Float4;
            ub.Uniforms[0].ArrayCount = 1;

            
            shaderDesc.Fs.Images[0].Name = "tex";
            shaderDesc.Fs.Images[0].ImageType = Gfx.ImageType._2d;
            shaderDesc.Fs.Images[0].SamplerType = Gfx.SamplerType.Float;

            fixed (byte* ptrVs = _d3d11_vs_hlsl)
            {
                fixed (byte* ptrFs = _d3d11_fs_hlsl)
                {
                    shaderDesc.Vs.Bytecode = new() { Ptr = ptrVs, Size = (nuint)_d3d11_vs_hlsl.Length };
                    shaderDesc.Fs.Bytecode = new() { Ptr = ptrFs, Size = (nuint)_d3d11_fs_hlsl.Length };
                   
                    _state.Shd = Gfx.MakeShader(shaderDesc);
                }
            }

            var pipeLineDesc = new Gfx.PipelineDesc();
            pipeLineDesc.Layout.Buffers[0].Stride = sizeof(ImGui.ImDrawVert);
            ref var attrs = ref pipeLineDesc.Layout.Attrs;
            attrs[0].Offset = 0;
            attrs[0].Format = Gfx.VertexFormat.Float2;
            attrs[1].Offset = 8;
            attrs[1].Format = Gfx.VertexFormat.Float2;
            attrs[2].Offset = 16;
            attrs[2].Format = Gfx.VertexFormat.Ubyte4n;

            pipeLineDesc.Shader = _state.Shd;
            pipeLineDesc.IndexType = Gfx.IndexType.Uint16;
            pipeLineDesc.SampleCount = _state.Desc.SampleCount;
            pipeLineDesc.Depth.PixelFormat = _state.Desc.DepthFormat;
            pipeLineDesc.Colors[0].PixelFormat = _state.Desc.ColorFormat;
            pipeLineDesc.Colors[0].Blend.Enabled = true;
            pipeLineDesc.Colors[0].Blend.SrcFactorRgb = Gfx.BlendFactor.SrcAlpha;
            pipeLineDesc.Colors[0].Blend.DstFactorRgb = Gfx.BlendFactor.OneMinusSrcAlpha;
            pipeLineDesc.Colors[0].WriteMask = Gfx.ColorMask.Rgb;
            _state.Pip = Gfx.MakePipeline(pipeLineDesc);
        }


        public void Shutdown()
        {
            ImGui.igDestroyContext(_context);
        }

        public void HandleEvent(SDL.SDL_Event* ev)
        {
            ref var io = ref Unsafe.AsRef<ImGui.ImGuiIO>(ImGui.igGetIO());

            switch (ev->type)
            {
                case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    break;
                case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                    break;
                case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                    break;
                case SDL.SDL_EventType.SDL_MOUSEMOTION:    
                    break;
                case SDL.SDL_EventType.SDL_KEYDOWN:
                case SDL.SDL_EventType.SDL_KEYUP:

                    int key = (int) ev->key.keysym.scancode;
                    var modState = SDL.SDL_GetModState();
                    io.KeysDown [key] = (ev->type == SDL.SDL_EventType.SDL_KEYDOWN);
                    io.KeyShift = ((modState & SDL.SDL_Keymod.KMOD_SHIFT) != 0);
				    io.KeyCtrl = ((modState & SDL.SDL_Keymod.KMOD_CTRL) != 0);
				    io.KeyAlt = ((modState & SDL.SDL_Keymod.KMOD_ALT) != 0);

                    break;
                case SDL.SDL_EventType.SDL_TEXTINPUT:

                    ImGui.ImGuiIO_AddInputCharactersUTF8((ImGui.ImGuiIO*)Unsafe.AsPointer(ref io), ev->text.text);

                    break;

                case SDL.SDL_EventType.SDL_WINDOWEVENT:

                    switch (ev->window.windowEvent)
                    {
                        case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                            break;
                        case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                            break;
                        case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                            break;
                    }


                    break;
            }
        }

        public void NewFrame(in ImGuiFrameDesc desc)
        {
            _state.DpiScale = desc.DpiScale == 0 ? 1.0f : desc.DpiScale;
            ref var io = ref Unsafe.AsRef<ImGui.ImGuiIO>(ImGui.igGetIO());
            //ref var viewport = ref Unsafe.AsRef<ImGui.ImGuiViewport>(ImGui.igGetMainViewport());

            SDL.SDL_GetWindowPosition(desc.Window, out var window_x, out var window_y);
            SDL.SDL_GetWindowSize(desc.Window, out var w, out var h);
            SDL.SDL_GL_GetDrawableSize(desc.Window, out var dW, out var dH);
            var wndFlags = (SDL.SDL_WindowFlags) SDL.SDL_GetWindowFlags(desc.Window);

            bool isAppFocused = (wndFlags & SDL.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS) != 0;

            io.DisplaySize = new Vector2(w, h);

            if (w > 0 && h > 0)
            {
                io.DisplayFramebufferScale = new Vector2((float) dW / w, (float) dH / h);
            }

            var frequency = SDL.SDL_GetPerformanceFrequency();
            var current_time = SDL.SDL_GetPerformanceCounter();
            io.DeltaTime = _timer > 0 ? (float)((double)(current_time - _timer) / frequency) : (float)(1.0f / 60.0f);
            _timer = current_time;

            //viewport.Pos.X = window_x;
            //viewport.Pos.Y = window_y;
            //viewport.Size.X = w;
            //viewport.Size.Y = h;

            int x, y;
            var mouseButtons = desc.GlobalMouseState ? 
                SDL.SDL_GetGlobalMouseState(out x, out y) 
                :
                SDL.SDL_GetMouseState(out x, out y);


            io.MousePos = desc.GlobalMouseState ? new Vector2(x - window_x, y - window_y) : new Vector2(x, y);

            if (!isAppFocused)
            {
                mouseButtons = 0;
            }

            io.MouseDown[0] = (mouseButtons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_LEFT)) != 0;
            io.MouseDown[1] = (mouseButtons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT)) != 0;
            io.MouseDown[2] = (mouseButtons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE)) != 0;
            io.MouseDown[3] = (mouseButtons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_X1)) != 0;
            io.MouseDown[4] = (mouseButtons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_X2)) != 0;

            SDL.SDL_CaptureMouse(isAppFocused && desc.GlobalMouseState && ImGui.igIsAnyMouseDown() ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);


            if ((io.ConfigFlags & ImGui.ImGuiConfigFlags_NoMouseCursorChange) == 0)
            {
                var imguiCursor = ImGui.igGetMouseCursor();

                if (io.MouseDrawCursor || imguiCursor.Data == ImGui.ImGuiMouseCursor_None)
                {
                    SDL.SDL_ShowCursor(0);
                }
                else
                {
                    SDL.SDL_SetCursor(_cursors[imguiCursor] != IntPtr.Zero ? _cursors[imguiCursor] : _cursors[ImGui.ImGuiMouseCursor_Arrow]);
                    SDL.SDL_ShowCursor(1);
                }
            }            

            ImGui.igNewFrame();
        }

        public void Render()
        {
            ImGui.igRender();

            ref var data = ref Unsafe.AsRef<ImGui.ImDrawData>(ImGui.igGetDrawData());
            ref var io = ref Unsafe.AsRef<ImGui.ImGuiIO>(ImGui.igGetIO());

            if (data.CmdListsCount == 0)
            {
                return;
            }

            int allVtxSize = 0;
            int allIdxSize = 0;
            int cmdListCount = 0;

            for (int clIndex = 0; clIndex < data.CmdListsCount; clIndex++, cmdListCount++)
            {
                ref var cl = ref Unsafe.AsRef<ImGui.ImDrawList>(data.CmdLists[clIndex]);
                var vtxSize = cl.VtxBuffer.Size * sizeof(ImGui.ImDrawVert);
                var idxSize = cl.IdxBuffer.Size * sizeof(ImGui.ImDrawIdx);

                if (allVtxSize + vtxSize > (int) _state.Vertices.Size ||
                    allIdxSize + idxSize > (int) _state.Indices.Size)
                {
                    break;
                }
                
                if (vtxSize > 0)
                {
                    ref var srcVtxPtr = ref cl.VtxBuffer.Data;
                    var dstVtxPtr = ((byte*)_state.Vertices.Ptr) + allVtxSize;
                    new Span<byte>(srcVtxPtr, vtxSize).CopyTo(new Span<byte>(dstVtxPtr, vtxSize));
                }

                if (idxSize > 0)
                {
                    ref var srcIdxPtr = ref cl.IdxBuffer.Data;
                    var dstIdxPtr = ((byte*)_state.Indices.Ptr) + allIdxSize;
                    new Span<byte>(srcIdxPtr, idxSize).CopyTo(new Span<byte>(dstIdxPtr, idxSize));
                }

                allVtxSize += vtxSize;
                allIdxSize += idxSize;
            }

            if (cmdListCount == 0)
            {
                return;
            }

            if (allVtxSize > 0)
            {
                var vtxData = _state.Vertices;
                vtxData.Size = (nuint) allVtxSize;
                Gfx.UpdateBuffer(_state.VBuf, vtxData);
            }

            if (allIdxSize > 0)
            {
                var idxData = _state.Indices;
                idxData.Size = (nuint)allIdxSize;
                Gfx.UpdateBuffer(_state.IBuf, idxData);
            }


            var dpiScale = _state.DpiScale;
            var fbWidth = (int) (io.DisplaySize.X * dpiScale);
            var fbHeight = (int) (io.DisplaySize.Y * dpiScale);

            Gfx.ApplyViewport(0, 0, fbWidth, fbHeight, true);
            Gfx.ApplyScissorRect(0, 0, fbWidth, fbHeight, true);
            Gfx.ApplyPipeline(_state.Pip);

          
            ImGuiVsParam vsParam = new ImGuiVsParam();
            vsParam.DispSize.X = io.DisplaySize.X;
            vsParam.DispSize.Y = io.DisplaySize.Y;

            Gfx.ApplyUniforms(Gfx.ShaderStage.Vs, 0, new Gfx.Range() { Ptr = Unsafe.AsPointer(ref vsParam), Size = (nuint) sizeof(ImGuiVsParam) });

            var bind = new Gfx.Bindings();
            bind.VertexBuffers[0] = _state.VBuf;
            bind.IndexBuffer = _state.IBuf;
            var texID = io.Fonts->TexID;
            bind.FsImages[0].Id = (uint) (void*) texID;
            
            int vbOffset = 0;
            int ibOffset = 0;
            for (int clIndex = 0; clIndex < cmdListCount; ++clIndex)
            {
                ref var cl = ref Unsafe.AsRef<ImGui.ImDrawList>(data.CmdLists[clIndex]);
                bind.VertexBufferOffsets[0] = vbOffset;
                bind.IndexBufferOffset = ibOffset;

                Gfx.ApplyBindings(bind);

                int numCmds = cl.CmdBuffer.Size;
                uint vtxOffset = 0;

                for (int cmdIndex = 0; cmdIndex < numCmds; ++cmdIndex)
                {
                    ref var pcmd = ref cl.CmdBuffer.Data[cmdIndex];

                    if ((uint)(void*)texID != (uint)(void*)pcmd.TextureId || vtxOffset != pcmd.VtxOffset)
                    {
                        texID = pcmd.TextureId;
                        vtxOffset = pcmd.VtxOffset;
                        bind.FsImages[0].Id = (uint)(void*)texID;
                        bind.VertexBufferOffsets[0] = vbOffset + (int)(pcmd.VtxOffset * sizeof(ImGui.ImDrawVert));
                        Gfx.ApplyBindings(bind);
                    }

                    Gfx.ApplyScissorRect
                    (
                        (int)(pcmd.ClipRect.X * dpiScale),
                        (int)(pcmd.ClipRect.Y * dpiScale),
                        (int)((pcmd.ClipRect.Z - pcmd.ClipRect.X) * dpiScale),
                        (int)((pcmd.ClipRect.W - pcmd.ClipRect.Y) * dpiScale),
                        true
                    );

                    Gfx.Draw(pcmd.IdxOffset, pcmd.ElemCount, 1);
                }

                var vtxSize = cl.VtxBuffer.Size * sizeof(ImGui.ImDrawVert);
                var idxSize = cl.IdxBuffer.Size * sizeof(ImGui.ImDrawIdx);
                vbOffset += vtxSize;
                ibOffset += idxSize;
            }

            Gfx.ApplyViewport(0, 0, fbWidth, fbHeight, true);
            Gfx.ApplyViewport(0, 0, fbWidth, fbHeight, true);
        }


        private static readonly byte[] _d3d11_vs_hlsl = new byte[892]
        {
            0x44,0x58,0x42,0x43,0x05,0xf8,0x0b,0x1e,0x7a,0x13,0x49,0x07,0x83,0x60,0x2e,0x88,
    0x06,0xfa,0x10,0x2e,0x01,0x00,0x00,0x00,0x7c,0x03,0x00,0x00,0x05,0x00,0x00,0x00,
    0x34,0x00,0x00,0x00,0xfc,0x00,0x00,0x00,0x60,0x01,0x00,0x00,0xd0,0x01,0x00,0x00,
    0x00,0x03,0x00,0x00,0x52,0x44,0x45,0x46,0xc0,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x48,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x1c,0x00,0x00,0x00,0x00,0x04,0xfe,0xff,
    0x10,0x81,0x00,0x00,0x98,0x00,0x00,0x00,0x3c,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x01,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x76,0x73,0x5f,0x70,0x61,0x72,0x61,0x6d,
    0x73,0x00,0xab,0xab,0x3c,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x60,0x00,0x00,0x00,
    0x10,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x78,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x08,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x88,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x5f,0x32,0x33,0x5f,0x64,0x69,0x73,0x70,0x5f,0x73,0x69,0x7a,
    0x65,0x00,0xab,0xab,0x01,0x00,0x03,0x00,0x01,0x00,0x02,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x4d,0x69,0x63,0x72,0x6f,0x73,0x6f,0x66,0x74,0x20,0x28,0x52,
    0x29,0x20,0x48,0x4c,0x53,0x4c,0x20,0x53,0x68,0x61,0x64,0x65,0x72,0x20,0x43,0x6f,
    0x6d,0x70,0x69,0x6c,0x65,0x72,0x20,0x31,0x30,0x2e,0x31,0x00,0x49,0x53,0x47,0x4e,
    0x5c,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x08,0x00,0x00,0x00,0x50,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x03,0x03,0x00,0x00,0x50,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x03,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x03,0x03,0x00,0x00,0x50,0x00,0x00,0x00,
    0x02,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x02,0x00,0x00,0x00,
    0x0f,0x0f,0x00,0x00,0x54,0x45,0x58,0x43,0x4f,0x4f,0x52,0x44,0x00,0xab,0xab,0xab,
    0x4f,0x53,0x47,0x4e,0x68,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x08,0x00,0x00,0x00,
    0x50,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x03,0x0c,0x00,0x00,0x50,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x0f,0x00,0x00,0x00,
    0x59,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x03,0x00,0x00,0x00,
    0x02,0x00,0x00,0x00,0x0f,0x00,0x00,0x00,0x54,0x45,0x58,0x43,0x4f,0x4f,0x52,0x44,
    0x00,0x53,0x56,0x5f,0x50,0x6f,0x73,0x69,0x74,0x69,0x6f,0x6e,0x00,0xab,0xab,0xab,
    0x53,0x48,0x44,0x52,0x28,0x01,0x00,0x00,0x40,0x00,0x01,0x00,0x4a,0x00,0x00,0x00,
    0x59,0x00,0x00,0x04,0x46,0x8e,0x20,0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x5f,0x00,0x00,0x03,0x32,0x10,0x10,0x00,0x00,0x00,0x00,0x00,0x5f,0x00,0x00,0x03,
    0x32,0x10,0x10,0x00,0x01,0x00,0x00,0x00,0x5f,0x00,0x00,0x03,0xf2,0x10,0x10,0x00,
    0x02,0x00,0x00,0x00,0x65,0x00,0x00,0x03,0x32,0x20,0x10,0x00,0x00,0x00,0x00,0x00,
    0x65,0x00,0x00,0x03,0xf2,0x20,0x10,0x00,0x01,0x00,0x00,0x00,0x67,0x00,0x00,0x04,
    0xf2,0x20,0x10,0x00,0x02,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x68,0x00,0x00,0x02,
    0x01,0x00,0x00,0x00,0x36,0x00,0x00,0x05,0x32,0x20,0x10,0x00,0x00,0x00,0x00,0x00,
    0x46,0x10,0x10,0x00,0x01,0x00,0x00,0x00,0x36,0x00,0x00,0x05,0xf2,0x20,0x10,0x00,
    0x01,0x00,0x00,0x00,0x46,0x1e,0x10,0x00,0x02,0x00,0x00,0x00,0x0e,0x00,0x00,0x08,
    0x32,0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x46,0x10,0x10,0x00,0x00,0x00,0x00,0x00,
    0x46,0x80,0x20,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0a,
    0x32,0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x46,0x00,0x10,0x00,0x00,0x00,0x00,0x00,
    0x02,0x40,0x00,0x00,0x00,0x00,0x00,0xbf,0x00,0x00,0x00,0xbf,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x38,0x00,0x00,0x0a,0x32,0x20,0x10,0x00,0x02,0x00,0x00,0x00,
    0x46,0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x02,0x40,0x00,0x00,0x00,0x00,0x00,0x40,
    0x00,0x00,0x00,0xc0,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x36,0x00,0x00,0x08,
    0xc2,0x20,0x10,0x00,0x02,0x00,0x00,0x00,0x02,0x40,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3f,0x00,0x00,0x80,0x3f,0x3e,0x00,0x00,0x01,
    0x53,0x54,0x41,0x54,0x74,0x00,0x00,0x00,0x07,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x06,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        };

        private static readonly byte[] _d3d11_fs_hlsl = new byte[620]
        {
            0x44,0x58,0x42,0x43,0xd1,0x93,0x1f,0x1b,0x9d,0x70,0x90,0xeb,0xc2,0x7c,0x26,0x07,
    0xdf,0x52,0xda,0x49,0x01,0x00,0x00,0x00,0x6c,0x02,0x00,0x00,0x05,0x00,0x00,0x00,
    0x34,0x00,0x00,0x00,0xd4,0x00,0x00,0x00,0x20,0x01,0x00,0x00,0x54,0x01,0x00,0x00,
    0xf0,0x01,0x00,0x00,0x52,0x44,0x45,0x46,0x98,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x1c,0x00,0x00,0x00,0x00,0x04,0xff,0xff,
    0x10,0x81,0x00,0x00,0x6d,0x00,0x00,0x00,0x5c,0x00,0x00,0x00,0x03,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x01,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x69,0x00,0x00,0x00,0x02,0x00,0x00,0x00,
    0x05,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0xff,0xff,0xff,0xff,0x00,0x00,0x00,0x00,
    0x01,0x00,0x00,0x00,0x0d,0x00,0x00,0x00,0x5f,0x74,0x65,0x78,0x5f,0x73,0x61,0x6d,
    0x70,0x6c,0x65,0x72,0x00,0x74,0x65,0x78,0x00,0x4d,0x69,0x63,0x72,0x6f,0x73,0x6f,
    0x66,0x74,0x20,0x28,0x52,0x29,0x20,0x48,0x4c,0x53,0x4c,0x20,0x53,0x68,0x61,0x64,
    0x65,0x72,0x20,0x43,0x6f,0x6d,0x70,0x69,0x6c,0x65,0x72,0x20,0x31,0x30,0x2e,0x31,
    0x00,0xab,0xab,0xab,0x49,0x53,0x47,0x4e,0x44,0x00,0x00,0x00,0x02,0x00,0x00,0x00,
    0x08,0x00,0x00,0x00,0x38,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x03,0x00,0x00,0x38,0x00,0x00,0x00,
    0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x0f,0x0f,0x00,0x00,0x54,0x45,0x58,0x43,0x4f,0x4f,0x52,0x44,0x00,0xab,0xab,0xab,
    0x4f,0x53,0x47,0x4e,0x2c,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x08,0x00,0x00,0x00,
    0x20,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x0f,0x00,0x00,0x00,0x53,0x56,0x5f,0x54,0x61,0x72,0x67,0x65,
    0x74,0x00,0xab,0xab,0x53,0x48,0x44,0x52,0x94,0x00,0x00,0x00,0x40,0x00,0x00,0x00,
    0x25,0x00,0x00,0x00,0x5a,0x00,0x00,0x03,0x00,0x60,0x10,0x00,0x00,0x00,0x00,0x00,
    0x58,0x18,0x00,0x04,0x00,0x70,0x10,0x00,0x00,0x00,0x00,0x00,0x55,0x55,0x00,0x00,
    0x62,0x10,0x00,0x03,0x32,0x10,0x10,0x00,0x00,0x00,0x00,0x00,0x62,0x10,0x00,0x03,
    0xf2,0x10,0x10,0x00,0x01,0x00,0x00,0x00,0x65,0x00,0x00,0x03,0xf2,0x20,0x10,0x00,
    0x00,0x00,0x00,0x00,0x68,0x00,0x00,0x02,0x01,0x00,0x00,0x00,0x45,0x00,0x00,0x09,
    0xf2,0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x46,0x10,0x10,0x00,0x00,0x00,0x00,0x00,
    0x46,0x7e,0x10,0x00,0x00,0x00,0x00,0x00,0x00,0x60,0x10,0x00,0x00,0x00,0x00,0x00,
    0x38,0x00,0x00,0x07,0xf2,0x20,0x10,0x00,0x00,0x00,0x00,0x00,0x46,0x0e,0x10,0x00,
    0x00,0x00,0x00,0x00,0x46,0x1e,0x10,0x00,0x01,0x00,0x00,0x00,0x3e,0x00,0x00,0x01,
    0x53,0x54,0x41,0x54,0x74,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x01,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        };
    }
}
