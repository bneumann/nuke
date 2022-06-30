// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Humanizer;
using Markdig;
using Newtonsoft.Json;
using Nuke.CodeGeneration.Model;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Nuke.Common.Tests;

public class Writer
{
    private readonly IndentedTextWriter _writer;

    public Writer(StringWriter writer)
    {
        _writer = new IndentedTextWriter(writer);
    }

    public IDisposable Indent()
    {
        return DelegateDisposable.CreateBracket(
            () => _writer.Indent++,
            () => _writer.Indent--);
    }

    public IDisposable WriteBlock()
    {
        return DelegateDisposable.CreateBracket(
                () => WriteLine("{"),
                () => WriteLine("}"))
            .CombineWith(Indent());
    }

    public Writer WriteLine()
    {
        _writer.WriteLine();
        return this;
    }

    public Writer WriteLineParts(params string[] parts)
    {
        return WriteLine(parts.Where(x => !x.IsNullOrWhiteSpace()).JoinSpace());
    }

    public Writer WriteLine(params string[] lines)
    {
        foreach (var line in lines)
        {
            if (line.IsNullOrWhiteSpace())
                continue;

            _writer.WriteLine(line.Trim());
        }

        return this;
    }

    public Writer WriteLine(IEnumerable<string> items, Func<string, string> formatter)
    {
        items.ForEach(x => WriteLine(formatter.Invoke(x)));
        return this;
    }
}


public class Generate
{
    private static AbsolutePath RootDirectory => Constants.TryGetRootDirectoryFrom(Directory.GetCurrentDirectory()).NotNull();

    [Fact]
    public void Test()
    {
        var yamlFile = RootDirectory / "source" / "Nuke.Common.Tests" / "New" / "Fake.options.yml";
        var reader = new StringReader(File.ReadAllText(yamlFile));
        var parser = new Parser(reader);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var options = new List<Option>();

        parser.Consume<StreamStart>();
        while (parser.Accept<DocumentStart>(out _))
        {
            options.Add(deserializer.Deserialize<Option>(parser));
        }

        var output = new StringWriter();
        var writer = new Writer(output);

        var cliType = "FakeCli";
        var commandName = "FakeWithArguments";
        var optionsType = "FakeOptions";
        var namespaces = new[]
                         {
                             "System.Collections.Generic",
                             "System.Collections.ObjectModel",
                             "System.Linq",
                             "JetBrains.Annotations",
                             "Nuke.Common.Tests",
                             "Nuke.Common.Utilities.Collections"
                         };

        writer.WriteLine("// ReSharper disable ArrangeMethodOrOperatorBody");
        writer.WriteLine();
        writer.WriteLine(namespaces, x => $"using {x};");
        writer.WriteLine();
        writer.WriteLine("[PublicAPI]");
        writer.WriteLine($"[CommandOptions(CliType = typeof({cliType}), Command = nameof({cliType}.{commandName}))]");
        writer.WriteLine($"public partial class {optionsType} : {nameof(CliOptionsBuilder)}");

        using (writer.WriteBlock())
        {
            foreach (var option in options)
            {
                // [Argument(Format = "--boolean {value}")]  public bool? Boolean => GetScalar<bool?>(() => Boolean);

                string GetAttribute()
                {
                    var properties = new Dictionary<string, string>()
                        .AddPairWhenValueNotNull(nameof(Option.Format), option.Format)
                        .AddPairWhenValueNotNull(nameof(Option.AlternativeFormat), option.AlternativeFormat)
                        .Select(x => $"{x.Key} = {x.Value.DoubleQuote()}")
                        .JoinCommaSpace();

                    return $"[Argument({properties})]";
                }

                var attribute = option.Format != null
                    ? GetAttribute()
                    : string.Empty;

                var getter = option.IsValueType()
                    ? "GetScalar"
                    : "GetComplex";

                writer.WriteLineParts(
                    attribute,
                    "public",
                    option.GetReturnType(),
                    option.Name,
                    "=>",
                    $"{getter}<{option.GetInternalType()}>(() => {option.Name});");
            }
        }

        writer.WriteLine();
        writer.WriteLine("[PublicAPI]");
        writer.WriteLine($"public static class {optionsType}Extensions");
        using (writer.WriteBlock())
        {
            // /// <summary><inheritdoc cref="FakeOptions.Integer"/></summary>
            // [OptionsModificator(OptionsType = typeof(FakeOptions), Property = nameof(FakeOptions.Integer))]
            // public static T SetInteger<T>(this T o, int? value) where T : FakeOptions => o.Copy(b => b.Set(() => o.Integer, value));

            foreach (var option in options)
            {
                IEnumerable<(string Extension, string Modification)> GetExtensionsWithModifications()
                {
                    if (option.Type != "list" && option.Type != "dictionary" && option.Type != "lookup")
                    {
                        yield return (
                            $"Set{option.Name}<T>(this T o, {option.GetReturnType()} value)",
                            $"Set(() => o.{option.Name}, value)");
                    }

                    var singular = option.Name.Singularize(inputIsKnownToBePlural: false);
                    switch (option.Type)
                    {
                        case "bool":
                            yield return (
                                $"Enable{option.Name}<T>(this T o)",
                                $"Set(() => o.{option.Name}, true)");
                            yield return (
                                $"Disable{option.Name}<T>(this T o)",
                                $"Set(() => o.{option.Name}, false)");
                            break;

                        case "list":
                            yield return (
                                $"Set{option.Name}<T>(this T o, params {option.Value}[] values)",
                                $"Set(() => o.{option.Name}, values)");
                            yield return (
                                $"Set{option.Name}<T>(this T o, IEnumerable<{option.Value}> values)",
                                $"Set(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{option.Name}<T>(this T o, params {option.Value}[] values)",
                                $"AddCollection(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{option.Name}<T>(this T o, IEnumerable<{option.Value}> values)",
                                $"AddCollection(() => o.{option.Name}, values)");
                            yield return (
                                $"Remove{option.Name}<T>(this T o, params {option.Value}[] values)",
                                $"RemoveCollection(() => o.{option.Name}, values)");
                            yield return (
                                $"Remove{option.Name}<T>(this T o, IEnumerable<{option.Value}> values)",
                                $"RemoveCollection(() => o.{option.Name}, values)");
                            yield return (
                                $"Clear{option.Name}<T>(this T o)",
                                $"ClearCollection(() => o.{option.Name})");
                            break;

                        case "dictionary":
                            yield return (
                                $"Set{option.Name}<T>(this T o, IReadOnlyDictionary<{option.Key}, {option.Value}> values)",
                                $"Set(() => o.{option.Name}, values)");
                            yield return (
                                $"Set{option.Name}<T>(this T o, IDictionary<{option.Key}, {option.Value}> values)",
                                $"Set(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{option.Name}<T>(this T o, IReadOnlyDictionary<{option.Key}, {option.Value}> values)",
                                $"AddDictionary(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{option.Name}<T>(this T o, IDictionary<{option.Key}, {option.Value}> values)",
                                $"AddDictionary(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{singular}<T>(this T o, {option.Key} key, {option.Value} value)",
                                $"AddDictionary(() => o.{option.Name}, key, value)");
                            yield return (
                                $"Set{singular}<T>(this T o, {option.Key} key, {option.Value} value)",
                                $"SetDictionary(() => o.{option.Name}, key, value)");
                            yield return (
                                $"Remove{singular}<T>(this T o, {option.Key} key)",
                                $"RemoveDictionary(() => o.{option.Name}, key)");
                            yield return (
                                $"Clear{option.Name}<T>(this T o)",
                                $"ClearDictionary(() => o.{option.Name})");
                            break;

                        case "lookup":
                            yield return (
                                $"Set{option.Name}<T>(this T o, ILookup<{option.Key}, {option.Value}> values)",
                                $"Set(() => o.{option.Name}, values)");
                            yield return (
                                $"Add{singular}<T>(this T o, {option.Key} key, params {option.Value}[] values)",
                                $"AddLookup(() => o.{option.Name}, key, values)");
                            yield return (
                                $"Add{singular}<T>(this T o, {option.Key} key, IEnumerable<{option.Value}> values)",
                                $"AddLookup(() => o.{option.Name}, key, values)");
                            yield return (
                                $"Set{singular}<T>(this T o, {option.Key} key, params {option.Value}[] values)",
                                $"SetLookup(() => o.{option.Name}, key, values)");
                            yield return (
                                $"Set{singular}<T>(this T o, {option.Key} key, IEnumerable<{option.Value}> values)",
                                $"SetLookup(() => o.{option.Name}, key, values)");
                            yield return (
                                $"Remove{singular}<T>(this T o, {option.Key} key)",
                                $"AddLookup(() => o.{option.Name}, key)");
                            yield return (
                                $"Remove{singular}<T>(this T o, {option.Key} key, {option.Value} value)",
                                $"AddLookup(() => o.{option.Name}, key, value)");
                            yield return (
                                $"Clear{option.Name}<T>(this T o)",
                                $"ClearLookup(() => o.{option.Name})");
                            break;
                    }

                    yield return (
                        $"Reset{option.Name}<T>(this T o)",
                        $"Remove(() => o.{option.Name})");
                }

                writer.WriteLine($"#region {optionsType}.{option.Name}");
                foreach (var (extension, modification) in GetExtensionsWithModifications())
                {
                    // writer.WriteLine($"/// <summary>{option.GetSummary()}</summary>");
                    // writer.WriteLine($"[OptionsModificator(OptionsType = typeof({optionsType}), Property = nameof({optionsType}.{option.Name}))]");
                    writer.WriteLineParts($"public static T {extension} where T : FakeOptions => o.Copy(b => b.{modification});");
                }
                writer.WriteLine("#endregion");
            }
        }

        File.WriteAllText(yamlFile.Parent / "FakeOptions.cs", output.ToString());
    }
}

public class Option
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string Format { get; set; }
    public string AlternativeFormat { get; set; }
    public string Description { get; set; }
}

public static class OptionsExtensions
{
    public static bool IsValueType(this Option option)
    {
        return option.Type.EqualsAnyOrdinalIgnoreCase(
            "int",
            "bool",
            "sbyte",
            "short",
            "long",
            "byte",
            "ushort",
            "uint",
            "ulong",
            "float",
            "double",
            "char",
            "decimal");
    }

    public static string GetNullableType(this Option option)
    {
        return option.IsValueType() ? option.Type + "?" : option.Type.Trim('!');
    }

    public static string GetInternalType(this Option option)
    {
        return option.Type switch
        {
            "list" => $"List<{option.Value}>",
            "dictionary" => $"Dictionary<{option.Key}, {option.Value}>",
            "lookup" => $"LookupTable<{option.Key}, {option.Value}>",
            _ => option.GetNullableType()
        };
    }

    public static string GetReturnType(this Option option)
    {
        return option.Type switch
        {
            "list" => $"IReadOnlyList<{option.Value}>",
            "dictionary" => $"IReadOnlyDictionary<{option.Key}, {option.Value}>",
            "lookup" => $"ILookup<{option.Key}, {option.Value}>",
            _ => option.GetNullableType()
        };
    }

    public static string GetSummary(this Option option)
    {
        // https://learn-the-web.algonquindesign.ca/topics/markdown-yaml-cheat-sheet/
        return Markdown.ToHtml(option.Description)
            .Replace("code>", "c>")
            .Replace(Environment.NewLine, string.Empty)
            .Trim();
    }
}
