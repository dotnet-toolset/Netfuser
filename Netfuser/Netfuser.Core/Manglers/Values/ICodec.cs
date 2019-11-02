using System;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Manglers.Values
{
    /// <summary>
    /// Base interface for mangling/demangling algorithms for constant values
    /// </summary>
    public interface ICodec
    {
        /// <summary>
        /// Mangles given value
        /// </summary>
        /// <param name="value">value to mangle</param>
        /// <returns>mangled value</returns>
        object Mangle(object value);
        
        /// <summary>
        /// Restores original value from the mangled object 
        /// </summary>
        /// <param name="mangled">mangled object</param>
        /// <returns>original value</returns>
        object Demangle(object mangled);
        
        /// <summary>
        /// Loads raw or mangled value at the top of the stack 
        /// </summary>
        /// <param name="emitter">IL emitter</param>
        /// <param name="value">mangled or raw value</param>
        /// <param name="mangled">indicates whether the value is mangled or raw</param>
        void LoadValue(IILEmitter emitter, object value, bool mangled);
        
        /// <summary>
        /// Emits de-mangling code. The code should expect mangled value on top of the stack,
        /// and should leave original value on top of the stack
        /// </summary>
        /// <param name="emitter">IL emitter</param>
        void EmitDemangler(IILEmitter emitter);

        /// <summary>
        /// Emit conversion of value on top of the stack from one type to another  
        /// </summary>
        /// <param name="emitter">IL emitter</param>
        /// <param name="fromType">source type</param>
        /// <param name="toType">destination type</param>
        void EmitConversion(IILEmitter emitter, Type fromType, Type toType);
    }
}