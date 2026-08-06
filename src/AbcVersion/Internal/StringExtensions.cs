using System.Collections.Generic;

namespace Deneblab.AbcVersion.Internal;

internal static class StringExtensions
{
    public static string Join(this IEnumerable<string> enumerable, string separator)
    {
        return string.Join(separator, enumerable);
    }
}