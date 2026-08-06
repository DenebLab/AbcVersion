using System;
using System.IO;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Deneblab.AbcVersion.Internal;
using Deneblab.StashLock.Cli.Common.Simple;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

// ReSharper disable ConvertToUsingDeclaration

namespace Deneblab.AbcVersionCmd;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var env = SimpleEnv.Detect("Deneblab", "AbcVersion", SimpleAppMode.CurrentUserDir);
        var appVersion = SimpleVersionParser.FromCurrentApp();


        try
        {
            args = RewriteBareFlagAsSubcommand(args);
            var shouldContinue = StartupCheck(args, appVersion);
            if (!shouldContinue) return;
            var reg = new AbcVersionRegistry(env, appVersion);
            var nlogConfig = new LoggingConfiguration();
            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout =
                    "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}"
            };
            var fileTarget = new FileTarget("file")
            {
                FileName = Path.Combine(reg.Env.LogDir, "abcversion-cli.log"),
                Layout =
                    "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}",
                ArchiveAboveSize = 10_000_000,
                MaxArchiveFiles = 3
            };
            nlogConfig.AddTarget(consoleTarget);
            nlogConfig.AddTarget(fileTarget);
            nlogConfig.AddRule(LogLevel.Warn, LogLevel.Fatal, consoleTarget);
            nlogConfig.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
            LogManager.Configuration = nlogConfig;

            var services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                loggingBuilder.AddNLog();
            });

            await using var serviceProvider = services.BuildServiceProvider();
            {
                ConsoleApp.ServiceProvider = serviceProvider;
                var msLogger = serviceProvider.GetRequiredService<ILogger<Program>>();
                ConsoleApp.Log = msg => msLogger.LogInformation(msg);
                ConsoleApp.LogError = msg => msLogger.LogError(msg);

                var app = ConsoleApp.Create();
                app.Add<MainCommands>();
                app.Add<InitCommands>("init");
                app.Add<InfoCommands>("info");
                app.Add<ProjectsCommands>("projects");
                await app.RunAsync(args);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    private static bool StartupCheck(string[] args, SimpleVersionInfo appVersion)
    {
        var msg = $"DenebLab AbcVersion {appVersion.SemVer}";
        ConsoleApp.Version = msg;

        if (args.Length == 0) return true;

        if (args.Length == 1 && args[0].Equals("--version", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(msg);
            return false;
        }

        if (args.Length == 1 && (args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                                 args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHelp(msg);
            return false;
        }

        if (args.Length == 2 && (args[1].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                                 args[1].Equals("--help", StringComparison.OrdinalIgnoreCase)))
        {
            PrintSubcommandHelp(msg, args[0]);
            return false;
        }

        return true;
    }

    private static void PrintHelp(string version)
    {
        Console.WriteLine(version);
        Console.WriteLine();
        Console.WriteLine("Usage: abcversion [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  (default)    Get version information for the current repository");
        Console.WriteLine();
        Console.WriteLine("     Examples:");
        Console.WriteLine("       abcversion");
        Console.WriteLine("       abcversion --path ./my-repo");
        Console.WriteLine("       abcversion --project MyLib");
        Console.WriteLine("       abcversion -p semversion");
        Console.WriteLine("  init         Initialize .abcversion.json in current git repository");
        Console.WriteLine();
        Console.WriteLine("     Examples:");
        Console.WriteLine("       abcversion init");
        Console.WriteLine("       abcversion init --force");
        Console.WriteLine("  info         Show diagnostic information about version resolution");
        Console.WriteLine();
        Console.WriteLine("     Examples:");
        Console.WriteLine("       abcversion info");
        Console.WriteLine("       abcversion info --path ./my-repo");
        Console.WriteLine("       abcversion info --project MyLib");
        Console.WriteLine("  projects     List configured projects from .abcversion.json");
        Console.WriteLine();
        Console.WriteLine("     Examples:");
        Console.WriteLine("       abcversion projects");
        Console.WriteLine("       abcversion projects --path ./my-repo");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --path <path>        Path to git repository (default: current directory)");
        Console.WriteLine("  --project <name>     Project name from .abcversion.json (default: main)");
        Console.WriteLine("  -p, --property <name>  Get only one property (e.g. semversion, gitsha)");
        Console.WriteLine("  --version            Show version");
        Console.WriteLine("  -h, --help           Show this help");
        Console.WriteLine();
        Console.WriteLine("Example .abcversion.json:");
        Console.WriteLine();
        Console.WriteLine("  {");
        Console.WriteLine("    // Base version for the entire repository.");
        Console.WriteLine("    // All branches and projects will use this as a starting point unless overridden.");
        Console.WriteLine("    // It used in version repository without projects and branches");
        Console.WriteLine("    \"BaseVersion\": \"0.3.0\",");
        Console.WriteLine();
        Console.WriteLine("    // With projects, you can define multiple projects in the same repository,");
        Console.WriteLine("    // each with its own versioning scheme.");
        Console.WriteLine("    \"Projects\": {");
        Console.WriteLine("      // Project name. This is an arbitrary identifier for the project.");
        Console.WriteLine("      \"server\": {");
        Console.WriteLine("        // Path to the project relative to the repository root.");
        Console.WriteLine("        // This is used to determine which commits belong to this project.");
        Console.WriteLine("        \"Path\": \"src/StashLock.Server\",");
        Console.WriteLine("        // Base version for this project.");
        Console.WriteLine("        // This overrides the global BaseVersion for this project and its branches.");
        Console.WriteLine("        \"BaseVersion\": \"2.0.0\",");
        Console.WriteLine("        \"Branches\": {");
        Console.WriteLine("          \"production\": {");
        Console.WriteLine("            \"StartPoint\": {");
        Console.WriteLine("              \"Version\": \"1.5.0\",");
        Console.WriteLine("              \"ParentSha\": \"9a25480...\"");
        Console.WriteLine("            }");
        Console.WriteLine("          }");
        Console.WriteLine("        }");
        Console.WriteLine("      }");
        Console.WriteLine("    },");
        Console.WriteLine();
        Console.WriteLine("    // Snapshots are used to define specific commits that should be treated");
        Console.WriteLine("    // as version milestones. They can be used to mark important commits");
        Console.WriteLine("    // that don't necessarily correspond to branch tips.");
        Console.WriteLine("    \"Snapshots\": {");
        Console.WriteLine("      \"9a25480...\": \"1.2.3\"");
        Console.WriteLine("    }");
        Console.WriteLine("  }");
    }

    /// <summary>
    /// Rewrites bare flags (e.g. "--project" with no value) into subcommands.
    /// </summary>
    private static string[] RewriteBareFlagAsSubcommand(string[] args)
    {
        if (args.Length >= 1 && args[^1].Equals("--project", StringComparison.OrdinalIgnoreCase))
        {
            // Extract --path value if present
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length - 1)
                    return new[] { "projects", "--path", args[i + 1] };
            }

            return new[] { "projects" };
        }

        return args;
    }

    private static void PrintSubcommandHelp(string version, string subcommand)
    {
        Console.WriteLine(version);
        Console.WriteLine();

        switch (subcommand.ToLowerInvariant())
        {
            case "init":
                Console.WriteLine("Initialize .abcversion.json in current git repository");
                Console.WriteLine();
                Console.WriteLine("Usage: abcversion init [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  -f, --force    Overwrite existing .abcversion.json");
                Console.WriteLine("  -h, --help     Show this help");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  abcversion init");
                Console.WriteLine("  abcversion init --force");
                Console.WriteLine();
                Console.WriteLine("Generated .abcversion.json structure:");
                Console.WriteLine();
                Console.WriteLine("  {");
                Console.WriteLine("    // Base version for the entire repository.");
                Console.WriteLine("    // All branches and projects will use this as a starting point unless overridden.");
                Console.WriteLine("    // It used in version repository without projects and branches");
                Console.WriteLine("    \"BaseVersion\": \"0.3.0\",");
                Console.WriteLine();
                Console.WriteLine("    // With projects, you can define multiple projects in the same repository,");
                Console.WriteLine("    // each with its own versioning scheme.");
                Console.WriteLine("    \"Projects\": {");
                Console.WriteLine("      // Project name. This is an arbitrary identifier for the project.");
                Console.WriteLine("      \"server\": {");
                Console.WriteLine("        // Path to the project relative to the repository root.");
                Console.WriteLine("        // This is used to determine which commits belong to this project.");
                Console.WriteLine("        \"Path\": \"src/StashLock.Server\",");
                Console.WriteLine("        // Base version for this project.");
                Console.WriteLine("        // This overrides the global BaseVersion for this project and its branches.");
                Console.WriteLine("        \"BaseVersion\": \"2.0.0\",");
                Console.WriteLine("        \"Branches\": {");
                Console.WriteLine("          \"production\": {");
                Console.WriteLine("            \"StartPoint\": {");
                Console.WriteLine("              \"Version\": \"1.5.0\",");
                Console.WriteLine("              \"ParentSha\": \"9a25480...\"");
                Console.WriteLine("            }");
                Console.WriteLine("          }");
                Console.WriteLine("        }");
                Console.WriteLine("      }");
                Console.WriteLine("    },");
                Console.WriteLine();
                Console.WriteLine("    // Snapshots are used to define specific commits that should be treated");
                Console.WriteLine("    // as version milestones. They can be used to mark important commits");
                Console.WriteLine("    // that don't necessarily correspond to branch tips.");
                Console.WriteLine("    \"Snapshots\": {");
                Console.WriteLine("      \"9a25480...\": \"1.2.3\"");
                Console.WriteLine("    }");
                Console.WriteLine("  }");
                break;
            case "info":
                Console.WriteLine("Show diagnostic information about version resolution");
                Console.WriteLine();
                Console.WriteLine("Usage: abcversion info [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --path <path>      Path to git repository (default: current directory)");
                Console.WriteLine("  --project <name>   Project name from .abcversion.json (default: main)");
                Console.WriteLine("  -h, --help         Show this help");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  abcversion info");
                Console.WriteLine("  abcversion info --path ./my-repo");
                Console.WriteLine("  abcversion info --project MyLib");
                break;
            case "projects":
                Console.WriteLine("List configured projects from .abcversion.json");
                Console.WriteLine();
                Console.WriteLine("Usage: abcversion projects [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --path <path>      Path to git repository (default: current directory)");
                Console.WriteLine("  -h, --help         Show this help");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  abcversion projects");
                Console.WriteLine("  abcversion projects --path ./my-repo");
                break;
            default:
                Console.WriteLine($"Unknown command: {subcommand}");
                Console.WriteLine("Run 'abcversion --help' for available commands.");
                break;
        }
    }
}