using System.Collections.Generic;

namespace Netfuser.Dnext.Hierarchy
{
    /// <summary>
    ///  This class represents a group of related methods from different types, that must have the same name.
    ///  Methods A and B are related if either of the below holds true:
    ///  * A overrides B
    ///  * B overrides A
    ///  * A implements B
    ///  * B implements A
    /// </summary>
    public class VChain
    {
        public readonly string Name;
        public readonly ISet<ITypeKey> Types;

        public bool DontRename;
        public string NewName;

        internal VChain(string name)
        {
            Name = name;
            Types = new HashSet<ITypeKey>();
        }
    }
}