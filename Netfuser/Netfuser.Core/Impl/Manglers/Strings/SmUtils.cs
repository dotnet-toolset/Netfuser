using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Base.Collections;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Strings
{
    public static class SmUtils
    {
        private static readonly MethodInfo Method_StringBuilder_Append_Char =
            typeof(StringBuilder).GetMethod("Append", new[] { typeof(char) });

        public static readonly MethodInfo Method_StringBuilder_Append_String =
            typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) });

        private static readonly MethodInfo Method_Object_ToString =
            typeof(object).GetMethod("ToString");

        private static readonly ConstructorInfo Ctor_StringBuilder =
            typeof(StringBuilder).GetConstructor(Empty.Array<Type>());

        private static readonly ConstructorInfo Ctor_StringBuilder_String =
            typeof(StringBuilder).GetConstructor(new Type[] { typeof(string) });

        /// <summary>
        /// Converts small string to integer
        /// </summary>
        /// <param name="s">string to convert</param>
        /// <param name="bits">number of significant bits in the string</param>
        /// <param name="be">use big endian encoding</param>
        /// <returns></returns>
        public static int String2Int(string s, int bits, bool be)
        {
            Debug.Assert(s.Length * bits <= 32);
            uint result = 0;
            int shift;
            if (be)
            {
                shift = 32;
                if (bits <= 8)
                    foreach (var c in s)
                        result |= (c & 0xffu) << (shift -= 8);
                else
                    foreach (var c in s)
                        result |= (c & 0xffffu) << (shift -= 16);
            }
            else
            {
                shift = 0;
                if (bits <= 8)
                    foreach (var c in s)
                    {
                        result |= (c & 0xffu) << shift;
                        shift += 8;
                    }
                else
                    foreach (var c in s)
                    {
                        result |= (c & 0xffffu) << shift;
                        shift += 16;
                    }
            }

            return (int)result;
        }

        public static long String2Long(string s, int bits, bool be)
        {
            Debug.Assert(s.Length * bits <= 64);
            ulong result = 0;
            int shift;
            if (be)
            {
                shift = 64;
                switch (bits)
                {
                    case 7:
                        foreach (var c in s)
                            result |= (c & 0x7fu) << (shift -= 7);
                        break;
                    case 8:
                        foreach (var c in s)
                            result |= (c & 0xffu) << (shift -= 8);
                        break;
                    case 16:
                        foreach (var c in s)
                            result |= (c & 0xffffu) << (shift -= 16);
                        break;
                }
            }
            else
            {
                shift = 0;
                switch (bits)
                {
                    case 7:
                        foreach (var c in s)
                        {
                            result |= (c & 0x7fu) << shift;
                            shift += 7;
                        }

                        break;
                    case 8:
                        foreach (var c in s)
                        {
                            result |= (c & 0xffu) << shift;
                            shift += 8;
                        }

                        break;
                    case 6:
                        foreach (var c in s)
                        {
                            result |= (c & 0xffffu) << shift;
                            shift += 16;
                        }

                        break;
                }
            }

            return (long)result;
        }

        public static StringMangleStackTop Int2String(IStringMangleContext context, int len, int bits, bool be)
        {
            Debug.Assert(len * bits <= 32);
            var emitter = context.Emitter;
            using (emitter.UseTempLocal<int>(out var encVar))
            {
                var uni = bits > 8;
                var step = uni ? 16 : 8;
                var shift = be ? 32 : 0;
                var mask = uni ? 0xffff : 0xff;
                emitter.Stloc(encVar);
                context.EnsureStringBuilderOnStackTop();
                for (var i = 0; i < len; i++)
                {
                    emitter.Ldloc(encVar);
                    if (be)
                    {
                        shift -= step;
                        if (shift > 0)
                            emitter.Const(shift).Emit(OpCodes.Shr_Un);
                    }
                    else
                    {
                        if (shift > 0)
                            emitter.Const(shift).Emit(OpCodes.Shr_Un);
                        shift += step;
                    }

                    emitter.Const(mask).Emit(OpCodes.And).Emit(OpCodes.Conv_U2);
                    emitter.Callvirt(Method_StringBuilder_Append_Char);
                }
            }
            return StringMangleStackTop.StringBuilder;
        }

        public static void NewStringBuilder(IILEmitter emitter, Local initString = null)
        {

            if (initString != null)
            {
                emitter.Ldloc(initString);
                emitter.Newobj(Ctor_StringBuilder_String);
            }
            else emitter.Newobj(Ctor_StringBuilder);
        }

        public static void ToString(IILEmitter emitter) =>
            emitter.Callvirt(Method_Object_ToString);
    }
}