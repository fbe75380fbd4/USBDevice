using System;
using System.Management.Automation;

namespace USBDevice.PowerShell.Helpers
{
    internal static class WildcardMatcher
    {
        public static WildcardPattern[] CreatePatterns(string[] patterns)
        {
            return Array.ConvertAll(patterns, pattern =>
                new WildcardPattern(pattern, WildcardOptions.IgnoreCase | WildcardOptions.Compiled)
            );
        }

        public static bool MatchesAnyWildcard(string targetValue, WildcardPattern[] patterns)
        {
            if (string.IsNullOrEmpty(targetValue)) return false;

            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(targetValue))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
