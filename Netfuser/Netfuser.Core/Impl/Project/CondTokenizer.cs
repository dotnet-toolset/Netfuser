using System;
using System.Globalization;
using System.Text;
using Base.Text;

namespace Netfuser.Core.Impl.Project
{
     class CondTokenizer
    {
        private const int Eof = -1;

        private readonly ITextBuffer _buffer;
        private readonly StringBuilder _builder;

        private object _value;

        private CondToken _token;
        
        public object Value => _value;
        public CondToken Token => _token;
        public CondTokenizer(ITextBuffer buffer)
        {
            _buffer = buffer;
            _builder=new StringBuilder();
        }

        private static bool IsIdentifierStart(int c)
        {
            switch (c)
            {
                case Eof:
                    return false;
                case '$':
                case '_':
                case '\u2118':
                case '\u212E':
                case '\u309B':
                case '\u309C':
                    return true;
                default:
                    switch (char.GetUnicodeCategory((char)c))
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.ModifierLetter:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.LetterNumber:
                            return true;
                        default:
                            return false;
                    }
            }
        }

        private static bool IsIdentifierPart(int c)
        {
            switch (c)
            {
                case Eof:
                    return false;
                case '$':
                case '_':
                case '\u200C': // <ZWNJ>
                case '\u200D': // <ZWJ>
                // Other_ID_Start 
                case '\u2118':
                case '\u212E':
                case '\u309B':
                case '\u309C':
                // Other_ID_Continue 
                case '\u00B7':
                case '\u0387':
                case '\u1369':
                case '\u136A':
                case '\u136B':
                case '\u136C':
                case '\u136D':
                case '\u136E':
                case '\u136F':
                case '\u1370':
                case '\u1371':
                case '\u19DA':
                    return true;
                default:
                    switch (char.GetUnicodeCategory((char)c))
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.ModifierLetter:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.LetterNumber:
                        // UnicodeCombiningMark
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.SpacingCombiningMark:
                        // UnicodeDigit
                        case UnicodeCategory.DecimalDigitNumber:
                        // UnicodeConnectorPunctuation
                        case UnicodeCategory.ConnectorPunctuation:
                            return true;
                        default:
                            // Other_ID_Continue
                            return c >= '\u1369' && c <= '\u1371';
                    }
            }
        }

        private void TakeRegularIdentifier(int c)
        {
            _builder.Clear();
            _builder.Append((char)c);
            while (IsIdentifierPart(_buffer.Peek()))
                _builder.Append((char)_buffer.Next());
            _value = _builder.ToString();
            _token = CondToken.Identifier;
        }

        private void TakeString(int delim)
        {
            _builder.Clear();
            while (true)
            {
                var c = _buffer.Next();
                if (c == Eof) throw Unexpected(c);
                if (c == delim) break;
                _builder.Append((char) c);
            }

            _value = _builder.ToString();
            _token = CondToken.String;
        }

        private object TakeNumber(int c)
        {
            _builder.Clear();
            _builder.Append((char) c);
            var seenDot = c == '.';
            var rightAfterE = false;
            var seenE = false;
            do
            {
                c = _buffer.Next();
                if (c == Eof) break;
                switch (c)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        rightAfterE = false;
                        _builder.Append((char) c);
                        continue;
                    case '.':
                        if (seenDot || seenE) throw Unexpected(c);
                        seenDot = true;
                        rightAfterE = false;
                        _builder.Append((char) c);
                        continue;
                    case 'e':
                    case 'E':
                        if (seenE) throw Unexpected(c);
                        seenE = true;
                        rightAfterE = true;
                        _builder.Append((char) c);
                        continue;
                    case '+':
                    case '-':
                        if (!rightAfterE) break;
                        rightAfterE = false;
                        _builder.Append((char) c);
                        continue;
                }

                _buffer.Prev();
                break;
            } while (true);

            var s = _builder.ToString();
            if (seenDot || seenE || !long.TryParse(s, out var l))
                return double.Parse(s, NumberFormatInfo.InvariantInfo);
            if (l <= int.MaxValue && l >= int.MinValue)
                return (int) l;
            return l;
        }

        public Exception Unexpected(int c)
        {
            throw new Exception("unexpected character: " + (char) c);
        }

        public bool Next()
        {
            while (true)
            {
                var c = _buffer.Next();
                switch (c)
                {
                    case Eof:
                        _token = CondToken.Eof;
                        break;
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                    case '\u000C':
                        _token = CondToken.Space;
                        continue;
                    case '\'':
                    case '"':
                        TakeString(c);
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        _value = TakeNumber(c);
                        _token = CondToken.Number;
                        break;
                    case '.':
                        switch (_buffer.Peek())
                        {
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                                _value = TakeNumber(c);
                                _token = CondToken.Number;
                                break;
                            default:
                                Unexpected(c);
                                break;
                        }

                        break;
                    case '=':
                        if (_buffer.Next() == '=')
                            _token = CondToken.Equal;
                        else Unexpected(c);
                        break;
                    case '>':
                        if (_buffer.Peek() == '=')
                        {
                            _token = CondToken.GreaterEqual;
                            _buffer.Next();
                        }
                        else _token = CondToken.Greater;

                        break;
                    case '<':
                        if (_buffer.Peek() == '=')
                        {
                            _token = CondToken.LowerEqual;
                            _buffer.Next();
                        }
                        else _token = CondToken.Lower;

                        break;
                    case '!':
                        if (_buffer.Peek() == '=')
                        {
                            _token = CondToken.NotEqual;
                            _buffer.Next();
                        }
                        else _token = CondToken.Not;

                        break;
                    case '(':
                        _token = CondToken.Lpar;
                        break;
                    case ')':
                        _token = CondToken.Rpar;
                        break;
                    case ',':
                        _token = CondToken.Comma;
                        break;
                    default:
                        if (IsIdentifierStart(c))
                            TakeRegularIdentifier(c);
                        else throw Unexpected(c);
                        break;
                }

                return _token != CondToken.Eof;
            }
        }
        
        public bool Matches(CondToken expected)
        {
            if (_token!=expected) return false;
            Next();
            return true;
        }
        
    }
}