﻿using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class AcStringValues {
        private static Regex _idYearRegex;

        private static Regex IdYearRegex => _idYearRegex ??
                                            (_idYearRegex = new Regex(@"(?<![a-zA-Z0-9])(?:19[2-9]|20[01])\d$", RegexOptions.Compiled));

        public static int? GetYearFromId([NotNull] string id) {
            var result = IdYearRegex.Match(id);
            if (!result.Success) return null;
            return int.Parse(result.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        private static Regex _nameYearRegex;

        private static Regex NameYearRegex => _nameYearRegex ??
                                              (_nameYearRegex = new Regex(@"\s(?:['’](\d\d))$", RegexOptions.Compiled));

        public static int? GetYearFromName([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0 || !char.IsDigit(name[name.Length - 1])) return null;

            var result = NameYearRegex.Match(name);
            if (!result.Success) return null;

            var matched = result.Groups[1].Value;
            if (string.IsNullOrEmpty(matched)) matched = result.Groups[2].Value;
            var value = int.Parse(matched, NumberStyles.Any, CultureInfo.InvariantCulture);
            return value < 1000 ? value < 18 ? 2000 + value : 1900 + value : value;
        }

        [NotNull]
        public static string NameReplaceYear([NotNull] string name, int year) {
            return NameYearRegex.Replace(name, $" '{year % 100:D2}", 1);
        }

        private static Regex _nameVersionRegex;

        private static Regex NameVersionRegex => _nameVersionRegex ??
                                              (_nameVersionRegex = new Regex(@"^(.+?) (?:[vV](\d+\.\d[\w\.]*))$", RegexOptions.Compiled));

        public static string GetVersionFromName([NotNull] string name, out string nameWithoutVersion) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var match = NameVersionRegex.Match(name);
            if (match.Success) {
                nameWithoutVersion = match.Groups[1].Value;
                return match.Groups[2].Value;
            }

            nameWithoutVersion = name;
            return null;
        }

        [CanBeNull]
        public static string BrandFromName([NotNull] string name) {
            var key = name.Trim().ToLower();
            return (from brand in DataProvider.Instance.BrandCountries.Keys
                    where key.StartsWith(brand) && (char.IsWhiteSpace(key.ElementAtOrDefault(brand.Length)) || key.ElementAtOrDefault(brand.Length) == '_')
                    select name.Trim().Substring(0, brand.Length)).FirstOrDefault();
        }

        [CanBeNull]
        public static string CountryFromBrand([NotNull] string brand) {
            var key = brand.Trim().ToLower();
            return DataProvider.Instance.BrandCountries.GetValueOrDefault(key);
        }

        [CanBeNull]
        public static string CountryFromTag([NotNull] string tag) {
            var key = tag.ToLower();

            var firstPass = Pass(key);
            if (firstPass != null) return firstPass;

            if (key.IndexOfAny(new[]{ '(', ',', ';' }) != -1) {
                var pieces = key.Split(new[] { '(', ')', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = pieces.Length - 1; i >= 0; i--) {
                    var extraPass = Pass(pieces[i]);
                    if (extraPass != null) return extraPass;
                }
            }

            return null;

            string Pass(string k) {
                k = k.Trim();
                return DataProvider.Instance.TagCountries.GetValueOrDefault(k) ?? DataProvider.Instance.Countries.GetValueOrDefault(k);
            }
        }

        [CanBeNull]
        public static string GetCountryId([NotNull] string countryNameOrTag) {
            return DataProvider.Instance.CountryToIds.GetValueOrDefault(CountryFromTag(countryNameOrTag) ?? countryNameOrTag);
        }

        [CanBeNull]
        public static string GetCountryFromId([NotNull] string countryId) {
            return DataProvider.Instance.CountryToIds.FirstOrDefault(x => string.Equals(x.Value, countryId, StringComparison.OrdinalIgnoreCase)).Key;
        }

        private static readonly Regex SplitWordsRegex = new Regex(@"[\s_]+|(?<=[a-z])-?(?=[A-Z])|(?<=[a-z]{2})-?(?=[A-Z\d])", RegexOptions.Compiled);
        private static readonly Regex RejoinRegex = new Regex(@"(?<=\bgt) (?=[234]\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UpperRegex = new Regex(@"\b[a-z]", RegexOptions.Compiled);

        private static readonly Regex UpperAcronimRegex = new Regex(@"\b(?:
                [234]d|a(?:i|c(?:c|fl)?|mg)|b(?:r|mw)|c(?:cgt|pu|sl|ts|tr\d*)|d(?:mc|rs|s3|tm)|
                g(?:gt|pu|rm|sr|t[\dbceirsx]?\d?)|f(?:[ox]|fb)|
                h(?:dr|ks|rt)|ita|jtc|ks(?:drift)?|lms?|
                m(?:c12|gu|p\d*)|n(?:a|vidia)|rs[\drs]?|s(?:[lm]|r8)|qv|v[dr]c|wrc)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        [NotNull]
        public static string NameFromId([NotNull] string id) {
            return NameFromId(id, true);
        }

        [NotNull]
        public static string NameFromId([NotNull] string id, bool titleCase) {
            id = SplitWordsRegex.Replace(id, " ");
            id = RejoinRegex.Replace(id, "");
            id = UpperAcronimRegex.Replace(id, x => x.Value.ToUpper());
            if (titleCase) {
                id = UpperRegex.Replace(id, x => x.Value.ToUpper());
            } else {
                id = id.Length > 0 ? id.Substring(0, 1).ToUpper() + id.Substring(1) : string.Empty;
            }
            return id;
        }

        [NotNull]
        public static string IdFromName([NotNull] string name) {
            name = Regex.Replace(name, @"\W+", "_");
            return name.ToLower();
        }

        private static Regex _decodeDescriptionRegex, _cleanDescriptionRegex;

        /// <summary>
        /// Converts HTML-like description to a normal one. We don’t need any YouTube-players built-in it.
        /// </summary>
        // TODO: Add support for some tags which could be drawn using MUI bb-codes?
        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static string DecodeDescription([CanBeNull] string s) {
            if (s == null) return null;

            if (_decodeDescriptionRegex == null) {
                _decodeDescriptionRegex = new Regex(@"<\s*/?\s*br\s*/?\s*>\s*", RegexOptions.Compiled);
                _cleanDescriptionRegex = new Regex(@"<\s*\w+(\s+[^>]*|\s*)>|</\s*\w+\s*>|\t", RegexOptions.Compiled);
            }

            s = _decodeDescriptionRegex.Replace(s, "\n");
            s = _cleanDescriptionRegex.Replace(s, "");
            return HttpUtility.HtmlDecode(s).Trim();
        }

        [CanBeNull]
        public static string EncodeDescription([CanBeNull] string s) {
            return s == null ? null : HttpUtility.HtmlEncode(s).Trim().Replace("\r", "").Replace("\n", "<br>");
        }

        public static bool IsAppropriateId([CanBeNull] string id) {
            return id != null && Regex.IsMatch(id, @"^\w[\w-]*$");
        }
    }
}
