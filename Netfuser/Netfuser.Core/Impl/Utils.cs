using System.Diagnostics;
using System.IO;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl
{
    static class Utils
    {
        public class ProcessOutput
        {
            public int ExitCode;
            public string StdOut;
            public string StdErr;

            public ProcessOutput(int exitCode, string stdout, string stderr)
            {
                ExitCode = exitCode;
                StdOut = stdout;
                StdErr = stderr;
            }

            public override string ToString()
            {
                return StdOut + StdErr;
            }
        }

        public static ProcessOutput RunProcess(string target, params string[] arguments)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = string.Join(" ", arguments),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                },
            };

            process.Start();

            process.OutputDataReceived += (_, args) => stdout.WriteLine(args.Data);
            process.ErrorDataReceived += (_, args) => stderr.WriteLine(args.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            return new ProcessOutput(process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        public static bool BitsEqual(byte[] a, byte[] b, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                var bi = i >> 3;
                var bm = 1 << (i % 8);
                var sb = (a[bi] & bm) != 0;
                var db = (b[bi] & bm) != 0;
                if (sb != db) return false;
            }

            return true;
        }

        public static void RandomConst(IILEmitter il, TypeSig t, IRng rng)
        {
            switch (t.ElementType)
            {
                case ElementType.I1:
                    il.Const(rng.NextInt8());
                    break;
                case ElementType.I2:
                    il.Const(rng.NextInt16());
                    break;
                case ElementType.I4:
                    il.Const(rng.NextInt32());
                    break;
                case ElementType.I8:
                    il.Const(rng.NextInt64());
                    break;
                case ElementType.U1:
                    il.Const(rng.NextUInt8());
                    break;
                case ElementType.U2:
                    il.Const(rng.NextUInt16());
                    break;
                case ElementType.U4:
                    il.Const(rng.NextUInt32());
                    break;
                case ElementType.U8:
                    il.Const(rng.NextUInt64());
                    break;
                case ElementType.R4:
                    il.Const(rng.NextFloat());
                    break;
                case ElementType.R8:
                    il.Const(rng.NextDouble());
                    break;
                case ElementType.Char:
                    il.Const((char) rng.NextUInt16());
                    break;
                case ElementType.Boolean:
                    il.Const(rng.NextBoolean());
                    break;
                default:
                    if (t.IsValueType)
                    {
                        var it = il.Importer.Import(t);                     
                        using (il.UseTempLocal(it, out var l))
                        {
                            il.Ldloca(l);
                            il.Initobj(new TypeSpecUser(it));
                            il.Ldloc(l);
                        }
                    }
                    else
                        il.Emit(OpCodes.Ldnull);
                    break;
            }
        }
    }
}