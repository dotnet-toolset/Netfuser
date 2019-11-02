using System;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Impl
{
    static class Tools
    {
        public static OpCode SimplifyBranch(OpCode code)
        {
            switch (code.Code)
            {
                case Code.Beq_S:
                    return OpCodes.Beq;
                case Code.Bge_S:
                    return OpCodes.Bge;
                case Code.Bgt_S:
                    return OpCodes.Bgt;
                case Code.Ble_S:
                    return OpCodes.Ble;
                case Code.Blt_S:
                    return OpCodes.Blt;
                case Code.Bne_Un_S:
                    return OpCodes.Bne_Un;
                case Code.Bge_Un_S:
                    return OpCodes.Bge_Un;
                case Code.Bgt_Un_S:
                    return OpCodes.Bgt_Un;
                case Code.Ble_Un_S:
                    return OpCodes.Ble_Un;
                case Code.Blt_Un_S:
                    return OpCodes.Blt_Un;
                case Code.Br_S:
                    return OpCodes.Br;
                case Code.Brfalse_S:
                    return OpCodes.Brfalse;
                case Code.Brtrue_S:
                    return OpCodes.Brtrue;
                case Code.Leave_S:
                    return OpCodes.Leave;
            }

            throw new NotSupportedException();
        }
    }
}