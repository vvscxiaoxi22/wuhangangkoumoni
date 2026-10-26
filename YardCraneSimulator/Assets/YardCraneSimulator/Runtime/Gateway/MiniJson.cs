using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PortTraining.YardCraneSimulator.Gateway
{
    // Minimal, dependency-free JSON reader/writer used only to speak PortGatewayService's
    // realtime-pipe wire protocol for debug/test purposes. Not a general-purpose JSON
    // library. Written here because this Unity project has neither Newtonsoft.Json nor
    // System.Text.Json available (checked: absent from Packages/manifest.json,
    // apiCompatibilityLevel=NetStandard has no bundled System.Text.Json), and V1.1
    // section 4 explicitly forbids UnityEngine.JsonUtility for cross-software protocol
    // messages ("跨软件协议不得使用Unity JsonUtility直接序列化"). This is a genuine, zero-
    // dependency stand-in for the PortTraining.Contracts package the standard calls for,
    // which does not exist yet.
    public static class MiniJsonReader
    {
        public static object Parse(string json)
        {
            var index = 0;
            var value = ParseValue(json, ref index);
            return value;
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) throw new FormatException("Unexpected end of JSON input.");

            switch (json[index])
            {
                case '{': return ParseObject(json, ref index);
                case '[': return ParseArray(json, ref index);
                case '"': return ParseString(json, ref index);
                case 't':
                    Expect(json, ref index, "true");
                    return true;
                case 'f':
                    Expect(json, ref index, "false");
                    return false;
                case 'n':
                    Expect(json, ref index, "null");
                    return null;
                default:
                    return ParseNumber(json, ref index);
            }
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            index++; // consume '{'
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                var key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') throw new FormatException("Expected ':' in JSON object.");
                index++;
                var value = ParseValue(json, ref index);
                result[key] = value;
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("Unexpected end of JSON object.");
                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                {
                    index++;
                    break;
                }

                throw new FormatException("Expected ',' or '}' in JSON object.");
            }

            return result;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var result = new List<object>();
            index++; // consume '['
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return result;
            }

            while (true)
            {
                var value = ParseValue(json, ref index);
                result.Add(value);
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("Unexpected end of JSON array.");
                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == ']')
                {
                    index++;
                    break;
                }

                throw new FormatException("Expected ',' or ']' in JSON array.");
            }

            return result;
        }

        private static string ParseString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"') throw new FormatException("Expected '\"' to start a JSON string.");
            index++;
            var builder = new StringBuilder();
            while (true)
            {
                if (index >= json.Length) throw new FormatException("Unexpected end of JSON string.");
                var character = json[index];
                if (character == '"')
                {
                    index++;
                    break;
                }

                if (character == '\\')
                {
                    index++;
                    if (index >= json.Length) throw new FormatException("Unexpected end of JSON escape sequence.");
                    var escape = json[index];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            var hex = json.Substring(index + 1, 4);
                            builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default:
                            throw new FormatException($"Unsupported JSON escape sequence '\\{escape}'.");
                    }

                    index++;
                }
                else
                {
                    builder.Append(character);
                    index++;
                }
            }

            return builder.ToString();
        }

        private static double ParseNumber(string json, ref int index)
        {
            var start = index;
            while (index < json.Length &&
                   (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '+' ||
                    json[index] == '.' || json[index] == 'e' || json[index] == 'E'))
            {
                index++;
            }

            if (index == start) throw new FormatException($"Unexpected character '{json[index]}' in JSON input.");

            var slice = json.Substring(start, index - start);
            return double.Parse(slice, CultureInfo.InvariantCulture);
        }

        private static void Expect(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length || string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"Expected literal '{literal}' in JSON input.");
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }
    }

    // Companion writer - only supports what this debug client needs to send (a flat
    // object of string/number values), not general JSON serialization.
    public static class MiniJsonWriter
    {
        public static string EscapeString(string value)
        {
            if (value == null) return "null";
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
