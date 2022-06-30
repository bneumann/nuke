// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using FluentAssertions;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Xunit;
using Xunit.Abstractions;

namespace Nuke.Common.Tests;

public class Settings2Test
{
    private readonly ITestOutputHelper _testOutputHelper;

    public Settings2Test(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void IntegerScalarTest()
    {
        var settings = new FakeOptions();

        settings = settings.SetInteger(1).Copy();
        settings.Integer.Should().Be(1);

        settings = settings.ResetInteger().Copy();
        settings.Integer.Should().BeNull();
    }

    [Fact]
    public void StringScalarTest()
    {
        var settings = new FakeOptions();

        settings = settings.SetString("foo").Copy();
        settings.String.Should().Be("foo");

        settings = settings.ResetString().Copy();
        settings.String.Should().BeNull();
    }

    [Fact]
    public void DictionaryTest()
    {
        var settings = new FakeOptions();
        var properties = new Dictionary<string, object>
        {
            {"foo", "bar"},
            {"baz", "qux"}
        };

        settings = settings.SetProperties(properties).Copy();
        settings.Properties.Should().BeEquivalentTo(properties);

        settings = settings.SetProperty("foo", "buzz").Copy();
        settings.Properties["foo"].Should().Be("buzz");

        settings = settings.RemoveProperty("baz").Copy();
        settings.Properties.Should().NotContainKey("baz");

        Action action = () => settings.AddProperty("foo", "existing");
        action.Should().Throw<Exception>();

        settings = settings.EnableSpecialProperty().Copy();
        settings.Properties["SpecialProperty"].As<bool>().Should().BeTrue();

        settings = settings.ClearProperties().Copy();
        settings.Properties.Should().BeEmpty();

        settings = settings.ResetProperties().Copy();
        settings.Properties.Should().BeNull();
    }

    [Fact]
    public void ListTest()
    {
        var settings = new FakeOptions();
        var flags = new List<BindingFlags>
        {
            BindingFlags.Static,
            BindingFlags.DeclaredOnly
        };

        settings = settings.SetFlags(flags).Copy();
        settings.Flags.Should().BeEquivalentTo(flags);

        settings = settings.AddFlag(BindingFlags.Instance).Copy();
        settings.Flags.Should().Contain(BindingFlags.Instance);

        settings = settings.RemoveFlag(BindingFlags.Static).Copy();
        settings.Flags.Should().NotContain(BindingFlags.Static);

        settings = settings.ClearFlags().Copy();
        settings.Flags.Should().BeEmpty();

        settings = settings.ResetFlags().Copy();
        settings.Flags.Should().BeNull();
    }

    [Fact]
    public void LookupTest()
    {
        var settings = new FakeOptions();
        var traits = new LookupTable<string, int>
        {
            ["foo"] = new[] { 1, 2, 3 },
            ["bar"] = new[] { 3, 4, 5 },
        };

        settings = settings.SetTraits(traits).Copy();
        settings.Traits.Should().BeEquivalentTo(traits);

        settings = settings.SetTrait("buzz", 1000).Copy();
        settings.Traits["buzz"].Should().Equal(1000);

        settings = settings.AddTrait("foo", 4, 5).Copy();
        settings.Traits["foo"].Should().Equal(1, 2, 3, 4, 5);

        settings = settings.RemoveTrait("foo", 2).Copy();
        settings.Traits["foo"].Should().Equal(1, 3, 4, 5);

        settings = settings.RemoveTrait("foo").Copy();
        settings.Traits["foo"].Should().BeEmpty();

        settings = settings.SetTrait("buzz", 9).Copy();
        settings.Traits["buzz"].Should().Equal(9);

        settings = settings.ClearTraits().Copy();
        settings.Traits.Should().BeEmpty();

        settings = settings.ResetTraits().Copy();
        settings.Traits.Should().BeNull();
    }

    [Fact]
    public void NestedTest()
    {
        var innerSettings = new FakeOptions();

        innerSettings = innerSettings.SetInteger(1).Copy();
        innerSettings.Integer.Should().Be(1);

        var settings = new FakeOptions();
        settings = settings.SetNested(innerSettings).Copy();

        settings.Nested.Integer.Should().Be(1);

        settings = settings.AddNestedList(new FakeOptions().SetInteger(1));
        settings = settings.AddNestedList(new FakeOptions().SetInteger(5));
    }

    [Fact]
    public void RenderTest()
    {
        var settings = new FakeOptions()
            .SetNoRestore(true)
            .SetInteger(5)
            .SetString("spacy value");

        var arguments = Processing.ParseArguments(settings);

        arguments.Should().Be("restore");
    }
}

class SecretAttribute : Attribute
{
}

class Processing
{
    public static string ParseArguments(CliOptionsBuilder options)
    {
        var commandOptionsAttribute = options.GetType().GetCustomAttribute<CommandOptionsAttribute>().NotNull();
        var commandMethod = commandOptionsAttribute.CliType.GetMethod(commandOptionsAttribute.Command).NotNull();
        var commandAttribute = commandMethod.GetCustomAttribute<CommandAttribute>().NotNull();
        var arguments = commandAttribute.Arguments;

        return new[]{arguments}.Concat(options.GetArguments()).JoinSpace();
    }

    public static (IReadOnlyCollection<Output> Output, int ExitCode) Execute(string toolPath, string arguments, int? timeout)
    {
        return ExecuteAsync(toolPath, arguments, captureStandardOutput: true, captureErrorOutput: true, timeout).GetAwaiter().GetResult();
    }

    public static async Task<(IReadOnlyCollection<Output> Output, int ExitCode)> ExecuteAsync(
        string toolPath,
        string arguments,
        bool captureStandardOutput,
        bool captureErrorOutput,
        int? timeout)
    {
        var output = new List<Output>();
        var exitCode = 0;
        var cmd = Cli.Wrap(toolPath)
            .WithArguments(arguments);

        var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource() : null;
        cancellationTokenSource?.CancelAfter(timeout.Value);
        var cancellationToken = cancellationTokenSource?.Token ?? default(CancellationToken);

        await foreach (var cmdEvent in cmd.ListenAsync(cancellationToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent std when captureStandardOutput:
                    output.Add(new Output { Type = OutputType.Std, Text = std.Text });
                    break;
                case StandardErrorCommandEvent err when captureErrorOutput:
                    output.Add(new Output { Type = OutputType.Std, Text = err.Text });
                    break;
                case ExitedCommandEvent exited:
                    exitCode = exited.ExitCode;
                    break;
            }
        }
        return (output, exitCode);
    }

    public static Command ExecuteCommand()
    {
        return null;
    }
}

// TODO: allow custom tool path using env var

// TODO: use Tool delegate for string based main command
