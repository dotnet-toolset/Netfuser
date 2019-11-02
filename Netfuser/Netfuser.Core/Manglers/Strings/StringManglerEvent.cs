using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.Impl;

namespace Netfuser.Core.Manglers.Strings
{
    public abstract class StringManglerEvent : NetfuserEvent
    {
        protected StringManglerEvent(IContext context)
            : base(context)
        {
        }

        public class WillMangle : StringManglerEvent
        {
            public readonly TypeMapping TypeMapping;
            public readonly MethodDef Method;
            public readonly Instruction Instruction;

            public string String;

            internal WillMangle(IContextImpl context, TypeMapping tm, MethodDef m, Instruction i)
                :base(context)
            {
                TypeMapping = tm;
                Method = m;
                Instruction = i;
                String = (string)i.Operand;
            }
        }
    }
}
