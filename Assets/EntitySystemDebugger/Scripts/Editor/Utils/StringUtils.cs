using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Utils
{
    public static class StringUtils
    {

        static bool prevIsNumber = false;
        static bool prevIsUpper = false;

        public static string Humanize (string field, int maxChars = -1)
        {
            // If there are 0 or 1 characters, just return the string.
            if (field == null) return field;
            if (field.Length < 2) return field.ToUpper ();

            // Start with the first character.
            string result = field.Substring (0, 1).ToUpper ();

            // Add the remaining characters.
            for (int i = 1; i < field.Length; i++)
            {
                var isUpper = char.IsUpper (field[i]);
                var isNumber = char.IsNumber (field[i]);
                if (
                    (isUpper || isNumber) &&
                    (
                        (i + 1 >= field.Length || char.IsLower (field[i + 1])) ||
                        (!prevIsNumber && !prevIsUpper)
                    )
                )
                {
                    result += " ";
                }

                prevIsUpper = isUpper;
                prevIsNumber = isNumber;

                result += field[i];
            }

            if (maxChars > -1 && result.Length > maxChars)
            {
                return result.Remove (maxChars) + "...";
            }

            return result;
        }

        public static string FormatDt (string title, float value)
        {
            if (title != "")
            {
                title += " ";
            }
            return string.Format (title + "{0:f2} ms", Mathf.Round (value * 100) / 100);
        }

        public static string GetAbbreviation (string name)
        {
            var str = "";
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper (name[i]) || char.IsNumber (name[i]))
                {
                    str += name[i];
                }
            }
            return str;
        }

        public static string ValidClassName (string name)
        {
            name = StripNonAlphanumDot (name);
            CSharpCodeProvider codeProvider = new CSharpCodeProvider ();
            return codeProvider.CreateValidIdentifier (name);
        }

        public static string CreateFileName (string name)
        {
            name = StripNonAlphanumDot (name);
            if (!Char.IsUpper (name, 0))
            {
                TextInfo textInfo = new CultureInfo ("en-US", false).TextInfo;
                name = textInfo.ToTitleCase (name);
            }
            var invalids = System.IO.Path.GetInvalidFileNameChars ();
            return String.Join ("_", name.Split (invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd ('.');
        }

        public static string StripNonAlphanumDot (string name)
        {
            Regex nonAlphanum = new Regex ("[^a-zA-Z0-9.]");
            name = nonAlphanum.Replace (name, "");
            return name;
        }
    }
}