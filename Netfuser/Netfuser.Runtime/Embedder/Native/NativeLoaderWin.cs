using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Netfuser.Runtime.Embedder.Native
{
    class NativeLoaderWin : INativeLoader
    {
        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, uint dwFlags);

        public void Preload(string path)
        {
            var vHandle = LoadLibraryEx(path, IntPtr.Zero, 0);
            if (vHandle == IntPtr.Zero) throw new Win32Exception($"error loading {path}");
        }
    }
}
