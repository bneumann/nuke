﻿// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Serilog;

namespace Nuke.Common
{
    public static partial class EnvironmentInfo
    {
        public static string NewLine => Environment.NewLine;
        public static string MachineName => Environment.MachineName;

        public static AbsolutePath WorkingDirectory
        {
#if NETCORE
            get => (AbsolutePath) Directory.GetCurrentDirectory();
            set => Directory.SetCurrentDirectory(value);
#else
            get => (AbsolutePath) Environment.CurrentDirectory;
            set => Environment.CurrentDirectory = value;
#endif
        }

        public static IDisposable SwitchWorkingDirectory(string workingDirectory, bool allowCreate = true)
        {
            if (allowCreate)
                FileSystemTasks.EnsureExistingDirectory(workingDirectory);

            var previousWorkingDirectory = WorkingDirectory;
            return DelegateDisposable.CreateBracket(
                () => WorkingDirectory = (AbsolutePath) workingDirectory,
                () => WorkingDirectory = previousWorkingDirectory);
        }

        public static string[] CommandLineArguments { get; internal set; } = Environment.GetCommandLineArgs();

        internal static string[] ParseCommandLineArguments(string commandLine)
        {
            var inSingleQuotes = false;
            var inDoubleQuotes = false;
            var escaped = false;
            return commandLine.Split((c, _) =>
                    {
                        if (c == '\"' && !inSingleQuotes && !escaped)
                            inDoubleQuotes = !inDoubleQuotes;

                        if (c == '\'' && !inDoubleQuotes && !escaped)
                            inSingleQuotes = !inSingleQuotes;

                        escaped = c == '\\' && !escaped;

                        return c == ' ' && !(inDoubleQuotes || inSingleQuotes);
                    },
                    includeSplitCharacter: true)
                .Select(x => x.Trim().TrimMatchingDoubleQuotes().TrimMatchingQuotes().Replace("\\\"", "\"").Replace("\\\'", "'"))
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }
    }
}
