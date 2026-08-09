using System;
using System.Collections.Generic;
using Deneblab.AbcVersion;
using Helpers;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Utilities.Collections;
using Serilog;
using static System.Environment;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

class Build : FalloutBuild
{
    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    readonly DateTime BuildDate = DateTime.UtcNow;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

  
    [Solution] readonly Solution Solution;

    AbcVersion AbcVersion => AbcVersionFactory
        .CreateOneBuilder()
        .SetRepositoryRoot(RootDirectory)
        .SetDateTime(BuildDate)
        .UseLogger(new LogImplementation(Log.Logger))
        .Build();

    AbsolutePath TmpBuild => TemporaryDirectory / "w";
    AbsolutePath SourceDirectory => RootDirectory / "src";
    Project AbcVersionClientProject => Solution.GetProject("AbcVersion").NotNull();

    Project AbcVersionCmdProject => Solution.GetProject("AbcVersion.Cmd").NotNull();

    Target Information => _ => _
        .Executes(() =>
        {
            var b = AbcVersion;
            Log.Information($"Host: '{Host}'");
            Log.Information($"Version: '{b.SemVersion}'");
            Log.Information($"Date: '{b.DateTime:s}Z'");
            Log.Information($"Build target: {Configuration}");
            Log.Information($"FullVersion: '{b.InformationalVersion}'");

        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            TmpBuild.CreateOrCleanDirectory();
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x =>
            {
                if (x.Parent?.Name == "build") return;
                if (x.Parent?.Name == "infra") return;
                x.DeleteDirectory();
            });
        });



    Target Restore => _ => _
        .DependsOn()
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(AbcVersionClientProject));
            DotNetRestore(s => s.SetProjectFile(AbcVersionCmdProject));
        });

    Target PublishAll => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
        });


    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(AbcVersionCmdProject)
                .SetConfiguration(Configuration.Release)
                .SetVersion(AbcVersion.SemVersion)
                .SetFileVersion(AbcVersion.SemVersion)
                .SetAssemblyVersion(AbcVersion.SemVersion)
                .SetInformationalVersion(AbcVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target InstallLocal => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var packDir = TmpBuild / "local-tool";
            packDir.CreateOrCleanDirectory();

            DotNetPack(s => s
                .SetProject(AbcVersionCmdProject)
                .SetConfiguration(Configuration.Release)
                .SetOutputDirectory(packDir)
                .SetVersion(AbcVersion.SemVersion)
                .SetFileVersion(AbcVersion.SemVersion)
                .SetAssemblyVersion(AbcVersion.SemVersion)
                .SetInformationalVersion(AbcVersion.InformationalVersion));

            DotNet("tool uninstall Deneblab.AbcVersionCmd --global", exitHandler: p => p);

            DotNetToolInstall(s => s
                .SetPackageName("Deneblab.AbcVersionCmd")
                .EnableGlobal()
                .AddSources(packDir));
        });

    [Parameter("Runtime identifier for a single-RID native AOT publish (e.g. win-x64, linux-x64). If unset, builds all of NativeRuntimeIdentifiers.")]
    readonly string Rid;

    IReadOnlyCollection<string> NativeRuntimeIdentifiers => ["win-x64", "linux-x64"];

    IReadOnlyCollection<string> RidsToPublish => string.IsNullOrWhiteSpace(Rid)
        ? NativeRuntimeIdentifiers
        : [Rid];

    Target GithubRelease => _ => _
        .DependsOn(Information, Clean)
        .Executes(() =>
        {
            var p = AbcVersionCmdProject;
            if (p == null) return;

            foreach (var rid in RidsToPublish)
            {
                Log.Information($"Build Native AOT; Project file: {p.Name}; RID: {rid}; Version: {AbcVersion.SemVersion}");
                var outDir = TmpBuild / p.Name / "github-release" / rid;
                outDir.CreateOrCleanDirectory();

                DotNetRestore(s => s
                    .SetProjectFile(p.Path)
                    .SetRuntime(rid)
                );

                DotNetPublish(o => o
                    .SetProject(p.Path)
                    .EnableNoRestore()
                    .SetConfiguration(Configuration)
                    .SetOutput(outDir)
                    .SetSelfContained(true)
                    .SetRuntime(rid)
                    .SetProperty("PublishAot", true)
                    .SetVersion(AbcVersion.SemVersion)
                    .SetFileVersion(AbcVersion.SemVersion)
                    .SetAssemblyVersion(AbcVersion.SemVersion)
                    .SetInformationalVersion(AbcVersion.InformationalVersion)
                );

                var executableName = rid.StartsWith("win-") ? "Deneblab.AbcVersionCmd.exe" : "Deneblab.AbcVersionCmd";
                Log.Information($"Native executable created: {outDir / executableName}");
            }
        });

    public static int Main() => Execute<Build>(x => x.PublishAll);


}