using System;

namespace Netfuser.Core.Project
{
    public class ProjectOptions
    {
        /// <summary>
        /// Project configuration (usually <c>Debug</c> or <c>Release</c>)
        /// </summary>
        public string Configuration;
        /// <summary>
        /// Platform
        /// </summary>
        public string Platform = "AnyCPU";
        /// <summary>
        /// Target framework
        /// </summary>
        public string TargetFramework;
        /// <summary>
        /// Build method
        /// </summary>
        public Building Building = Building.Build;
        /// <summary>
        /// Path to the builder
        /// </summary>
        public string BuilderPath;
        /// <summary>
        /// Max.number of builder instances for parallel building
        /// </summary>
        public int MaxCpuCount = Environment.ProcessorCount;
        /// <summary>
        /// Increment the <c>Build</c> part in the assembly version with every Netfuser run
        /// </summary>
        public bool AutoIncrementTargetAssemblyBuildNumber = true;
    }
}