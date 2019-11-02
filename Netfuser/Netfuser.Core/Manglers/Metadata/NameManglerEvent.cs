using System.Collections.Generic;
using Base.Collections;
using dnlib.DotNet;
using Netfuser.Core.Naming;

namespace Netfuser.Core.Manglers.Metadata
{
    /// <summary>
    /// Base class for name mangler events
    /// </summary>
    public abstract class NameManglerEvent : NetfuserEvent
    {
        protected NameManglerEvent(IContext context)
            : base(context)
        {
        }

        /// <summary>
        /// This event is fired before the new name is generated for a given metadata element.
        /// </summary>
        public class GenerateName : NameManglerEvent
        {
            /// <summary>
            /// Name scope (namespace, name of the enclosing type, etc)
            /// </summary>
            public readonly INsMember NameScope;
            /// <summary>
            /// Member that is being renamed
            /// </summary>
            public readonly IMemberDef Source;
            
            /// <summary>
            /// Observers may change name generator options, or replace name generator completely.
            /// If an observer replaces this with a different object, it is advised that applicable options defined in the
            /// current instance are honored. The best way to achieve that is to use a constructor that takes <see cref="NameGenerator"/> 
            /// </summary>
            public NameGenerator Options;
            
            /// <summary>
            /// Generator must not use names in this set
            /// </summary>
            public ISet<string> AvoidNames;
            
            /// <summary>
            /// Convenience method to add names to avoid
            /// </summary>
            /// <param name="names">list of names to avoid</param>
            public void Avoid(IEnumerable<string> names)
            {
                if (names != null)
                    lock (this)
                    {
                        if (AvoidNames == null) AvoidNames = new HashSet<string>();
                        AvoidNames.AddRange(names);
                    }
            }

            /// <summary>
            /// Convenience method to add name to avoid
            /// </summary>
            /// <param name="name">name to avoid</param>
            public void Avoid(string name)
            {
                if (name != null)
                    lock (this)
                    {
                        if (AvoidNames == null) AvoidNames = new HashSet<string>();
                        AvoidNames.Add(name);
                    }
            }

            internal GenerateName(IContext context, INsMember nameScope, IMemberDef source, NameGenerator options)
                : base(context)
            {
                NameScope = nameScope;
                Source = source;
                Options = options;
            }
        }

    }
}
