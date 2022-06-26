﻿// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.ValueInjection;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Nuke.Common;

public static class TargetDefinitionExtensions
{
    private static readonly AbsolutePath WindowsBuildDirectory = (AbsolutePath)@"C:\nuke";
    private static readonly AbsolutePath UnixBuildDirectory = (AbsolutePath)@"/nuke";

    /// <summary>
    /// Execute this target within a Docker container
    /// </summary>
    public static ITargetDefinition DockerRun(this ITargetDefinition targetDefinition, Configure<DockerRunTargetSettings> configurator)
    {
        var definition = (TargetDefinition)targetDefinition;
        definition.Intercept = () =>
        {
            if (NukeBuild.IsInterceptorExecution)
                return false;

            var settings = configurator.InvokeSafe(new DockerRunTargetSettings());
            Assert.NotNull(settings.DotNetRuntime);
            Assert.NotNull(settings.Platform);

            var buildAssemblyDirectory = NukeBuild.BuildAssemblyDirectory.NotNull().Parent / settings.DotNetRuntime;
            var buildAssembly = buildAssemblyDirectory / NukeBuild.BuildAssemblyFile.NotNull().NameWithoutExtension;

            FileSystemTasks.EnsureCleanDirectory(buildAssemblyDirectory);

            Log.Information("Preparing build executable for {DotNetRuntime}...", $".NET {settings.DotNetRuntime}");
            DotNetPublish(p => p
                .SetProject(NukeBuild.BuildProjectFile)
                .SetOutput(buildAssemblyDirectory)
                .SetVerbosity(DotNetVerbosity.Quiet)
                .EnableNoLogo()
                .SetRuntime(settings.DotNetRuntime)
                .EnableSelfContained()
                .DisableProcessLogInvocation()
                .DisableProcessLogOutput());

            // TODO: how should PullImage work?
            try
            {
                Docker($"image inspect {settings.Image}", logInvocation: false, logOutput: false);
            }
            catch
            {
                Log.Information("Pulling image {Image}...", settings.Image);
                Docker($"pull {settings.Image}", logInvocation: false, logOutput: false);
            }

            var workingDirectory = settings.Platform.StartsWithOrdinalIgnoreCase("win") ? WindowsBuildDirectory : UnixBuildDirectory;
            var envFile = buildAssemblyDirectory / $".env.{definition.Target.Name}";
            File.WriteAllLines(envFile, GetEnvironmentVariables(workingDirectory).Select(x => $"{x.Key}={x.Value}"));

            Log.Information("Launching target in {Image}...", settings.Image);

            DockerTasks.DockerRun(_ => settings
                // Allow option to --rm after build
                .EnableRm()
                .AddVolume($"{NukeBuild.RootDirectory}:{workingDirectory}")
                .AddVolume($"{NuGetPackageResolver.GetPackagesDirectory(ToolPathResolver.NuGetPackagesConfigFile)}:/nuget")
                .SetPlatform(settings.Platform)
                .SetWorkdir(workingDirectory)
                .SetEnvFile(envFile)
                .SetEntrypoint(workingDirectory / NukeBuild.RootDirectory.GetRelativePathTo(buildAssembly))
                .SetArgs(new[]
                         {
                             definition.Target.Name,
                             $"--{ParameterService.GetParameterDashedName(Constants.SkippedTargetsParameterName)}"
                         }.Concat(settings.Args)));

            return true;
        };

        return definition;
    }

    private static IReadOnlyDictionary<string, string> GetEnvironmentVariables(AbsolutePath workingDirectory)
    {
        return new Dictionary<string, string>()
            .AddPair(Constants.InterceptorEnvironmentKey, value: 1)
            .AddPairWhenValueNotNull("TERM", Logging.SupportsAnsiOutput ? "xterm" : null)
            .AddPair("NUGET_PACKAGES", "/nuget")
            .AddPair("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", value: 1)
            .AddPair("DOTNET_CLI_TELEMETRY_OPTOUT", value: 1)
            // TODO: what's this?
            .AddPair("DOTNET_CLI_HOME", workingDirectory)
            // TODO: sure this needs to be set?
            .AddPair("TEMP", workingDirectory / NukeBuild.RootDirectory.GetRelativePathTo(NukeBuild.TemporaryDirectory))
            .AddPair("TMP", workingDirectory / NukeBuild.RootDirectory.GetRelativePathTo(NukeBuild.TemporaryDirectory))
            // Otherwise: Failed to create CoreCLR, HRESULT: 0x80004005
            // https://github.com/actions/runner/issues/619
            .AddPair("COMPlus_EnableDiagnostics", value: 0)
            .AddDictionary(EnvironmentInfo.Variables
                .Where(x =>
                    !x.Key.Contains(' ') &&
                    x.Key.EqualsAnyOrdinalIgnoreCase(
                        "USERPROFILE",
                        "USERNAME",
                        "LOCALAPPDATA",
                        "APPDATA",
                        "TEMP",
                        "TMP",
                        "HOMEPATH"
                    ))
                .ToDictionary(x => x.Key, x => x.Value).AsReadOnly()).AsReadOnly();
    }
}

[PublicAPI]
[ExcludeFromCodeCoverage]
[Serializable]
public class DockerRunTargetSettings : DockerRunSettings
{
    //todo: mattr: consider supporting credentials for the docker feed
    //todo: mattr: consider supporting additional env vars
    //todo: mattr: consider supporting a "dont pass any env vars" mode
    //todo: mattr: consider if there's a solid use case for "args""
    //todo: mattr: consider if we need entrypoint? 

    /// <summary>
    /// Whether to execute a `docker pull` before running the container. 
    /// </summary>
    public virtual bool PullImage { get; internal set; }

    /// <summary>
    /// The .NET Runtime Identifier (<see ref="https://docs.microsoft.com/en-us/dotnet/core/rid-catalog">RID</see>) to use to publish the Nuke project.
    /// For example, `linux-x64`, `linux-arm64`, `win-x64` etc.
    /// </summary>
    public virtual string DotNetRuntime { get; internal set; }

    [Pure]
    public DockerRunTargetSettings SetPullImage(bool pullImage)
    {
        var settings = this.NewInstance();
        settings.PullImage = pullImage;
        return settings;
    }

    [Pure]
    public DockerRunTargetSettings SetDotNetRuntime(string rid)
    {
        var settings = this.NewInstance();
        settings.DotNetRuntime = rid;
        return settings;
    }
}
