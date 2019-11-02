using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Base.Rng;
using Base.Text.Impl;
using dnlib.DotNet;
using Netfuser.Core.Naming;

namespace Netfuser.Core.Manglers.Metadata
{
    /// <summary>
    /// Base class for metadata names generator
    /// </summary>
    public abstract class NameGenerator
    {
        /// <summary>
        /// Maximum number of attempts to generate new name (if generated name is rejected as duplicate or for any other reason).
        /// Exception is thrown when this threshold is exceeded. 
        /// </summary>
        public int MaxIterations = 100;

        /// <summary>
        /// Methods/types with generic parameters have "`x" postfix added by the compiler, where x-number of generic parameters.
        /// Many obfuscators preserve this notation, and mangle only the portion of the name before "`".
        /// We believe they do it to ensure uniqueness of the names, in case of multiple types with the same name but
        /// different number of generic parameters. We use have different approach to ensure uniqueness of names, so keeping the
        /// "`x" format may not be necessary.
        /// Even though we haven't found any problems when the entire name is obfuscated, this flag
        /// allows to preserve postfixes in case there are side effects we are not aware of.
        /// </summary>
        public bool PreserveGenericCount = false;

        /// <summary>
        /// Generate new name for a given metadata member
        /// </summary>
        /// <param name="mangler">instance of the mangler plugin</param>
        /// <param name="scope">naming scope</param>
        /// <param name="member">the member to rename</param>
        /// <param name="old">old name</param>
        /// <param name="iteration">number of current iteration (increments with each unsuccessful attemt to generate new name)</param>
        /// <returns>new name</returns>
        public abstract string Generate(IMetadataMangler mangler, INsMember scope, IMemberDef member, string old,
            int iteration);

        /// <summary>
        /// Generate new name for a given method parameter
        /// </summary>
        /// <param name="mangler">instance of the mangler plugin</param>
        /// <param name="source">method parameter</param>
        /// <returns>new name</returns>
        public abstract string Generate(IMetadataMangler mangler, ParamDef source);

        /// <summary>
        /// Generate new name for a given generic parameter
        /// </summary>
        /// <param name="mangler">instance of the mangler plugin</param>
        /// <param name="source">generic parameter</param>
        /// <returns>new name</returns>
        public abstract string Generate(IMetadataMangler mangler, GenericParam source);

        /// <summary>
        /// Make a copy of this instance of name generator
        /// </summary>
        /// <returns>cloned instance</returns>
        public virtual NameGenerator Clone()
        {
            var result = (NameGenerator)MemberwiseClone();
            return result;
        }

        /// <summary>
        /// This is for debugging purposes. Appends underscore ('_') to all names, 
        /// to make sure mangling works and to keep the metadata/code readable
        /// </summary>
        public class Debug : NameGenerator
        {
            public override string Generate(IMetadataMangler mangler, INsMember scope, IMemberDef member, string old,
                int iteration)
            {
                var name = old.Replace('.', '_') + "_";
                if (iteration > 0) name += iteration;
                return name;
            }

            public override string Generate(IMetadataMangler mangler, ParamDef source)
            {
                return source.Name + "_";
            }

            public override string Generate(IMetadataMangler mangler, GenericParam source)
            {
                return source.Name + "_";
            }
        }
        
        /// <summary>
        /// This is a base class for all dictionary-based name encoders that transform 
        /// the given name into a string of pre-defined characters 
        /// </summary>
        public abstract class Encoded : NameGenerator
        {
            /// <summary>
            /// Only these characters will be used in new names
            /// </summary>
            public string Dictionary = ManglerCharsets.Ascii;

            /// <summary>
            /// Only this many bits will be used to encode names.
            /// Generator may provide excessively long input for encoding. For example, 160 bits of SHA1 together with Ascii
            /// charset will generate unnecessarily long names of about 25 characters each. We can easily avoid this and save
            /// some space in the assembly by limiting the number of bits to use for encoding. Using 24 bits (3 bytes) should
            /// be enough in most cases, covering 2^24 or over 16 million of possible names. In the unlikely case that your
            /// project contains more names, this number can be increased.
            /// There's no need to change this unless you are getting errors about too many attempts to generate unique name
            /// </summary>
            public int MaxSignificantBits = 24;

            protected virtual string Encode(byte[] src)
            {
                var codec = BaseNCodec.GetInstance(Dictionary);
                var bitCount = Math.Min(src.Length << 3, MaxSignificantBits);
                var encoded = codec.Encode(src, 0, bitCount);
                // Debug.Assert(Utils.BitsEqual(encoded, codec.Decode(encoded), bitCount);
                return encoded;
            }

            protected static byte[] ToBytes(long v, int msb)
            {
                if (v < 1 << 8 || msb <= 8)
                    return new[] { (byte)(v & 0xff) };
                if (v < 1 << 16 || msb <= 16)
                    return new[] { (byte)((v >> 8) & 0xff), (byte)(v & 0xff) };
                if (v < 1 << 24 || msb <= 24)
                    return new[] { (byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff), (byte)(v & 0xff) };
                if (v < 1L << 32 || msb <= 32)
                    return new[]
                        {(byte) ((v >> 24) & 0xff), (byte) ((v >> 16) & 0xff), (byte) ((v >> 8) & 0xff), (byte) (v & 0xff)};
                if (v < 1L << 40 || msb <= 40)
                    return new[]
                    {
                        (byte) ((v >> 32) & 0xff), (byte) ((v >> 24) & 0xff), (byte) ((v >> 16) & 0xff),
                        (byte) ((v >> 8) & 0xff), (byte) (v & 0xff)
                    };
                if (v < 1L << 48 || msb <= 48)
                    return new[]
                    {
                        (byte) ((v >> 40) & 0xff), (byte) ((v >> 32) & 0xff), (byte) ((v >> 24) & 0xff),
                        (byte) ((v >> 16) & 0xff), (byte) ((v >> 8) & 0xff), (byte) (v & 0xff)
                    };
                if (v < 1L << 56 || msb <= 56)
                    return new[]
                    {
                        (byte) ((v >> 48) & 0xff), (byte) ((v >> 40) & 0xff), (byte) ((v >> 32) & 0xff),
                        (byte) ((v >> 24) & 0xff), (byte) ((v >> 16) & 0xff), (byte) ((v >> 8) & 0xff), (byte) (v & 0xff)
                    };
                return new[]
                {
                    (byte) ((v >> 56) & 0xff), (byte) ((v >> 48) & 0xff), (byte) ((v >> 40) & 0xff),
                    (byte) ((v >> 32) & 0xff), (byte) ((v >> 24) & 0xff), (byte) ((v >> 16) & 0xff),
                    (byte) ((v >> 8) & 0xff), (byte) (v & 0xff)
                };
            }

            public override string Generate(IMetadataMangler mangler, ParamDef source)
            {
                return Encode(ToBytes(source.Sequence, MaxSignificantBits));
            }

            public override string Generate(IMetadataMangler mangler, GenericParam source)
            {
                return Encode(ToBytes(source.Number, MaxSignificantBits));
            }
        }

        /// <summary>
        /// Incremental encoding of names.
        /// Every name in the naming scope is assigned a number from 0 to 
        /// the count of names in the scope, and then the number is encoded using the provided dictionary.
        /// This method does not guarantee one-to-one encoding of names, as it depends on the order 
        /// and quantity of members in the naming scope. 
        /// Decoding of stack traces for an assembly obfuscated with this method will require 
        /// map file to be preserved for every build of the project
        /// </summary>
        public class Incremental : Encoded
        {
            class Seq
            {
                long _value;
                public long Next() => Interlocked.Increment(ref _value);
            }

            private readonly ConcurrentDictionary<INsMember, Seq> _sequences =
                new ConcurrentDictionary<INsMember, Seq>();

            private long NextSequence(INsMember scope) => _sequences.GetOrAdd(scope, s => new Seq()).Next();

            public override string Generate(IMetadataMangler mangler, INsMember scope, IMemberDef member, string old,
                int iteration)
            {
                return Encode(ToBytes(NextSequence(scope), MaxSignificantBits));
            }
        }

        /// <summary>
        /// Random encoding of names. 
        /// Every name in the naming scope is assigned a random value that is encoded using the provided dictionary
        /// This method does not guarantee one-to-one encoding of names.
        /// Decoding of stack traces for an assembly obfuscated with this method will require 
        /// map file to be preserved for every build of the project
        /// </summary>
        public class Random : Encoded
        {
            public override string Generate(IMetadataMangler mangler, INsMember scope, IMemberDef member, string old,
                int iteration)
            {
                return Encode(mangler.Rng.NextBytes((MaxSignificantBits + 7) >> 3));
            }
        }

        /// <summary>
        /// Hash-based encoding of names.
        /// For every name, SHA1 is computed and then encoded using the provided dictionary.
        /// This method guarantees one-to-one encoding of names.
        /// It is OK to keep map file only for the most recent build of the project if this method is used
        /// (unless some major refactoring is performed)
        /// </summary>
        public class Hash : Encoded
        {
            /// <summary>
            /// This algorithm will be used to hash original names when mangling mode is <see cref="NameGenerator.Hash"/>
            /// </summary>
            public HashAlgorithm Hasher = SHA1.Create();

            public override string Generate(IMetadataMangler mangler, INsMember scope, IMemberDef member, string old,
                int iteration)
            {
                if (iteration > 0) old += iteration;
                return Encode(Hasher.ComputeHash(Encoding.UTF8.GetBytes(old)));
            }
        }
    }
}