using System;
using dnlib.DotNet;
using Netfuser.Core.Impl;

namespace Netfuser.Core.Naming
{
    /// <summary>
    /// This plugin establishes uniform API for changing names of metadata elements.
    /// To simplify renaming, tracking metadata names and preserving certain names as required by CLR (and possibly additional rules),
    /// a tree of naming scopes is created based on the metadata tree. Each naming scope may contain only unique names.
    /// The example of a naming scope is a <c>class</c>, where each member must have a unique name.
    /// </summary>
    public interface INaming : IPlugin
    {
        /// <summary>
        /// Get naming scope for a given type, or create one if doesn't exist yet.
        /// </summary>
        /// <param name="tm">type mapping that represents the type for which the naming scope is being requested</param>
        /// <param name="creator">creator of the naming scope. If not specified and if the naming scope doesn't exist, this method will return <see langword="null"/></param>
        /// <returns>naming scope or <see langword="null"/></returns>
        INsNode GetOrAddNode(TypeMapping tm, Func<TypeMapping, INsNode> creator = null);

        /// <summary>
        /// Find new name of the metadata member in any of the source modules.
        /// Usually new name means obfuscated name, but not always.
        /// </summary>
        /// <param name="source">member of one of the soruce modules</param>
        /// <param name="tm">type mapping that represents the parent type of the source</param>
        /// <param name="newName">new name of the source member, if it was renamed</param>
        /// <returns><see langword="true"/> if the source member has new name</returns>
        bool FindNewName(IMemberRef source, out TypeMapping tm, out string newName);

        /// <summary>
        /// Call this method to preserve name of the specified type and/or its relatives
        /// </summary>
        /// <param name="tm">type mapping that represents the type to exclude from renaming</param>
        /// <param name="type"><see cref="MetaType"/>(s) related to the given type that must not be renamed</param>
        /// <returns><c>type</c> parameter</returns>
        MetaType Preserve(TypeMapping tm, MetaType type);

        /// <summary>
        /// Returns <see cref="MetaType"/>(s) related to the given type that are excluded from renaming
        /// </summary>
        /// <param name="tm">type mapping that represents the type that is being checked</param>
        /// <returns></returns>
        MetaType Preserved(TypeMapping tm);

        /// <summary>
        /// Checks whether the given <see cref="MetaType"/>(s) related to the given type must not be renamed
        /// </summary>
        /// <param name="tm">type mapping that represents the type that is being checked</param>
        /// <param name="type"><see cref="MetaType"/>(s) to check</param>
        /// <returns><see langword="true"/> if at least one <see cref="MetaType"/> related to the given type is excluded from renaming </returns>
        bool IsPreserved(TypeMapping tm, MetaType type);

        /// <summary>
        /// Checks whether the given member related to the given type must not be renamed
        /// </summary>
        /// <param name="tm">type mapping that represents the type that is being checked</param>
        /// <param name="member">member to check</param>
        /// <returns><see langword="true"/> if the member is excluded from renaming</returns>
        bool IsPreserved(TypeMapping tm, IMemberDef member);
        
        /// <summary>
        /// Call this method to preserve the given resource from renaming
        /// </summary>
        /// <param name="tm">resource to preserve</param>
        void Preserve(ResourceMapping tm);

        /// <summary>
        /// Checks whether the given resource must not be renamed
        /// </summary>
        /// <param name="tm">resource to check</param>
        /// <returns><see langword="true"/> if the resource is excluded from renaming</returns>
        bool IsPreserved(ResourceMapping tm);
    }
}
