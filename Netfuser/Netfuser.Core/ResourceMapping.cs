using System.Text;
using Base.Collections.Props;
using dnlib.DotNet;

namespace Netfuser.Core
{
    /// <summary>
    /// This makes an entry in the <see cref="IContext"/>'s list of resources to be included in the target module.
    /// Connects resource from the source module with its peer from the target module.
    /// </summary>
    public class ResourceMapping : PropsContainer
    {
        private static readonly PropKey<ResourceMapping, string>
            PropUniqueName = new PropKey<ResourceMapping, string>();

        /// <summary>
        /// Unlike TypeDefs, Resources don't contain reference to their parent ModuleDef, so we include it here
        /// This will be null for injected resources, as they don't have any parent module (even if they do, we don't care)
        /// </summary>
        public readonly ModuleDef SourceModule;

        /// <summary>
        /// Resource in the source module that corresponds to the <see cref="Target"/>.
        /// </summary>
        public readonly Resource Source;

        /// <summary>
        /// Resource in the target module that corresponds to the <see cref="Source"/>
        /// </summary>
        public readonly Resource Target;

        /// <summary>
        /// This resource name is unique in the target module.
        /// Context.MappedResources.Values.Select(m=>m.UniqueName).GroupBy(n=>n).Any(g=>g.Count()>1) is always false 
        /// </summary>
        public string UniqueName => PropUniqueName[this] ?? Source.Name;


        internal ResourceMapping(ModuleDef sourceModule, Resource source, Resource target)
        {
            SourceModule = sourceModule;
            Source = source;
            Target = target;
            if (source.Name != target.Name)
                PropUniqueName[this] = target.Name;
        }

        public override string ToString() => Target.Name == UniqueName
            ? UniqueName
            : new StringBuilder(UniqueName).Append(" => ").Append(Target.Name).ToString();
    }
}