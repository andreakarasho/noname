using System.Runtime.CompilerServices;


namespace noname
{
    public static unsafe class UnsafeExt
    {
        public static ref T GetRef<T>(this ref T ptr) where T : unmanaged
        {
            return ref Unsafe.AsRef<T>(ptr);
        }

        public static T* GetPointer<T>(this ref T ptr) where T : unmanaged
        {
            return (T*)Unsafe.AsPointer<T>(ref ptr);
        }
    }
}
