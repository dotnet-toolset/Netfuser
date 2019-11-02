using System.IO;
using System.Linq;
using System.Xml;
using Netfuser.Core.Naming;
using Netfuser.Core.Writers;

namespace Netfuser.Core.Impl.Writers
{
    class MapWriterPluginPlugin : AbstractPlugin.Subscribed, IMapWriterPlugin
    {
        private readonly string _dest;

        public MapWriterPluginPlugin(IContextImpl context, string dest)
            : base(context)
        {
            _dest = dest ?? "names.xml";
        }

        private void WriteXml(TextWriter text)
        {
            var mm = Context.MappedTypes.Values.ToLookup(tm => tm.Source.Scope, tm => tm);
            var ns = Context.Plugin<INaming>();
            using (var writer = XmlWriter.Create(text, new XmlWriterSettings() {Indent = true}))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("names");
                foreach (var m in mm)
                {
                    writer.WriteStartElement(m.Key.ScopeType == dnlib.DotNet.ScopeType.AssemblyRef
                        ? "assembly"
                        : "module");
                    writer.WriteAttributeString("name", m.Key.ScopeName);
                    if (m.Any())
                        foreach (var tm in m)
                        {
                            writer.WriteStartElement("type");
                            writer.WriteAttributeString("source", tm.Source.FullName);
                            writer.WriteAttributeString("target", tm.Target.FullName);
                            var members = ns.GetOrAddNode(tm)?.MembersByOldName
                                .Where(kv => kv.Key != kv.Value.NewName && !(kv.Value is INsNode)).ToList();
                            if (members != null && members.Count > 0)
                                foreach (var mkv in members)
                                {
                                    writer.WriteStartElement("member");
                                    writer.WriteAttributeString("source", mkv.Key);
                                    writer.WriteAttributeString("target", mkv.Value.NewName);
                                    writer.WriteEndElement();
                                }

                            writer.WriteEndElement();
                        }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.Complete ce:
                    var dest = _dest;
                    if (!Path.IsPathRooted(dest) && Context.OutputFolder != null)
                    {
                        if (!Context.OutputFolder.Exists) Context.OutputFolder.Create();
                        dest = Path.Combine(Context.OutputFolder.FullName, dest);
                    }
                    using (var writer = new StreamWriter(dest))
                        WriteXml(writer);
                    break;
            }
        }
    }
}