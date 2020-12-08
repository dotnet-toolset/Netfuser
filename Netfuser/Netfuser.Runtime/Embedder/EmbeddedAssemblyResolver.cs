using Netfuser.Runtime.Embedder.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netfuser.Runtime.Embedder
{
    public class EmbeddedAssemblyResolver
    {
        private static readonly string _osPrefix;
        private static readonly string _arch;
        private static readonly INativeLoader _loader;

        private static ResourceReader _reader;
        private static Dictionary<AssemblyName, List<ResourceEntry>> _assemblies;

        /// <summary>
        /// This must be the first method in this class. Netfuser injects call to this method at the start of entry point method
        /// </summary>
        /// <param name="name"></param>
        public static void Bootstrap(string name)
        {
            _reader = ResourceReader.GetInstance(name);
            if (_reader != null)
            {
                _assemblies = new Dictionary<AssemblyName, List<ResourceEntry>>();
                var natives = new Dictionary<string, List<ResourceEntry>>();
                foreach (var e in _reader.Index)
                    foreach (var r in e) if (r.IsAssembly)
                        {
                            var n = new AssemblyName(e.Key);
                            if (!_assemblies.TryGetValue(n, out var l))
                                l = _assemblies[n] = new List<ResourceEntry>();
                            l.Add(r);
                        }
                        else if (r.IsNativeLib)
                        {
                            if (!natives.TryGetValue(e.Key, out var l))
                                l = natives[e.Key] = new List<ResourceEntry>();
                            l.Add(r);
                        }
                if (_loader != null)
                    foreach (var l in natives.Values)
                    {
                        var e = BestMatch(l);
                        if (e != null)
                        {
                            var bytes = _reader.GetBytes(e);
                            var appname = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "app";
                            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), appname, e.Path);
                            if (!File.Exists(path) || !ArraysEqual(bytes, File.ReadAllBytes(path)))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path));
                                File.WriteAllBytes(path, _reader.GetBytes(e));
                            }
                            try
                            {
                                _loader.Preload(path);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.ToString());
                            }
                        }
                    }
                AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
                {
                    var bytes = _reader.GetBytes(a.Name);
                    if (bytes == null)
                    {
                        var target = new AssemblyName(a.Name);
                        if (!_assemblies.TryGetValue(target, out var re))
                            foreach (var n in _assemblies)
                                if (n.Key.Name == target.Name && n.Key.Version >= target.Version)
                                {
                                    re = n.Value; break;
                                }
                        if (re != null)
                            bytes = _reader.GetBytes(BestMatch(re));
                    }
                    return bytes == null ? null : Assembly.Load(bytes);
                };
            }
        }

        static EmbeddedAssemblyResolver()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _osPrefix = "win";
                _loader = Activator.CreateInstance<NativeLoaderWin>();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _osPrefix = "linux";
                _loader = Activator.CreateInstance<NativeLoaderLinux>();

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _osPrefix = "osx";
                _loader = Activator.CreateInstance<NativeLoaderOsx>();
            }
            _arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        }

        static bool ArraysEqual(byte[] array1, byte[] array2)
        {
            if (array1 == array2) return true;
            if (array1 == null || array2 == null) return false;
            if (array1.Length != array2.Length) return false;
            for (var i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;
            return true;
        }

        static ResourceEntry BestMatch(List<ResourceEntry> list)
        {
            if (list.Count == 0) return null;
            foreach (var e in list) if (!string.IsNullOrEmpty(e.Rid))
                {
                    var rp = e.Rid.Split('-');
                    if (rp.Length != 2) continue;
                    if (!rp[0].StartsWith(_osPrefix)) continue;
                    if (rp[1] == _arch) return e;
                }
            foreach (var e in list) if (!string.IsNullOrEmpty(e.Rid))
                {
                    var rp = e.Rid.Split('-');
                    if (rp.Length != 1) continue;
                    if (rp[0].StartsWith(_osPrefix)) return e;
                }
            foreach (var e in list) if (string.IsNullOrEmpty(e.Rid))
                    return e;
            return null;
        }

    }
}