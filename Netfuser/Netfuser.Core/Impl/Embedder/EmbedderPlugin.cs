using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Base.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.Embedder;
using Netfuser.Dnext;
using Netfuser.Dnext.Impl;
using Netfuser.Runtime.Embedder;
using Netfuser.Runtime.Embedder.Compression;
using Netfuser.Runtime.Embedder.Encryption;

namespace Netfuser.Core.Impl.Embedder
{
    class EmbedderPlugin : AbstractPlugin.Subscribed, IEmbedder, IReadable
    {

        private readonly EmbeddedResource _index;
        private readonly IDictionary<string, Embedding> _entries;

        public string Name { get; }

        public EmbedderPlugin(IContextImpl context, string name)
            : base(context)
        {
            Name = name;
            _entries = new Dictionary<string, Embedding>();
            _index = new EmbeddedResource(name, new ReadableDataReaderFactory(this, name), 0, 0);
        }

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.InjectResources injr:
                    Parallel.ForEach(_entries.Values, e => e.Initialize());
                    injr.Add(_index);
                    foreach (var embedding in _entries.Values)
                        injr.Add(embedding.Resource);
                    break;
                case NetfuserEvent.InjectTypes inj:
                    if (Name != NetfuserFactory.AssembliesEmbedderName) break;
                    var ep = Context.MainSourceModule.EntryPoint;
                    if (ep == null) throw Context.Error("assemblies can be embedded only in executable target");
                    inj.Add(typeof(ResourceEntry));
                    inj.Add(typeof(ResourceReader));
                    inj.Add(typeof(EmbeddedAssemblyResolver));
                    // the following two must be added regardless of the compression/encryption being enabled or disabled,
                    // because they are referenced in the ResourceReader's CIL
                    inj.Add(typeof(IDecompressor));
                    inj.Add(typeof(IDecryptor));
                    var decompTypes = _entries.Values.Select(e => e.Compression?.RuntimeDecompressorType)
                        .Where(t => t != null).ToList();
                    if (decompTypes.Count > 0)
                    {
                        inj.Add(decompTypes);
                    }
                    var decryptTypes = _entries.Values.Select(e => e.Encryption?.RuntimeDecryptorType)
                        .Where(t => t != null).ToList();
                    if (decryptTypes.Count > 0)
                    {
                        inj.Add(decryptTypes);
                    }

                    Context.OfType<NetfuserEvent.CilBodyBuilding>().Where(me => me.Source == ep).Take(1).Subscribe(me =>
                    {
                        if (!Context.MappedTypes.TryGetValue(typeof(EmbeddedAssemblyResolver).CreateKey(),
                            out var mapping)) return;
                        var bootstrap = mapping.Target.Methods.FirstOrDefault(m =>
                            m.IsStatic && !m.IsConstructor && !m.IsRuntimeSpecialName);
                        var il = me.GetEmitter();
                        il.MainFragment.Instructions.Insert(0, Instruction.Create(OpCodes.Call, bootstrap));
                        var name = Context.MappedResources[DnextFactory.NewTypeKey(null, _index)].Target.Name;
                        il.MainFragment.Instructions.Insert(0, Instruction.Create(OpCodes.Ldstr, name));
                    });
                    break;
            }
        }

        public void Add(Embedding embedding)
        {
            _entries.Add(embedding.Name, embedding);
        }

        public Stream OpenReader()
        {
            var root = new XElement("index");
            var doc = new XDocument(root);
            foreach (var embedding in _entries.Values)
            {
                var res = embedding.Resource;
                var name = Context.MappedResources[DnextFactory.NewTypeKey(null, res)].Target.Name;
                var entry = new XElement("entry");
                if (embedding.Properties != null)
                    foreach (var kv in embedding.Properties)
                        entry.SetAttributeValue(kv.Key, kv.Value);
                entry.SetAttributeValue(ResourceEntry.KeyName, embedding.Name);
                entry.SetAttributeValue(ResourceEntry.KeyResourceName, name);
                if (embedding.Compression != null)
                {
                    if (!Context.MappedTypes.TryGetValue(embedding.Compression.RuntimeDecompressorType.CreateKey(),
                        out var mapping)) throw Context.Error("could not find decompressor type in target assembly");
                    entry.SetAttributeValue(ResourceEntry.KeyCompression, mapping.Target.MDToken.Raw);
                }
                if (embedding.Encryption != null)
                {
                    if (!Context.MappedTypes.TryGetValue(embedding.Encryption.RuntimeDecryptorType.CreateKey(),
                        out var mapping)) throw Context.Error("could not find decryptor type in target assembly");
                    entry.SetAttributeValue(ResourceEntry.KeyEncryption, mapping.Target.MDToken.Raw);
                }
                root.Add(entry);
            }

            var ms = new MemoryStream();
            using (var writer = new XmlTextWriter(new StreamWriter(ms, Encoding.UTF8, 16384, true)))
                doc.WriteTo(writer);
            ms.Position = 0;
            return ms;
        }
    }
}