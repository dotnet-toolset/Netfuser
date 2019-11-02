using System;

namespace Netfuser.Core
{
    /// <summary>
    /// Type (or types) of the .NET metadata elements
    /// </summary>
    [Flags]
    public enum MetaType
    {
        None = 0,

        Namespace = 1 << 0,
        Type = 1 << 1,

        Method = 1 << 2,
        Field = 1 << 3,
        Property = 1 << 4,
        Event = 1 << 5,

        NamespaceAndType = Namespace | Type,
        Member = Method | Field | Property | Event,
        All = Namespace | Type | Member
    }
}
