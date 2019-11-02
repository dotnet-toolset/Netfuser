using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Netfuser.Core.Naming
{
    /// <summary>
    /// Naming scope node that is also (potentially) a container 
    /// for other naming scopes and metadata members
    /// </summary>
    public interface INsNode : INsMember
    {
        /// <summary>
        /// Full new name of this naming scope, if renamed. 
        /// Full means inludes namespace name and names of parent scopes (such as classes)
        /// </summary>
        string FullNewName { get; }
        /// <summary>
        /// Child scopes of this naming scope
        /// </summary>
        IReadOnlyCollection<INsMember> Members { get; }
        /// <summary>
        /// Child scopes indexed by their original names
        /// </summary>
        IReadOnlyDictionary<string, INsMember> MembersByOldName { get; }
        /// <summary>
        /// Child scopes indexed by their new names
        /// </summary>
        IReadOnlyDictionary<string, INsMember> MembersByNewName { get; }

        /// <summary>
        /// Gets child scope of this scope by its original name, or create one if doesn't exist.
        /// </summary>
        /// <param name="oldName">original name of the child scope</param>
        /// <param name="generator">creator of the child scope</param>
        /// <returns>existing or created NS node</returns>
        INsNode GetOrAddNode(string oldName, Func<string> generator);
        /// <summary>
        /// Get member of the naming scope that is contained in this scope, or create one if doesn't exist
        /// </summary>
        /// <param name="source">metadata member</param>
        /// <param name="generator">creator of the NS member</param>
        /// <returns>existing or created NS member</returns>
        INsMember GetOrAddMember(IMemberDef source, Func<string> generator);
        /// <summary>
        /// Add metadata member to the list of members that cannot be renamed (if not added alreadty) and return corresponding NS member.
        /// </summary>
        /// <param name="source">metadata member</param>
        /// <returns>NS member that is equivalent to the metadata member</returns>
        INsMember GetOrAddPreservedMember(IMemberDef source);
    }
}
