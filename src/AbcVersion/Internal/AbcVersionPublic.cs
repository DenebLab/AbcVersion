using Deneblab.StashLock.Cli.Common.Simple;

namespace Deneblab.AbcVersion.Internal;

public class AbcVersionRegistry
{
    public SimpleEnvResult Env { get; }
    public SimpleVersionInfo AppVersion { get; }

    public AbcVersionRegistry(SimpleEnvResult env, SimpleVersionInfo appVersion)
    {
        Env = env;
        AppVersion = appVersion;
    }
}