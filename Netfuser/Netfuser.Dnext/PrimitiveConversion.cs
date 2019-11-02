using System.Collections.Generic;
using Base.Collections;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext
{
    public class PrimitiveConversion
    {
        public readonly ElementType ElementType;
        public readonly IReadOnlySet<ElementType> ConvertsTo;

        private PrimitiveConversion(ElementType elt, IEnumerable<ElementType> convertsTo)
        {
            ElementType = elt;
            ConvertsTo = convertsTo.AsReadOnlySet();
            Instances.Add(elt, this);
        }

        public void ConvertTo(IILEmitter emitter, ElementType to)
        {
            switch (to)
            {
                case ElementType.I1:
                    emitter.Emit(OpCodes.Conv_I1);
                    break;
                case ElementType.I2:
                    emitter.Emit(OpCodes.Conv_I2);
                    break;
                case ElementType.I4:
                    emitter.Emit(OpCodes.Conv_I4);
                    break;
                case ElementType.I8:
                    emitter.Emit(OpCodes.Conv_I8);
                    break;
                case ElementType.U1:
                    emitter.Emit(OpCodes.Conv_U1);
                    break;
                case ElementType.U2:
                    emitter.Emit(OpCodes.Conv_U2);
                    break;
                case ElementType.U4:
                    emitter.Emit(OpCodes.Conv_U4);
                    break;
                case ElementType.U8:
                    emitter.Emit(OpCodes.Conv_U8);
                    break;
            }
        }


        private static readonly Dictionary<ElementType, PrimitiveConversion> Instances;

        static PrimitiveConversion()
        {
            Instances = new Dictionary<ElementType, PrimitiveConversion>();
            new PrimitiveConversion(ElementType.I1,
                new[]
                {
                    ElementType.U1, ElementType.I2, ElementType.U2, ElementType.I4, ElementType.U4, ElementType.I8,
                    ElementType.U8
                });
            new PrimitiveConversion(ElementType.U1,
                new[]
                {
                    ElementType.I1, ElementType.I2, ElementType.U2, ElementType.I4, ElementType.U4, ElementType.I8,
                    ElementType.U8
                });
            new PrimitiveConversion(ElementType.I2,
                new[] {ElementType.U2, ElementType.I4, ElementType.U4, ElementType.I8, ElementType.U8});
            new PrimitiveConversion(ElementType.U2,
                new[] {ElementType.I2, ElementType.I4, ElementType.U4, ElementType.I8, ElementType.U8});
            new PrimitiveConversion(ElementType.I4, new[] {ElementType.U4, ElementType.I8, ElementType.U8});
            new PrimitiveConversion(ElementType.U4, new[] {ElementType.I4, ElementType.I8, ElementType.U8});
            new PrimitiveConversion(ElementType.I8, new[] {ElementType.U8});
            new PrimitiveConversion(ElementType.U8, new[] {ElementType.I8});
        }

        public static PrimitiveConversion Get(ElementType t) =>
            Instances.TryGetValue(t, out var result) ? result : null;
    }
}