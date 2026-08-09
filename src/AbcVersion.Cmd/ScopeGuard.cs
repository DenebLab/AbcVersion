using System;

namespace Deneblab.AbcVersionCmd;

internal static class ScopeGuard
{
    /// <summary>
    ///     Refuses <c>--scope</c> and <c>--project</c> together. The library enforces this as well;
    ///     this exists so the command line reports the failure in terms of the flags the user
    ///     actually typed.
    /// </summary>
    /// <remarks>
    ///     Both options narrow the commit count. Letting one win silently would leave an invocation
    ///     whose meaning cannot be read at the call site, so the combination is rejected outright.
    /// </remarks>
    public static void RejectScopeWithProject(string scope, string project)
    {
        if (string.IsNullOrEmpty(scope)) return;
        if (string.IsNullOrEmpty(project) || project == ".") return;

        throw new ArgumentException(
            $"--scope and --project cannot be used together (--scope '{scope}', --project '{project}'). " +
            "Both narrow the commit count to part of the repository — pick one.");
    }
}
