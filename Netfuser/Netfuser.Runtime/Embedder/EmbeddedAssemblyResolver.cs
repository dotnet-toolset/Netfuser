using System;
using System.Collections.Generic;
using System.Reflection;

namespace Netfuser.Runtime.Embedder
{
    public class EmbeddedAssemblyResolver
    {
        private static ResourceReader reader;
        private static Dictionary<AssemblyName, ResourceEntry> assemblies;
        public static void Bootstrap(string name)
        {
            reader = ResourceReader.GetInstance(name);
            if (reader != null)
            {
                assemblies = new Dictionary<AssemblyName, ResourceEntry>();
                foreach (var e in reader.Index)
                    foreach (var r in e) if (r.IsAssembly)
                        {
                            var n = new AssemblyName(e.Key);
                            assemblies.Add(n, r);
                        }
                AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
                {
                    var bytes = reader.GetBytes(a.Name);
                    if (bytes == null)
                    {
                        var target = new AssemblyName(a.Name);
                        if (!assemblies.TryGetValue(target, out var re))
                            foreach (var n in assemblies)
                                if (n.Key.Name == target.Name && n.Key.Version >= target.Version)
                                {
                                    re = n.Value; break;
                                }
                        if (re != null)
                            bytes = reader.GetBytes(re);
                    }
                    return bytes == null ? null : Assembly.Load(bytes);
                };
            }
        }
    }
}