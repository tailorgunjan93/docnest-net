using System;

namespace DocNest.Parsers.Tests;

internal static class ParserFixtures
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", name);
}
