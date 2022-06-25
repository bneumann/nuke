﻿// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Constants;

namespace Nuke.Common
{
    public abstract partial class NukeBuild
    {
        static NukeBuild()
        {
            RootDirectory = GetRootDirectory();
            TemporaryDirectory = GetTemporaryDirectory(RootDirectory);
            FileSystemTasks.EnsureExistingDirectory(TemporaryDirectory);

            BuildAssemblyFile = GetBuildAssemblyFile();
            BuildAssemblyDirectory = BuildAssemblyFile?.Parent;

            BuildProjectFile = GetBuildProjectFile(BuildAssemblyDirectory);
            BuildProjectDirectory = BuildProjectFile?.Parent;

            Verbosity = EnvironmentInfo.GetParameter<Verbosity?>(() => Verbosity) ?? Verbosity.Normal;
            Host = EnvironmentInfo.GetParameter(() => Host) ?? Host.Default;
            LoadedLocalProfiles = EnvironmentInfo.GetParameter(() => LoadedLocalProfiles) ?? new string[0];
        }

        /// <summary>
        /// Gets the full path to the root directory.
        /// </summary>
        [Parameter("Root directory during build execution.", Name = RootDirectoryParameterName)]
        public static AbsolutePath RootDirectory { get; }

        /// <summary>
        /// Gets the full path to the temporary directory <c>/.nuke/temp</c>.
        /// </summary>
        public static AbsolutePath TemporaryDirectory { get; }

        /// <summary>
        /// Gets the full path to the build assembly file.
        /// </summary>
        [CanBeNull]
        public static AbsolutePath BuildAssemblyFile { get; }

        /// <summary>
        /// Gets the full path to the build assembly directory.
        /// </summary>
        [CanBeNull]
        public static AbsolutePath BuildAssemblyDirectory { get; }

        /// <summary>
        /// Gets the full path to the build project directory, or <c>null</c>
        /// </summary>
        [CanBeNull]
        public static AbsolutePath BuildProjectDirectory { get; }

        /// <summary>
        /// Gets the full path to the build project file, or <c>null</c>
        /// </summary>
        [CanBeNull]
        public static AbsolutePath BuildProjectFile { get; }

        /// <summary>
        /// Gets the logging verbosity during build execution. Default is <see cref="Nuke.Common.Verbosity.Normal"/>.
        /// </summary>
        [Parameter("Logging verbosity during build execution. Default is 'Normal'.")]
        public static Verbosity Verbosity
        {
            get => (Verbosity) Logging.Level;
            set => Logging.Level = (LogLevel) value;
        }

        /// <summary>
        /// Gets the host for execution. Default is <em>automatic</em>.
        /// </summary>
        [Parameter("Host for execution. Default is 'automatic'.", ValueProviderMember = nameof(HostNames))]
        public static Host Host { get; internal set; }

        [Parameter("Defines the profiles to load.", Name = LoadedLocalProfilesParameterName)]
        public static string[] LoadedLocalProfiles { get; }

        public static bool IsLocalBuild => !IsServerBuild;
        public static bool IsServerBuild => Host is IBuildServer;

        internal static bool IsLocalAndEntryExecution => IsLocalBuild && !IsInterceptorExecution;

        private static AbsolutePath GetRootDirectory()
        {
            var parameterValue = EnvironmentInfo.GetParameter(() => RootDirectory);
            if (parameterValue != null)
                return parameterValue;

            if (EnvironmentInfo.GetParameter<bool>(() => RootDirectory))
                return EnvironmentInfo.WorkingDirectory;

            return TryGetRootDirectoryFrom(EnvironmentInfo.WorkingDirectory)
                .NotNull(new[]
                         {
                             $"Could not locate '{NukeDirectoryName}' directory/file while walking up from '{EnvironmentInfo.WorkingDirectory}'.",
                             "Either create a directory/file to mark the root directory, or add '--root [path]' to the invocation."
                         }.JoinNewLine());
        }

        [CanBeNull]
        private static AbsolutePath GetBuildAssemblyFile()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null || entryAssembly.GetTypes().All(x => !x.IsSubclassOf(typeof(NukeBuild))))
            {
                var assemblyName = entryAssembly?.GetName().Name;
                Assert.True(assemblyName is "ReSharperTestRunner" or "testhost", $"Assembly name was {assemblyName.SingleQuote()}");
                return null;
            }

            var assemblyLocation = entryAssembly.Location;
            var invokedLocation = Environment.GetCommandLineArgs().First();
            Assert.True(assemblyLocation == string.Empty || assemblyLocation == invokedLocation);

            return (AbsolutePath) (assemblyLocation != string.Empty ? assemblyLocation : invokedLocation);
        }

        [CanBeNull]
        private static AbsolutePath GetBuildProjectFile([CanBeNull] AbsolutePath buildAssemblyDirectory)
        {
            if (buildAssemblyDirectory == null)
                return null;

            return (AbsolutePath) new DirectoryInfo(buildAssemblyDirectory)
                .DescendantsAndSelf(x => x.Parent)
                .Select(x => x.GetFiles("*.csproj", SearchOption.TopDirectoryOnly)
                    .SingleOrDefaultOrError($"Found multiple project files in '{x}'."))
                .FirstOrDefault(x => x != null)
                ?.FullName;
        }
    }
}
