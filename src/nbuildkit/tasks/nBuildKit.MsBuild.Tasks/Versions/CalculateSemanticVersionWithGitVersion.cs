﻿//-----------------------------------------------------------------------
// <copyright company="nBuildKit">
// Copyright (c) nBuildKit. All rights reserved.
// Licensed under the Apache License, Version 2.0 license. See LICENCE.md file in the project root for full license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using NBuildKit.MsBuild.Tasks.Core;

namespace NBuildKit.MsBuild.Tasks.Versions
{
    /// <summary>
    /// Defines a <see cref="ITask"/> that gets the semantic version information for the workspace via the
    /// GitVersion command line tool.
    /// </summary>
    public sealed class CalculateSemanticVersionWithGitVersion : CommandLineToolTask
    {
        private const string ErrorIdInvalidOutput = "NBuildKit.GetVersion.GitVersion.InvalidOutput";

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculateSemanticVersionWithGitVersion"/> class.
        /// </summary>
        public CalculateSemanticVersionWithGitVersion()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculateSemanticVersionWithGitVersion"/> class.
        /// </summary>
        /// <param name="invoker">The object which handles the invocation of the command line applications.</param>
        public CalculateSemanticVersionWithGitVersion(IApplicationInvoker invoker)
            : base(invoker)
        {
        }

        /// <inheritdoc/>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Catching them all because we don't know what could be thrown.")]
        public override bool Execute()
        {
            var arguments = new List<string>();
            {
                arguments.Add("/nofetch ");

                if (!string.IsNullOrEmpty(UserName))
                {
                    arguments.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "/u \"{0}\" ",
                            UserName));
                    arguments.Add("/p \"%GitPassWord%\" ");
                }

                if (!string.IsNullOrEmpty(RemoteRepositoryUri) && !string.IsNullOrEmpty(UserName))
                {
                    arguments.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "/url \"{0}\" ",
                            RemoteRepositoryUri));
                }

                arguments.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "/l \"{0}\" ",
                        LogPath));
            }

            var text = new StringBuilder();
            DataReceivedEventHandler standardOutputHandler = (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        text.Append(e.Data);
                    }
                };

            var exitCode = InvokeCommandLineTool(
                ExePath,
                arguments,
                WorkingDirectory,
                standardOutputHandler: standardOutputHandler);
            if (exitCode != 0)
            {
                Log.LogError(
                    string.Empty,
                    ErrorCodeById(Core.ErrorInformation.ErrorIdApplicationNonzeroExitCode),
                    Core.ErrorInformation.ErrorIdApplicationNonzeroExitCode,
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    "{0} exited with a non-zero exit code. Exit code was: {1}",
                    System.IO.Path.GetFileName(ExePath.ItemSpec),
                    exitCode);
                Log.LogWarning(
                    "Output was: {0}",
                    text);

                return false;
            }

            try
            {
                string versionText = text.ToString();
                const string fullSemVersionStart = "\"FullSemVer\":";
                var index = versionText.IndexOf(fullSemVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionSemanticFull = versionText.Substring(
                        index + fullSemVersionStart.Length,
                        versionText.IndexOf(",", index + fullSemVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + fullSemVersionStart.Length))
                    .Trim('"');

                const string nugetSemVersionStart = "\"NuGetVersionV2\":";
                index = versionText.IndexOf(nugetSemVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionSemanticNuGet = versionText.Substring(
                        index + nugetSemVersionStart.Length,
                        versionText.IndexOf(",", index + nugetSemVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + nugetSemVersionStart.Length))
                    .Trim('"');

                const string semVersionStart = "\"MajorMinorPatch\":";
                index = versionText.IndexOf(semVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionSemantic = versionText.Substring(
                        index + semVersionStart.Length,
                        versionText.IndexOf(",", index + semVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + semVersionStart.Length))
                    .Trim('"');

                const string majorVersionStart = "\"Major\":";
                index = versionText.IndexOf(majorVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionMajor = versionText.Substring(
                        index + majorVersionStart.Length,
                        versionText.IndexOf(",", index + majorVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + majorVersionStart.Length))
                    .Trim('"');

                const string minorVersionStart = "\"Minor\":";
                index = versionText.IndexOf(minorVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionMinor = versionText.Substring(
                        index + minorVersionStart.Length,
                        versionText.IndexOf(",", index + minorVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + minorVersionStart.Length))
                    .Trim('"');

                const string patchVersionStart = "\"Patch\":";
                index = versionText.IndexOf(patchVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionPatch = versionText.Substring(
                        index + patchVersionStart.Length,
                        versionText.IndexOf(",", index + patchVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + patchVersionStart.Length))
                    .Trim('"');

                const string buildVersionStart = "\"BuildMetaData\":";
                index = versionText.IndexOf(buildVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionBuild = versionText.Substring(
                        index + buildVersionStart.Length,
                        versionText.IndexOf(",", index + buildVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + buildVersionStart.Length))
                    .Trim('"');

                const string tagVersionStart = "\"PreReleaseTag\":";
                index = versionText.IndexOf(tagVersionStart, StringComparison.OrdinalIgnoreCase);
                VersionPrerelease = versionText.Substring(
                        index + tagVersionStart.Length,
                        versionText.IndexOf(",", index + tagVersionStart.Length, StringComparison.OrdinalIgnoreCase) - (index + tagVersionStart.Length))
                    .Trim('"');
                if (VersionPrerelease.IndexOf(".", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    VersionPrerelease = VersionPrerelease.Substring(0, VersionPrerelease.IndexOf(".", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception e)
            {
                Log.LogError(
                    string.Empty,
                    ErrorCodeById(ErrorIdInvalidOutput),
                    ErrorIdInvalidOutput,
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    e.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Gets or sets the full path of the command line executable.
        /// </summary>
        [Required]
        public ITaskItem ExePath
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full path of the log file.
        /// </summary>
        [Required]
        public ITaskItem LogPath
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the URL of the remote repository.
        /// </summary>
        [Required]
        [SuppressMessage(
            "Microsoft.Design",
            "CA1056:UriPropertiesShouldNotBeStrings",
            Justification = "This can be an empty string in which case the default origin will be used.")]
        public string RemoteRepositoryUri
        {
            get;
            set;
        }

        /// <summary>
        /// Updates the environment variables for the application prior to execution.
        /// </summary>
        /// <param name="environmentVariables">
        ///     The environment variables for the application. The environment variables for the process can be
        ///     changed by altering the collection.
        /// </param>
        protected override void UpdateEnvironmentVariables(StringDictionary environmentVariables)
        {
            if (environmentVariables == null)
            {
                return;
            }

            // GitVersion does all kinds of magic when it detects that it is running on a build server.
            // That magic can stuff up any changes we make to the git workspace because if we change branches
            // (e.g. during a merge) then GitVersion may change back to the original branch. So we remove any
            // indication that GitVersion is running on a buildserver. Until GitVersion has a flag to do so
            // we do this the hard way by removing all the environment variables linked to build servers
            var knownBuildServerEnvironmentKeys = new List<string>
                            {
                                "APPVEYOR",
                                "BUILD",
                                "BuildRunner",
                                "CI",
                                "GIT",
                                "GITLAB",
                                "JENKINS",
                                "TEAMCITY",
                                "TF",
                                "TRAVIS",
                            };

            var variablesToRemove = new List<string>();
            foreach (DictionaryEntry pair in environmentVariables)
            {
                var key = pair.Key as string;
                if (knownBuildServerEnvironmentKeys.Any(s => key.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                {
                    variablesToRemove.Add(key);
                }
            }

            foreach (var keyToRemove in variablesToRemove)
            {
                environmentVariables.Remove(keyToRemove);
            }
        }

        /// <summary>
        /// Gets or sets the name of the user that should be used to connect to the remote repository. If a username
        /// is provied then the password should be made available via the 'GitPassWord' environment variable for the
        /// process.
        /// </summary>
        public string UserName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the build number of the version.
        /// </summary>
        [Output]
        public string VersionBuild
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the major number of the version.
        /// </summary>
        [Output]
        public string VersionMajor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the minor number of the version.
        /// </summary>
        [Output]
        public string VersionMinor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the patch number of the version.
        /// </summary>
        [Output]
        public string VersionPatch
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the prerelease information of the semantic version.
        /// </summary>
        [Output]
        public string VersionPrerelease
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the semantic version.
        /// </summary>
        [Output]
        public string VersionSemantic
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the complete semantic version, including the prerelease information.
        /// </summary>
        [Output]
        public string VersionSemanticFull
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the complete semantic version following the NuGet specification.
        /// </summary>
        [Output]
        public string VersionSemanticNuGet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full path to the working directory.
        /// </summary>
        [Required]
        public ITaskItem WorkingDirectory
        {
            get;
            set;
        }
    }
}
