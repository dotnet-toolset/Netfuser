using Base.Lang;
using System;
using System.Collections.Generic;

namespace Netfuser.Core.Project
{
    /// <summary>
    /// Contains elements of the .csproj file 
    /// </summary>
    public interface IProject : INamed
    {
        /// <summary>
        /// Full path to the .csproj file
        /// </summary>
        string CsprojPath { get; }

        /// <summary>
        /// Full path to the resulting assembly (built for the config/framework/cpu/platform specified in the project loader options) 
        /// </summary>
        string AssemblyPath { get; }
        /// <summary>
        /// Version of MS tools used to generate this .csproj file
        /// </summary>
        Version ToolsVersion { get; }

        /// <summary>
        /// Empty for the older .csproj files targeting .NET Framework only
        /// For multi-target .csproj contains one or more target frameworks
        /// specified in <TargetFramework/> or <TargetFrameworks/> tag 
        /// </summary>
        IReadOnlyList<string> TargetFrameworks { get; }

        /// <summary>
        /// References to packages
        /// </summary>
        IReadOnlyList<string> PackageReferences { get; }
        /// <summary>
        /// References to assemblies
        /// </summary>
        IReadOnlyList<string> References { get; }
        /// <summary>
        /// References to projects
        /// </summary>
        IReadOnlyList<IProject> ProjectReference { get; }
    }
}