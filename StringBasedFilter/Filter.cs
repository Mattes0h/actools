﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter {
    /// <summary>
    /// Typed version with fixed ITester.
    /// </summary>
    public class Filter<T> : Filter, IFilter<T> {
        private readonly ITester<T> _tester;

        public Filter(ITester<T> tester, string filter, FilterParams filterParams) : base(filter, filterParams) {
            _tester = tester;
        }

        public bool Test(T obj) {
            return Test(_tester, obj);
        }

        /// <summary>
        /// Checks if filter depends on specific property. Especially useful for NotifyPropertyChanged
        /// stuff.
        /// </summary>
        /// <param name="propertyName">Name of property</param>
        /// <returns>True if filtering depends</returns>
        public bool IsAffectedBy(string propertyName) {
            return IsAffectedBy(_tester, propertyName);
        }

        private class FilterAny : IFilter<T> {
            public string Source => "*";

            public bool Test(T obj) {
                return true;
            }

            public bool IsAffectedBy(string propertyName) {
                return false;
            }
        }

        private static readonly Lazy<IFilter<T>> AnyLazy = new Lazy<IFilter<T>>(() => new FilterAny());
        public static IFilter<T> Any => AnyLazy.Value;
    }

    /// <summary>
    /// Untyped version with variable ITester.
    /// </summary>
    public class Filter : IFilter {
        // some of users just can’t be bothered to spend a bit of time
        // to read how the whole system works, so here you go
        public static bool OptionSimpleMatching = false;

        [NotNull]
        public static IFilter<T> Create<T>([NotNull] ITester<T> tester, [NotNull, Localizable(false)] string filter, bool strictMode) {
            return new Filter<T>(tester, filter, new FilterParams {
                StringMatchMode = strictMode ? StringMatchMode.StartsWith : StringMatchMode.IncludedWithin
            });
        }

        [NotNull]
        public static IFilter<T> Create<T>([NotNull] ITester<T> tester, [NotNull, Localizable(false)] string filter, FilterParams filterParams = null) {
            return new Filter<T>(tester, filter, filterParams);
        }

        /// <summary>
        /// Untyped version with variable ITester.
        /// </summary>
        [NotNull]
        public static IFilter Create([NotNull, Localizable(false)] string filter, bool strictMode) {
            return new Filter(filter, new FilterParams {
                StringMatchMode = strictMode ? StringMatchMode.StartsWith : StringMatchMode.IncludedWithin
            });
        }

        /// <summary>
        /// Untyped version with variable ITester.
        /// </summary>
        [NotNull]
        public static IFilter Create([NotNull, Localizable(false)] string filter, FilterParams filterParams = null) {
            return new Filter(filter, filterParams);
        }

        [NotNull]
        public static string Encode([CanBeNull]string s) {
            if (s == null) return string.Empty;

            var b = new StringBuilder(s.Length + 5);
            for (var i = 0; i < s.Length; i++) {
                switch (s[i]) {
                    case '(':
                    case ')':
                    case '&':
                    case '!':
                    case ',':
                    case '|':
                    case '\\':
                    case '^':
                        b.Append('\\');
                        break;
                }

                b.Append(s[i]);
            }

            return b.ToString();
        }

        private readonly string[] _keys;
        private readonly FilterTreeNode _testTree;
        private string[] _properties;

        public string Source { get; }

        internal Filter(string filter, FilterParams filterParams) {
            Source = filter;
            _testTree = new FilterParser(filterParams).Parse(filter, out _keys);
        }

        internal Filter(FilterTreeNode tree) {
            _testTree = tree;
            _keys = new string[0];
        }

        public override string ToString() {
            return _testTree.ToString();
        }

        public void ResetProperties() {
            _properties = null;
        }

        public bool IsAffectedBy<T>(ITester<T> tester, string propertyName) {
            if (_properties == null) {
                _properties = _keys.Select(tester.ParameterFromKey).ToArray();
            }

            return _properties.Contains(propertyName);
        }

        public bool Test<T>(ITester<T> tester, T obj) {
            return _testTree.Test(tester, obj);
        }
    }
}
