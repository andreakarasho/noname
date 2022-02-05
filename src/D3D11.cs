using System;
using System.Runtime.InteropServices;
using Sokol;
using SDL2;
using System.Runtime.CompilerServices;

namespace noname
{
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

    static unsafe class D3D11
    {
        private static State _state;


        // https://github.com/acaly/LightDx/blob/master/LightDx/Natives.cs
        // https://github.com/floooh/sokol-samples/blob/master/d3d11/d3d11entry.c
        public static BackendContext Setup(IntPtr windowHandle, int w, int h)
        {
            BackendContext ctx = default;
            ref var d3d11 = ref ctx.D3D11;

            var d = new SwapChainDescription(windowHandle, w, h);

            // TODO: adapter ??
            IntPtr adapter = IntPtr.Zero;

            var res = D3D11CreateDeviceAndSwapChain
            (
                adapter,
                adapter == IntPtr.Zero ? 1u : 0u,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                7,
                ref d,
                out d3d11.SwapChain,
                out d3d11.Device,
                out var featureLevel,
                out d3d11.Context
            );


            _state = default;
            _state.Context = d3d11.Context;
            _state.Device = new D3D11DeviceVTable(d3d11.Device);
            _state.SwapChain = new D3D11SwapChainVTable(d3d11.SwapChain);

            CreateDefaultRenderTarget(w, h);

            ctx.PresentCB = &Present;
            ctx.UpdateRenderTargetCB = &UpdateDefaultRenderTarget;
            ctx.ShutdownCB = &Shutdown;

            return ctx;
        }

       

        public static Gfx.ContextDesc GetContext()
        {
            var ctx = new Gfx.ContextDesc
            {
                D3d11 = new Gfx.D3d11ContextDesc()
                {
                    Device = (void*)_state.Device.Ptr,
                    DeviceContext = (void*)_state.Context,
                    RenderTargetViewCb = (delegate* unmanaged<void*>)&GetRenderTargetView,
                    DepthStencilViewCb = (delegate* unmanaged<void*>)&GetDepthStencilView
                }
            };

            return ctx;
        }


        [UnmanagedCallersOnly]
        private static void Shutdown()
        {
            DestroyDefaultRenderTarget();
            SafeDispose(_state.Context);
            SafeDispose(_state.Device.Ptr);
            SafeDispose(_state.SwapChain.Ptr);
        }

        [UnmanagedCallersOnly]
        private static void UpdateDefaultRenderTarget(int w, int h)
        {
            if (_state.SwapChain.Ptr != IntPtr.Zero)
            {
                DestroyDefaultRenderTarget();

                var res = _state.SwapChain.ResizeBuffers
                (
                    _state.SwapChain.Ptr,
                    1,
                    (uint)w,
                    (uint)h,
                    87, // DXGI_FORMAT_B8G8R8A8_UNORM
                    0
                );

                CreateDefaultRenderTarget(w, h);
            }
        }

        private static void CreateDefaultRenderTarget(int w, int h)
        {
            // create backbuffer
            var res = _state.SwapChain.GetBuffer(_state.SwapChain.Ptr, 0, Guids.Texture2D, out _state.MainRenderTarget);

            // create render target view
            res = _state.Device.CreateRenderTargetView(_state.Device.Ptr, _state.MainRenderTarget, null, out _state.MainRenderTargetView);

            // create depthstencil stuff
            var texture2DDescr = new Texture2DDescription
            {
                Width = (uint)w,
                Height = (uint)h,
                MipLevels = 1,
                ArraySize = 1,
                Format = (uint)45,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = 0,
                MiscFlags = 0,
                BindFlags = 64
            };

            res = _state.Device.CreateTexture2D(_state.Device.Ptr, ref texture2DDescr, IntPtr.Zero, out _state.MainDepthStencil);


            var depthStencilDesc = stackalloc int[6]
            {
                (int)texture2DDescr.Format,
                3, //D3D11_DSV_DIMENSION_TEXTURE2D
                0, //Flags = 0
                0, //MipSlice = 0
                0,
                0,
            };

            res = _state.Device.CreateDepthStencilView(_state.Device.Ptr, _state.MainDepthStencil, depthStencilDesc, out _state.MainDepthStencilView);
        }

        private static void DestroyDefaultRenderTarget()
        {
            SafeDispose(_state.MainRenderTarget);
            SafeDispose(_state.MainRenderTargetView);
            SafeDispose(_state.MainDepthStencil);
            SafeDispose(_state.MainDepthStencilView);
        }

        private static void SafeDispose(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.Release(ptr);
            }
        }

        [UnmanagedCallersOnly]
        private static void* GetRenderTargetView() => _state.MainRenderTargetView.ToPointer();

        [UnmanagedCallersOnly]
        private static void* GetDepthStencilView() => _state.MainDepthStencilView.ToPointer();

        [UnmanagedCallersOnly]
        private static void Present() => _state.SwapChain.Present(_state.SwapChain.Ptr, 0, 0);


        private struct State
        {
            public D3D11DeviceVTable Device;
            public D3D11SwapChainVTable SwapChain;
            public IntPtr Context;
            public IntPtr MainRenderTarget;
            public IntPtr MainDepthStencil;
            public IntPtr MainRenderTargetView;
            public IntPtr MainDepthStencilView;
        }

        private struct D3D11SwapChainVTable
        {
            private IntPtr _swapChain;

            public D3D11SwapChainVTable(IntPtr swapChain)
            {
                _swapChain = swapChain;
            }

            public readonly IntPtr Ptr => _swapChain;

            public readonly ref IntPtr func0 => ref (*(IntPtr**)(void*)_swapChain)[0];
            public readonly ref IntPtr func1 => ref (*(IntPtr**)(void*)_swapChain)[1];
            public readonly ref IntPtr func2 => ref (*(IntPtr**)(void*)_swapChain)[2];
            public readonly ref IntPtr func3 => ref (*(IntPtr**)(void*)_swapChain)[3];
            public readonly ref IntPtr func4 => ref (*(IntPtr**)(void*)_swapChain)[4];
            public readonly ref IntPtr func5 => ref (*(IntPtr**)(void*)_swapChain)[5];
            public readonly ref IntPtr func6 => ref (*(IntPtr**)(void*)_swapChain)[6];
            public readonly ref IntPtr func7 => ref (*(IntPtr**)(void*)_swapChain)[7];
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, int, uint> Present =>
                (delegate* unmanaged[Stdcall]<IntPtr, int, int, uint>)(*(IntPtr**)(void*)_swapChain)[8];
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, out IntPtr, uint> GetBuffer =>
                (delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_swapChain)[9];
            public readonly ref IntPtr func10 => ref (*(IntPtr**)(void*)_swapChain)[10];
            public readonly ref IntPtr func11 => ref (*(IntPtr**)(void*)_swapChain)[11];
            public readonly ref IntPtr func12 => ref (*(IntPtr**)(void*)_swapChain)[12];
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, uint, uint, uint> ResizeBuffers =>
                (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, uint, uint, uint>)(*(IntPtr**)(void*)_swapChain)[13];
        }

        private struct D3D11DeviceVTable
        {
            private IntPtr _device;

            public D3D11DeviceVTable(IntPtr device)
            {
                _device = device;
            }

            public readonly IntPtr Ptr => _device;

            public readonly ref IntPtr func0 => ref (*(IntPtr**)(void*)_device)[0];
            public readonly ref IntPtr func1 => ref (*(IntPtr**)(void*)_device)[1];
            public readonly ref IntPtr func2 => ref (*(IntPtr**)(void*)_device)[2];

            public readonly delegate* unmanaged[Stdcall]<IntPtr, void*, void*, out IntPtr, uint> CreateBuffer =>
                   (delegate* unmanaged[Stdcall]<IntPtr, void*, void*, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[3];
            public readonly ref IntPtr func4 => ref (*(IntPtr**)(void*)_device)[4];
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ref Texture2DDescription, IntPtr, out IntPtr, uint> CreateTexture2D =>
                   (delegate* unmanaged[Stdcall]<IntPtr, ref Texture2DDescription, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[5];
            public readonly ref IntPtr func6 => ref (*(IntPtr**)(void*)_device)[6];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint> CreateShaderResourceView =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[7];
            public readonly ref IntPtr funct8 => ref (*(IntPtr**)(void*)_device)[8];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint> CreateRenderTargetView =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[9];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint> CreateDepthStencilView =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[10];
            public delegate* unmanaged[Stdcall]<IntPtr, void*, uint, IntPtr, IntPtr, out IntPtr, uint> CreateInputLayout =>
                (delegate* unmanaged[Stdcall]<IntPtr, void*, uint, IntPtr, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[11];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint> CreateVertex =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[12];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint> CreateGeometry =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[13];
            public readonly ref IntPtr func14 => ref (*(IntPtr**)(void*)_device)[14];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint> CreatePixel =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[15];
            public readonly ref IntPtr func16 => ref (*(IntPtr**)(void*)_device)[16];
            public readonly ref IntPtr func17 => ref (*(IntPtr**)(void*)_device)[17];
            public readonly ref IntPtr func18 => ref (*(IntPtr**)(void*)_device)[18];
            public readonly ref IntPtr func19 => ref (*(IntPtr**)(void*)_device)[19];
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, out IntPtr, uint> CreateBlendState =>
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, out IntPtr, uint>)(*(IntPtr**)(void*)_device)[20];
        }

        private struct SwapChainDescription
        {
            //DXGI_MODE_DESC BufferDesc;
            public uint BufferWidth;
            public uint BufferHeight;
            public uint RefreshRateNumerator;
            public uint RefreshRateDenominator;
            public uint BufferFormat;
            public uint ScanlineOrdering;
            public uint Scaling;
            //DXGI_SAMPLE_DESC SampleDesc;
            public uint SampleCount;
            public uint SampleQuality;
            //DXGI_USAGE BufferUsage;
            public uint BufferUsage;
            //UINT BufferCount;
            public uint BufferCount;
            //HWND OutputWindow;
            public IntPtr OutputWindow;
            //BOOL Windowed;
            public int Windowed;
            //DXGI_SWAP_EFFECT SwapEffect;
            public uint SwapEffect;
            //UINT Flags;
            public uint Flags;

            public SwapChainDescription(IntPtr hWnd, int width, int height)
            {
                BufferWidth = (uint)width;
                BufferHeight = (uint)height;
                RefreshRateNumerator = 60;
                RefreshRateDenominator = 1;
                BufferFormat = 87; //R8G8B8A8_UNorm
                ScanlineOrdering = 0;
                Scaling = 0;
                SampleCount = 1;
                SampleQuality = 0;
                BufferUsage = 32; //RenderTargetOutput
                BufferCount = 1;
                OutputWindow = hWnd;
                Windowed = 1;
                SwapEffect = 0; //Discard
                Flags = 0;
            }
        }

        private struct Texture2DDescription
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public uint Format;
            public uint SampleCount;
            public uint SampleQuality;
            public uint Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
        }

        [DllImport("d3d11.dll")]
        private static extern uint D3D11CreateDeviceAndSwapChain
        (
               IntPtr pAdapter, //null
               uint DriverType, //hardware(1)
               IntPtr Software, //null
               uint Flags, //debug(2) (? or 0)
               IntPtr pFeatureLevels, //null
               uint FeatureLevels, //0
               uint SDKVersion, // D3D11_SDK_VERSION(7)
               ref SwapChainDescription pSwapChainDesc,
               out IntPtr ppSwapChain,
               out IntPtr ppDevice,
               out uint pFeatureLevel,
               out IntPtr ppImmediateContext
        );

        private static class Guids
        {
            public static readonly IntPtr Texture2D = Allocate("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            public static readonly IntPtr Factory = Allocate("7b7166ec-21c7-44ae-b21a-c9ae321ae369");

            private static IntPtr Allocate(string guid)
            {
                IntPtr ret = Marshal.AllocHGlobal(16);
                Unsafe.WriteUnaligned(ret.ToPointer(), new Guid(guid));

                return ret;
            }
        }
    }
}
