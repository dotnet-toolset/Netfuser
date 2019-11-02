using System;
using System.Collections.Generic;
using dnlib.DotNet;
using Netfuser.Core.Naming;

namespace Netfuser.Core.Impl.Manglers.Metadata
{
    class MetadataElement : INsMember
    {
        public INsMember Parent { get; }
        public string NewName { get; }

        public MetadataElement(MetadataElement parent, string name)
        {
            Parent = parent;
            NewName = name;
        }

        public class Node : MetadataElement, INsNode
        {
            private readonly Dictionary<string, INsMember> _byOld;
            private readonly Dictionary<string, INsMember> _byNew;

            public string FullNewName { get; }
            public IReadOnlyCollection<INsMember> Members => _byOld.Values;
            public IReadOnlyDictionary<string, INsMember> MembersByOldName => _byOld;
            public IReadOnlyDictionary<string, INsMember> MembersByNewName => _byNew;

            public Node(Node parent, string name)
                : base(parent, name)
            {
                _byOld = new Dictionary<string, INsMember>();
                _byNew = new Dictionary<string, INsMember>();
                FullNewName = string.IsNullOrEmpty(parent?.FullNewName) ? name : (parent.FullNewName + '.' + name);
            }


            public INsNode GetOrAddNode(string oldName, Func<string> generator)
            {
                if (_byOld.TryGetValue(oldName, out var child))
                    return (INsNode)child;
                var newName = generator?.Invoke();
                if (newName == null) return null;
                var result = new Node(this, newName);
                _byOld.Add(oldName, result);
                _byNew.Add(newName, result);
                return result;
            }

            public INsMember GetOrAddMember(IMemberDef source, Func<string> generator)
            {
                var name = source.Name;
                if (_byOld.TryGetValue(name, out var member))
                    return member;
                var newName = generator?.Invoke();
                if (newName == null) return null;
                var result = new MetadataElement(this, newName);
                _byOld.Add(name, result);
                _byNew.Add(newName, result);
                return result;
            }

            public INsMember GetOrAddPreservedMember(IMemberDef source)
            {
                var name = source.Name;
                if (_byOld.TryGetValue(name, out var result)) return result;
                result = new MetadataElement(this, name);
                _byOld.Add(name, result);
                _byNew.Add(name, result);
                return result;
            }

        }


        public class Root : Node
        {
            public Root()
                : base(null, string.Empty)
            {
            }
        }
    }
}