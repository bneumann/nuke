// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;

namespace Nuke.Common.Tests;

/// <summary>Marks a class as CLI tool wrapper.</summary>
internal class CliAttribute : Attribute
{
    /// <summary>Defines the executable to invoke.</summary>
    public string Executable { get; set; }
    /// <summary>Defines the necessary NuGet package.</summary>
    public string PackageId { get; set; }
    /// <summary>Defines whether the NuGet package requires a <c>Framework</c> to be set.</summary>
    public bool? RequiresFramework { get; set; }
    /// <summary>Defines implicit arguments for the CLI tool wrapper.</summary>
    public string Arguments { get; set; }
}

/// <summary>Marks a method as CLI command wrapper.</summary>
internal class CommandAttribute : Attribute
{
    /// <summary>Defines the mandatory arguments for the CLI command.</summary>
    public string Arguments { get; set; }
    /// <summary>Defines the <see cref="CliOptionsBuilder"/> type related to this command.</summary>
    public Type OptionsType { get; set; } // Allows adding the command
}

internal class CommandOptionsAttribute : Attribute
{
    /// <summary>Defines the type marked with <see cref="CliAttribute"/> related to this <see cref="CliOptionsBuilder"/>.</summary>
    public Type CliType { get; set; }
    /// <summary>Defines the method marked with <see cref="CommandAttribute"/> related to this <see cref="CliOptionsBuilder"/>.</summary>
    public string Command { get; set; }
}

/// <summary>Marks a property </summary>
internal class ArgumentAttribute : Attribute
{
    public int? Position { get; set; }
    public string Format { get; set; }
    public string AlternativeFormat { get; set; }
    public bool? IsSecret { get; set; }
    public string FormatterMethod { get; set; }
    public Type FormatterType { get; set; }
    public string CollectionSeparator { get; set; }
    public string Container { get; set; }
}

/// <summary>Marks an extension method as modificator of an <see cref="OptionsBuilder"/> instance.</summary>
public class OptionsModificatorAttribute : Attribute
{
    /// <summary>Defines the specific <see cref="OptionsBuilder"/> type.</summary>
    public Type OptionsType { get; set; }
    /// <summary>Defines the property name of the <see cref="OptionsType"/> that is being modified.</summary>
    public string Property { get; set; }
}
