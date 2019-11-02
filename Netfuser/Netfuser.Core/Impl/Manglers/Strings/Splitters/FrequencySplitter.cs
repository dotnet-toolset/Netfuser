using System;
using System.Collections.Generic;
using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Splitters
{
    class FrequencySplitter : AbstractPlugin, IStringSplitter
    {
        private readonly FrequencySplitterOptions Options;
        public FrequencySplitter(IContextImpl context, FrequencySplitterOptions options)
            : base(context)
        {
            Options = options;
        }

        public void Split(IReadOnlyDictionary<string, StringPieces> strings)
        {
            var parts = new Dictionary<string, StringPiece>();
            var maxFreq = 0;
            var minPartLen = Options.MinPartLen;
            var maxPartLen = Options.MaxPartLen;
            for (var fs = minPartLen; fs <= maxPartLen; fs++)
            {
                foreach (var s in strings.Keys)
                {
                    for (var i = 0; i <= s.Length - fs; i++)
                    {
                        var part = s.Substring(i, fs);
                        if (!parts.TryGetValue(part, out var p))
                            parts.Add(part, p = new FrequencySplitterPiece(part));
                        var f = ((FrequencySplitterPiece)p).Frequency++;
                        if (f > maxFreq) maxFreq = f;
                    }
                }
            }

            foreach (var kv in strings)
            {
                var i = 0;
                var s = kv.Key;
                var pieces = new List<StringPiece>();
                while (i < s.Length)
                {
                    StringPiece best = null;
                    double bestWeight = 0;
                    for (var j = Math.Min(s.Length - i, maxPartLen); j >= minPartLen; j--)
                    {
                        var part = s.Substring(i, j);
                        var freq = (FrequencySplitterPiece)parts[part];
                        if (freq.Frequency == 1) continue;
                        var w = Options.FrequencyWeight * freq.Frequency / maxFreq +
                                Options.UsageWeight * freq.Usage / maxFreq +
                                Options.LengthWeight * part.Length / maxPartLen;
                        if (w > bestWeight)
                        {
                            best = freq;
                            bestWeight = w;
                        }
                    }

                    if (best == null)
                    {
                        var part = s.Substring(i);
                        if (!parts.TryGetValue(part, out best))
                            parts.Add(part, best = new FrequencySplitterPiece(part) { Frequency = 1 });
                    }

                    ((FrequencySplitterPiece)best).Usage++;

                    pieces.Add(best);
                    i += best.Value.Length;
                }
                kv.Value.Set(pieces.ToArray()); // save memory by converting to array
            }
        }
    }
}
