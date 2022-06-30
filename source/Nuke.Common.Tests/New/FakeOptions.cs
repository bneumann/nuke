// ReSharper disable ArrangeMethodOrOperatorBody

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Tests;
using Nuke.Common.Utilities.Collections;

[PublicAPI]
[CommandOptions(CliType = typeof(FakeCli), Command = nameof(FakeCli.FakeWithArguments))]
public partial class FakeOptions : CliOptionsBuilder
{
    [Argument(Format = "--boolean {value}")] public bool? Boolean => GetScalar<bool?>(() => Boolean);
    [Argument(Format = "--flag")] public bool? Flag => GetScalar<bool?>(() => Flag);
    [Argument(Format = "--string {value}")] public string String => GetComplex<string>(() => String);
    [Argument(Format = "--integer {value}")] public int? Integer => GetScalar<int?>(() => Integer);
    [Argument(Format = "--logger {value}")] public IReadOnlyList<string> Lists => GetComplex<List<string>>(() => Lists);
    [Argument(Format = "/p:{key}={value}", AlternativeFormat = "/property:{key}={value}")] public IReadOnlyDictionary<string, int> Dictionaries => GetComplex<Dictionary<string, int>>(() => Dictionaries);
    [Argument(Format = "--lookup {key}={value}")] public ILookup<string, string> Lookups => GetComplex<LookupTable<string, string>>(() => Lookups);
}

[PublicAPI]
public static class FakeOptionsExtensions
{
    #region FakeOptions.Boolean
    public static T SetBoolean<T>(this T o, bool? value) where T : FakeOptions => o.Copy(b => b.Set(() => o.Boolean, value));
    public static T EnableBoolean<T>(this T o) where T : FakeOptions => o.Copy(b => b.Set(() => o.Boolean, true));
    public static T DisableBoolean<T>(this T o) where T : FakeOptions => o.Copy(b => b.Set(() => o.Boolean, false));
    public static T ResetBoolean<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Boolean));
    #endregion
    #region FakeOptions.Flag
    public static T SetFlag<T>(this T o, bool? value) where T : FakeOptions => o.Copy(b => b.Set(() => o.Flag, value));
    public static T EnableFlag<T>(this T o) where T : FakeOptions => o.Copy(b => b.Set(() => o.Flag, true));
    public static T DisableFlag<T>(this T o) where T : FakeOptions => o.Copy(b => b.Set(() => o.Flag, false));
    public static T ResetFlag<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Flag));
    #endregion
    #region FakeOptions.String
    public static T SetString<T>(this T o, string value) where T : FakeOptions => o.Copy(b => b.Set(() => o.String, value));
    public static T ResetString<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.String));
    #endregion
    #region FakeOptions.Integer
    public static T SetInteger<T>(this T o, int? value) where T : FakeOptions => o.Copy(b => b.Set(() => o.Integer, value));
    public static T ResetInteger<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Integer));
    #endregion
    #region FakeOptions.Lists
    public static T SetLists<T>(this T o, params string[] values) where T : FakeOptions => o.Copy(b => b.Set(() => o.Lists, values));
    public static T SetLists<T>(this T o, IEnumerable<string> values) where T : FakeOptions => o.Copy(b => b.Set(() => o.Lists, values));
    public static T AddLists<T>(this T o, params string[] values) where T : FakeOptions => o.Copy(b => b.AddCollection(() => o.Lists, values));
    public static T AddLists<T>(this T o, IEnumerable<string> values) where T : FakeOptions => o.Copy(b => b.AddCollection(() => o.Lists, values));
    public static T RemoveLists<T>(this T o, params string[] values) where T : FakeOptions => o.Copy(b => b.RemoveCollection(() => o.Lists, values));
    public static T RemoveLists<T>(this T o, IEnumerable<string> values) where T : FakeOptions => o.Copy(b => b.RemoveCollection(() => o.Lists, values));
    public static T ClearLists<T>(this T o) where T : FakeOptions => o.Copy(b => b.ClearCollection(() => o.Lists));
    public static T ResetLists<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Lists));
    #endregion
    #region FakeOptions.Dictionaries
    public static T SetDictionaries<T>(this T o, IReadOnlyDictionary<string, int> values) where T : FakeOptions => o.Copy(b => b.Set(() => o.Dictionaries, values));
    public static T SetDictionaries<T>(this T o, IDictionary<string, int> values) where T : FakeOptions => o.Copy(b => b.Set(() => o.Dictionaries, values));
    public static T AddDictionaries<T>(this T o, IReadOnlyDictionary<string, int> values) where T : FakeOptions => o.Copy(b => b.AddDictionary(() => o.Dictionaries, values));
    public static T AddDictionaries<T>(this T o, IDictionary<string, int> values) where T : FakeOptions => o.Copy(b => b.AddDictionary(() => o.Dictionaries, values));
    public static T AddDictionary<T>(this T o, string key, int value) where T : FakeOptions => o.Copy(b => b.AddDictionary(() => o.Dictionaries, key, value));
    public static T SetDictionary<T>(this T o, string key, int value) where T : FakeOptions => o.Copy(b => b.SetDictionary(() => o.Dictionaries, key, value));
    public static T RemoveDictionary<T>(this T o, string key) where T : FakeOptions => o.Copy(b => b.RemoveDictionary(() => o.Dictionaries, key));
    public static T ClearDictionaries<T>(this T o) where T : FakeOptions => o.Copy(b => b.ClearDictionary(() => o.Dictionaries));
    public static T ResetDictionaries<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Dictionaries));
    #endregion
    #region FakeOptions.Lookups
    public static T SetLookups<T>(this T o, ILookup<string, string> values) where T : FakeOptions => o.Copy(b => b.Set(() => o.Lookups, values));
    public static T AddLookup<T>(this T o, string key, params string[] values) where T : FakeOptions => o.Copy(b => b.AddLookup(() => o.Lookups, key, values));
    public static T AddLookup<T>(this T o, string key, IEnumerable<string> values) where T : FakeOptions => o.Copy(b => b.AddLookup(() => o.Lookups, key, values));
    public static T SetLookup<T>(this T o, string key, params string[] values) where T : FakeOptions => o.Copy(b => b.SetLookup(() => o.Lookups, key, values));
    public static T SetLookup<T>(this T o, string key, IEnumerable<string> values) where T : FakeOptions => o.Copy(b => b.SetLookup(() => o.Lookups, key, values));
    public static T RemoveLookup<T>(this T o, string key) where T : FakeOptions => o.Copy(b => b.AddLookup(() => o.Lookups, key));
    public static T RemoveLookup<T>(this T o, string key, string value) where T : FakeOptions => o.Copy(b => b.AddLookup(() => o.Lookups, key, value));
    public static T ClearLookups<T>(this T o) where T : FakeOptions => o.Copy(b => b.ClearLookup(() => o.Lookups));
    public static T ResetLookups<T>(this T o) where T : FakeOptions => o.Copy(b => b.Remove(() => o.Lookups));
    #endregion
}
