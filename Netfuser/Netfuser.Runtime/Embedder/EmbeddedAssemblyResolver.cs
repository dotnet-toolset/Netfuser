using System;
using System.Reflection;

namespace Netfuser.Runtime.Embedder
{
    public class EmbeddedAssemblyResolver
    {
        private static ResourceReader reader;

        public static void Bootstrap(string name)
        {
            reader = ResourceReader.GetInstance(name);
            if (reader != null)
                AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
                {
                    var bytes = reader.GetBytes(a.Name);
                    return bytes == null ? null : Assembly.Load(bytes);
                };
        }
    }
}