// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.IO;
using Newtonsoft.Json;
using Nuke.CodeGeneration.Model;
using Nuke.Common.IO;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Nuke.Common.Tests;

public class Convert
{
    private static AbsolutePath RootDirectory => Constants.TryGetRootDirectoryFrom(Directory.GetCurrentDirectory()).NotNull();

    [Fact]
    public void Test()
    {
        var dotnetJson = RootDirectory / "source" / "Nuke.Common" / "Tools" / "DotNet" / "DotNet.json";

        var content = File.ReadAllText(dotnetJson);
        var json = JsonConvert.DeserializeObject<Tool>(content);
    }
}
