using System;
using System.Text.RegularExpressions;
using Netfuser.Core;
using Netfuser.Runtime.Demanglers.Strings;

namespace Netfuser.Example
{
    class Program
    {
        static readonly Regex PreserverMemeberNamesInTheseTypesByFullNames =
            new Regex(@"^full-name-starts-with-this\.|^or-with-this\.");

        static readonly Regex PreserverMemeberNamesInTheseTypesByName =
            new Regex(@"^TypeName1$|^TypeName2$|^TypeName3$");

        static readonly Regex PreserveTypeNamesWithFieldsAndPropertiesForTheseTypeNames =
            new Regex(@"^TypeNameA$|^TypeNameB$|^TypeNameC$");

        static void Main(string[] args)
        {
            // create new Netfuser context
            NetfuserFactory.NewContext()
                // Load .NET project. Make sure to set the right path!
                .LoadProject("/path/to/csproj")
                // Obfuscate names of classes, methods, fields etc.
                .MangleMetadata()
                // Exclude some members from obfuscation
                .PreserveNames(MetaType.Type, t =>
                {
                    if (PreserveTypeNamesWithFieldsAndPropertiesForTheseTypeNames.IsMatch(t.Name))
                        return MetaType.Type | MetaType.Field | MetaType.Property;
                    if (PreserverMemeberNamesInTheseTypesByName.IsMatch(t.Name)) return MetaType.Type | MetaType.Member;
                    if (PreserverMemeberNamesInTheseTypesByFullNames.IsMatch(t.FullName))
                        return MetaType.Type | MetaType.Member;
                    return 0;
                })
                // Split string constants in the code in the smaller parts and obfuscate each part separately
                .SplitStringsByFrequency()
                // Add simple RC4 mangler for strings
                .MangleStrings(RC4StringMangler.Mangle, RC4StringMangler.Instance)
                // Add int mangler for smaller string parts (<=4 bytes)
                .MangleStringsAsInt()
                // Obfuscate control flow
                .MangleControlFlow()
                // Save file with original metadata names corresponding to new obfuscated names to decode stack traces
                .WriteNameMap()
                // Save resulting assembly
                .WriteAssembly()
                // Do the work
                .Run();
        }
    }
}