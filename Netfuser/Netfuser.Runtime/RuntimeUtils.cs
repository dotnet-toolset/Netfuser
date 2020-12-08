using Netfuser.Runtime.Embedder;
using Netfuser.Runtime.Embedder.Compression;
using Netfuser.Runtime.Embedder.Encryption;
using Netfuser.Runtime.Embedder.Native;
using System;

namespace Netfuser.Runtime
{
    public static class RuntimeUtils
    {
        public static string Namespace => typeof(RuntimeUtils).Namespace;
        public static Type[] TypesToInject => new[] {
                    typeof(ResourceEntry),
                    typeof(ResourceReader),
                    typeof(EmbeddedAssemblyResolver),
                    // the following two must be added regardless of the compression/encryption being enabled or disabled,
                    // because they are referenced in the ResourceReader's CIL
                    typeof(IDecompressor),
                    typeof(IDecryptor),
                    typeof(INativeLoader),
                    typeof(NativeLoaderWin),
                    typeof(NativeLoaderLinux),
                    typeof(NativeLoaderOsx)
        };
    }
}