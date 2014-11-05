using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server
{
    public static class Extensions
    {
        public static void Initialize()
        {
            LuaType.RegisterTypeExtension(typeof(Extensions));
        }

        [LuaMember("ssub")]
        public static string sub(this string s, int i, int j = -1)
        {
            if (String.IsNullOrEmpty(s) || j == 0)
                return String.Empty;

            if (i == 0)
                i = 1;

            int iStart;
            int iLen;
            if (i < 0) // Suffix mode
            {
                iStart = s.Length + i;
                if (iStart < 0)
                    iStart = 0;
                iLen = (j < 0 ? s.Length + j + 1 : j) - iStart;
            }
            else // Prefix mode
            {
                iStart = i - 1;
                if (j < 0)
                    j = s.Length + j + 1;
                iLen = j - iStart;
            }

            // correct the length
            if (iStart + iLen > s.Length)
                iLen = s.Length - iStart;

            // return the string
            if (iLen <= 0)
                return String.Empty;
            else
                return s.Substring(iStart, iLen);
        }

        private static string TranslateRegularExpression(string sRegEx)
        {
            StringBuilder sb = new StringBuilder();
            bool lEscape = false;

            for (int i = 0; i < sRegEx.Length; i++)
            {
                char c = sRegEx[i];
                if (lEscape)
                {
                    if (c == '%')
                    {
                        sb.Append('%');
                        lEscape = false;
                    }
                    else
                    {
                        bool negate = false;

                        if (char.IsUpper(c))
                        {
                            c = char.ToLower(c);

                            sb.Append("[^");

                            negate = true;
                        }

                        switch (c)
                        {
                            case 'a': // all letters
                                sb.Append("[\\w-[\\d]]");
                                break;
                            case 's': // all space characters
                                sb.Append("\\s");
                                break;
                            case 'd': // all digits
                                sb.Append("\\d");
                                break;
                            case 'w': // all alphanumeric characters
                                sb.Append("\\w");
                                break;
                            case 'c': // all control characters
                            case 'g': // all printable characters except space
                            case 'l': // all lowercase letters
                            case 'p': // all punctuation characters
                            case 'u': // all uppercase letters
                            case 'x': // all hexadecimal digits
                                throw new NotImplementedException();
                            case 'z':
                                sb.Append("\0");
                                break;
                            default:
                                sb.Append('\\');
                                sb.Append(c);
                                break;
                        }

                        if (negate)
                        {
                            sb.Append("]");
                        }

                        lEscape = false;
                    }
                }
                else if (c == '%')
                {
                    lEscape = true;
                }
                else if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else
                    sb.Append(c);
            }

            return sb.ToString();
        } // func TranslateRegularExpression

        public static LuaResult @byte(this string s, int i = 1, int j = int.MaxValue)
        {
            if (String.IsNullOrEmpty(s) || i > j)
                return LuaResult.Empty;

            if (i < 1)
                i = 1; // default for i is 1
            if (j == int.MaxValue)
                j = i; // default for j is i
            else if (j > s.Length)
                j = s.Length; // correct the length

            int iLen = j - i + 1; // how many chars to we need

            object[] r = new object[iLen];
            for (int a = 0; a < iLen; a++)
                r[a] = (int)s[i + a - 1];

            return r;
        } // func byte

        public static string dump(Delegate dlg)
        {
            throw new NotImplementedException();
        } // func dump

        [LuaMember("sfind")]
        public static LuaResult find(this string s, string pattern, int init = 1, bool plain = false)
        {
            if (String.IsNullOrEmpty(s))
                return LuaResult.Empty;
            if (String.IsNullOrEmpty(pattern))
                return LuaResult.Empty;

            // correct the init parameter
            if (init < 0)
                init = s.Length + init + 1;
            if (init <= 0)
                init = 1;

            if (plain) // plain pattern
            {
                int iIndex = s.IndexOf(pattern, init - 1);
                if (iIndex >= 0)
                {
                    return new LuaResult(iIndex + 1, iIndex + pattern.Length);
                }
                else
                {
                    return new LuaResult(null);
                }
            }
            else
            {
                // translate the regular expression
                pattern = TranslateRegularExpression(pattern);

                Regex r = new Regex(pattern);
                Match m = r.Match(s.Substring(init - 1));
                if (m.Success)
                {
                    object[] result = new object[m.Captures.Count + 2];

                    result[0] = m.Index + (init - 1) + 1;
                    result[1] = m.Index + (init - 1) + m.Length;
                    for (int i = 0; i < m.Captures.Count; i++)
                        result[i + 2] = m.Captures[i].Value;

                    return result;
                }
                else
                    return new LuaResult(null);
            }
        } // func find

        private static LuaResult matchEnum(object s, object current)
        {
            System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;

            // return value
            if (e.MoveNext())
            {
                Match m = (Match)e.Current;
                return MatchResult(m);
            }
            else
                return LuaResult.Empty;
        } // func matchEnum

        public static LuaResult gmatch(this string s, string pattern)
        {
            // f,s,v
            if (String.IsNullOrEmpty(s))
                return LuaResult.Empty;
            if (String.IsNullOrEmpty(pattern))
                return LuaResult.Empty;

            // translate the regular expression
            pattern = TranslateRegularExpression(pattern);

            Regex r = new Regex(pattern);
            MatchCollection m = r.Matches(s);
            System.Collections.IEnumerator e = m.GetEnumerator();

            return new LuaResult(new Func<object, object, LuaResult>(matchEnum), e, e);
        } // func gmatch

        [LuaMember("gsub")]
        public static string gsub(this string s, string pattern, string repl, int n)
        {
            if (String.IsNullOrEmpty(s))
                return string.Empty;
            if (String.IsNullOrEmpty(pattern))
                return string.Empty;

            pattern = TranslateRegularExpression(pattern);

            Regex r = new Regex(pattern);
            return r.Replace(s, repl.Replace('%', '$'), (n == 0) ? int.MaxValue : n);
        } // func gsub

        [LuaMember("gsubf")]
        public static string gsub(this string s, string pattern, Func<string, LuaResult> repl, int n)
        {
            if (String.IsNullOrEmpty(s))
                return string.Empty;
            if (String.IsNullOrEmpty(pattern))
                return string.Empty;

            pattern = TranslateRegularExpression(pattern);

            Regex r = new Regex(pattern);
            return r.Replace(s, match => repl(match.Value)[0].ToString(), (n == 0) ? int.MaxValue : n);
        } // func gsub

        public static int len(this string s)
        {
            return s == null ? 0 : s.Length;
        } // func len

        public static string lower(this string s)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            return s.ToLower();
        } // func lower

        public static LuaResult match(this string s, string pattern, int init = 1)
        {
            if (String.IsNullOrEmpty(s))
                return LuaResult.Empty;
            if (String.IsNullOrEmpty(pattern))
                return LuaResult.Empty;

            // correct the init parameter
            if (init < 0)
                init = s.Length + init + 1;
            if (init <= 0)
                init = 1;

            // translate the regular expression
            pattern = TranslateRegularExpression(pattern);

            Regex r = new Regex(pattern);
            return MatchResult(r.Match(s, init));
        } // func match

        private static LuaResult MatchResult(Match m)
        {
            if (m.Success)
            {
                object[] result = new object[m.Captures.Count];

                for (int i = 0; i < m.Captures.Count; i++)
                    result[i] = m.Captures[i].Value;

                return result;
            }
            else
                return LuaResult.Empty;
        } // func MatchResult

        public static string rep(this string s, int n, string sep = "")
        {
            if (String.IsNullOrEmpty(s) || n == 0)
                return s;
            return String.Join(sep, Enumerable.Repeat(s, n));
        } // func rep

        public static string reverse(this string s)
        {
            if (String.IsNullOrEmpty(s) || s.Length == 1)
                return s;

            char[] a = s.ToCharArray();
            Array.Reverse(a);
            return new string(a);
        } // func reverse

        public static string upper(this string s)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            return s.ToUpper();
        } // func lower
    }
}
