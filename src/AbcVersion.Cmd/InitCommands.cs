using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;

namespace Deneblab.AbcVersionCmd;

internal class InitCommands
{
    private readonly ILogger<InitCommands> _log;

    public InitCommands(ILoggerFactory factory)
    {
        _log = factory.CreateLogger<InitCommands>();
    }

    /// <summary>Initialize .abcversion.json in current git repository</summary>
    /// <param name="cancellationToken"></param>
    /// <param name="force">-f, Force overwrite of existing .abcversion.json</param>
    [Command("")]
    public async Task Root(
        CancellationToken cancellationToken,
        bool force = false
    )
    {
        var cd = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(cd, ".abcversion.json");
        var gitPath = Path.Combine(cd, ".git");

        if (!Directory.Exists(gitPath))
        {
            _log.LogError("Git repository not found in current directory. Please run this command in a Git repository root.");
            return;
        }

        if (File.Exists(configPath) && !force)
        {
            _log.LogInformation("AbcVersion already initialized in this directory. Use --force to override.");
            return;
        }

        var json = """
                   {
                     // Base version for the entire repository. 
                     // All branches and projects will use this as a starting point unless overridden.
                     // It used in version repository without projects and branches
                     "BaseVersion": "0.3.0",
                     // With projects, you can define multiple projects in the same repository, 
                     // each with its own versioning scheme.
                     "Projects": {
                       // Project name. This is an arbitrary identifier for the project.  
                       "server": {
                         // Path to the project relative to the repository root. 
                         // This is used to determine which commits belong to this project.
                         "Path": "src/StashLock.Server",
                         // Base version for this project. 
                         // This overrides the global BaseVersion for this project and its branches.
                         "BaseVersion": "2.0.0",
                         "Branches": {
                           "production": {
                             "StartPoint": {
                               "Version": "1.5.0",
                               "ParentSha": "9a254805956b694926ddf341f6a1e60ca57d27cb"
                             }
                           }
                         }
                       }
                     },
                     "Snapshots" : {
                       // Snapshots are used to define specific commits in the repository that should be treated as version milestones. 
                       // They can be used to mark important commits that don't necessarily correspond to branch tips.
                      "9a254805956b694926ddf341f6a1e60ca57d27cb": "1.2.3"
                     }
                   }
                   """;


        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        _log.LogInformation("AbcVersion initialized successfully. Created .abcversion.json with default settings.");
    }
}