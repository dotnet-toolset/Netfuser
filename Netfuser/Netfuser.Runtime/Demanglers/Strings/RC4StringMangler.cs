using System;
using System.Collections.Generic;
using System.Text;

namespace Netfuser.Runtime.Demanglers.Strings
{
    /// <summary>
    /// This is just an example of string encryption/decryption.
    /// </summary>
    public unsafe class RC4StringMangler : IStringDemangler
    {
        class Rc4
        {
            private readonly byte[] _s = new byte[256];
            private readonly byte[] _k = new byte[256];

            public Rc4(byte[] key)
            {
                for (var i = 0; i < 256; i++)
                {
                    _s[i] = (byte)i;
                    _k[i] = key[i % key.Length];
                }
            }
            public unsafe void Crypt(byte* src, byte* dst, int len)
            {
                fixed (byte* ps = _s)
                fixed (byte* pk = _k)
                {
                    byte temp;
                    int i, j = 0;
                    var psi = ps;
                    var pki = pk;
                    for (i = 0; i < 256; i++)
                    {
                        j = (j + *psi + *pki) % 256;
                        temp = *psi;
                        *psi = ps[j];
                        ps[j] = temp;
                        psi++;
                        pki++;
                    }
                    i = j = 0;
                    for (var x = 0; x < len; x++)
                    {
                        i = (i + 1) % 256;
                        j = (j + ps[i]) % 256;
                        temp = ps[i];
                        ps[i] = ps[j];
                        ps[j] = temp;
                        var t = (ps[i] + ps[j]) % 256;
                        dst[x] = (byte)(src[x] ^ ps[t]);
                    }
                }
            }
            public unsafe void Crypt(byte[] src, int srcOffset, byte[] dst, int dstOffset, int length)
            {
                fixed (byte* psrc = &src[srcOffset])
                fixed (byte* pdst = &dst[dstOffset])
                    Crypt(psrc, pdst, length);
            }

            public static void Crypt(byte[] key, byte[] aBytes)
            {
                new Rc4(key).Crypt(aBytes, 0, aBytes, 0, aBytes.Length);
            }

            public static void Crypt(byte[] aKey, byte[] aSrc, byte[] aDst)
            {
                new Rc4(aKey).Crypt(aSrc, 0, aDst, 0, aSrc.Length);
            }

            public static void Crypt(byte[] aKey, byte[] aSrc, int aSrcOffset, byte[] aDst, int aDstOffset, int aLength)
            {
                new Rc4(aKey).Crypt(aSrc, aSrcOffset, aDst, aDstOffset, aLength);
            }

        }
        public Func<string, string> Demangler => Mangle;
        public static string Mangle(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var dst = new char[s.Length];
            var rc4 = new Rc4(BitConverter.GetBytes(0xdeadbeaf));
            fixed (char* srcChars = s)
            fixed (char* dstChars = dst)
            {
                var srcBytes = (byte*)srcChars;
                var dstBytes = (byte*)dstChars;
                rc4.Crypt(srcBytes, dstBytes, s.Length * 2);
            }
            return new string(dst);
        }

        public static readonly RC4StringMangler Instance = new RC4StringMangler();
    }
}
