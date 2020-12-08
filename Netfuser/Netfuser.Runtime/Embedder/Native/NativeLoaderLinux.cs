using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Netfuser.Runtime.Embedder.Native
{
    class NativeLoaderLinux : INativeLoader
    {
        private const string Libdl = "dl";

        [DllImport(Libdl)]
        static extern IntPtr dlopen(string filename, int flags);
        [DllImport(Libdl)]
        static extern IntPtr dlerror();
        public void Preload(string path)
        {
            var h = dlopen(path, 2);
            if (h == IntPtr.Zero)
                throw new IOException($"error loading {path}: {Marshal.PtrToStringAnsi(dlerror())}");
        }

    }
}
