using System.Text;
using Base.Collections.Props;
using dnlib.DotNet;

namespace Netfuser.Core
{
    /// <summary>
    /// This makes an entry in the <see cref="IContext"/>'s list of types to be included in the target module.
    /// Connects type definition from the source module with its peer from the target module.
    /// When several modules are merged into one, duplicate type names are possible, and in this case one of the following happens:
    /// 
    /// 1. Target type is renamed to prevent duplicates. In this case, both <see cref="TypeMapping.Target.Name"/>
    /// and <see cref="TypeMapping.PropUniqueName"/> are assigned a new name.
    /// This is needed because Target.Name may get changed by plugins, and there will be no way to obtain unique name for this type
    /// -- OR --
    /// 2. Members of the source type are merged with the members of target type. In this case, two or more source types will correspond 
    /// to a single target type
    /// </summary>
    public class TypeMapping : PropsContainer
    {
        private static readonly PropKey<TypeMapping, string> PropUniqueName = new PropKey<TypeMapping, string>();

        /// <summary>
        /// Type in the source module that corresponds to the <see cref="Target"/>.
        /// </summary>
        public readonly TypeDef Source;
        /// <summary>
        /// Type in the target module that corresponds to the <see cref="Source"/>.
        /// </summary>
        public readonly TypeDef Target;

        /// <summary>
        /// This name together with the namespace makes unique name in the target module's list of types.
        /// Context.MappedTypes.Values.Select(m=>m.Target.Namespace+"."+m.UniqueName).GroupBy(n=>n).Any(g=>g.Count()>1) is always false 
        /// </summary>
        public string UniqueName => PropUniqueName[this] ?? Source.Name;

        internal TypeMapping(TypeDef source, TypeDef target)
        {
            Source = source;
            Target = target;
            if (source.Name != target.Name)
                PropUniqueName[this] = target.Name;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Source.Namespace);
            if (sb.Length > 0) sb.Append('.');
            sb.Append(UniqueName);
            var result = sb.ToString();
            var tfn = Target.FullName;
            if (tfn == result) return result;
            sb.Append(" => ").Append(tfn);
            return sb.ToString();
        }
    }
}