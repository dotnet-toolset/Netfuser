using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    public interface ITempLocals
    {
        /// <summary>
        /// Request temporary variable from the pool. If no free variable of the type is found in the pool, new one will be
        /// created, but not added to the method body. For this reason, it is important to call <see cref="Flush"/>
        /// at the end
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        Local Request(TypeSig type);

        /// <summary>
        /// Same as <see cref="Request"/>, but allows to wrap request/release in the <code>using</code> clause
        /// </summary>
        /// <param name="type"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        IDisposable Use(TypeSig type, out Local local);

        /// <summary>
        /// Return temporary variable to the pool
        /// </summary>
        /// <param name="local"></param>
        void Release(Local local);

        /// <summary>
        /// Define locals that have been added to the pool by <see cref="Request"/> in the method body
        /// <see cref="Request"/> must not be called after this method is called 
        /// </summary>
        /// <param name="body">body of the method</param>
        void Flush(CilBody body);

        /// <summary>
        /// Adds locals that have already been defined in the method body to the pool of temporary locals.
        /// This has the same effect as calling <see cref="Release"/> for each local in the enumerable
        /// </summary>
        /// <param name="locals">list of locals to be re-used for temporary vars</param>
        void Add(IEnumerable<Local> locals);
    }
}