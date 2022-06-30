// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Text;
using Nuke.Common.Utilities;

namespace Nuke.Common.Tests;

public class CliOptionsBuilder : OptionsBuilder
{
    internal List<string> Secrets = new();

    internal IEnumerable<string> GetArguments()
    {
        var arguments = new List<string>();
        //
        var tokenAndProperties = InternalOptions.Properties()
            .Select(x => (Token: x.Value, GetType().GetProperty(x.Name).NotNull()));
        foreach (var (token, property) in tokenAndProperties)
        {
            var argumentAttribute = property.GetCustomAttribute<ArgumentAttribute>();
            if (argumentAttribute == null)
                continue;

            var format = argumentAttribute.Format;
            if (format.Contains(" "))
            {
                var formatSplit = format.Split(" ");
                Assert.True(formatSplit.Length == 2);
                yield return formatSplit.First();
                format = formatSplit.Last();
            }

            if (property.PropertyType == typeof(bool?) &&
                !format.ContainsOrdinalIgnoreCase("{value}"))
            {
                if (token.Value<bool>())
                    yield return format;

                continue;
            }

            yield return format.Replace("{value}", token.Value<string>());
        }
    }
}
