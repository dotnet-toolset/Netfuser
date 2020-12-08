using System;
using System.Collections.Generic;
using System.Text;

namespace Netfuser.Runtime.Embedder.Native
{
    public interface INativeLoader
    {
        void Preload(string path);
    }
}
