namespace DocNest.Parsers;

/// <summary>
/// Selects the correct <see cref="IParser"/> for a file. Ordered registry, first match wins;
/// new formats register at runtime without source edits (Factory Method + Registry). Ports the
/// Python <c>ParserFactory</c>; the docx/xlsx/pdf parsers register here in later slices.
/// </summary>
public sealed class ParserFactory
{
    private readonly List<IParser> _registry;

    /// <summary>Create a factory with the built-in text parsers (markdown, csv, html).</summary>
    public ParserFactory()
        => _registry = new List<IParser> { new MarkdownParser(), new CsvParser(), new HtmlParser() };

    /// <summary>Return the first registered parser that supports <paramref name="filePath"/>.</summary>
    /// <exception cref="UnsupportedFormatException">If no registered parser handles the format.</exception>
    public IParser Get(string filePath)
    {
        foreach (var parser in _registry)
        {
            if (parser.Supports(filePath))
            {
                return parser;
            }
        }

        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
        {
            ext = "(no extension)";
        }
        throw new UnsupportedFormatException(
            $"No parser registered for '{ext}'. Supported formats: {SupportedFormats()}. " +
            "Call Register(...) to add a new format.");
    }

    /// <summary>Returns <see langword="true"/> if any registered parser supports the file.</summary>
    public bool Supports(string filePath) => _registry.Any(p => p.Supports(filePath));

    /// <summary>Register a parser (default position 0 = highest priority).</summary>
    public void Register(IParser parser, int position = 0)
    {
        ArgumentNullException.ThrowIfNull(parser);
        _registry.Insert(Math.Clamp(position, 0, _registry.Count), parser);
    }

    /// <summary>Remove all registered parsers of type <typeparamref name="T"/>.</summary>
    public void Unregister<T>() where T : IParser => _registry.RemoveAll(p => p is T);

    private string SupportedFormats()
        => string.Join(", ", _registry.Select(p => p.GetType().Name.Replace("Parser", "", StringComparison.Ordinal).ToLowerInvariant()));
}
