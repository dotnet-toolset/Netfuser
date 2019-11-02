using System;
using System.Collections.Generic;
using dnlib.DotNet;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Manglers.Strings
{
    public interface IStringMangleContext : IDisposable
    {
        IStringMangler Mangler { get; }
        MethodDef SourceMethod { get; }
        IILEmitter Emitter { get; }
        Queue<StringPiece> Pieces { get; }
        bool IsEmpty { get; }
        StringMangleStackTop StackTop { get; }

        void EnsureStringBuilderOnStackTop();
    }
}