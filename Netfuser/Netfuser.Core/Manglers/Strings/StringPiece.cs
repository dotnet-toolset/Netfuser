namespace Netfuser.Core.Manglers.Strings
{
    public class StringPiece
    {
        public readonly string Value;
        public readonly byte Bits;

        public StringPiece(string part)
        {
            Value = part;
            byte bits = 7;
            foreach (var c in part)
            {
                if ((c & 0xff00) != 0)
                {
                    bits = 16;
                    break;
                }

                if ((c & 0x80) != 0 && bits < 8) bits = 8;
            }

            Bits = bits;
        }

        public override string ToString()
            => Value;
    }
}