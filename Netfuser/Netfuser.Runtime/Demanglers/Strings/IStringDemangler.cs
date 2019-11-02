using System;
using System.Collections.Generic;
using System.Text;

namespace Netfuser.Runtime.Demanglers.Strings
{
    /// <summary>
    /// Custom string demanglers MUST implement this interface
    /// </summary>
    public interface IStringDemangler
    {
        /// <summary>
        /// return demangler function that exists in the implementing type and is static
        /// </summary>
        Func<string, string> Demangler { get; }
    }
}
