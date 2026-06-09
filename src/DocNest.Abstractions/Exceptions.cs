namespace DocNest;

/// <summary>Base exception for all DocNest errors. Catch this to handle any DocNest failure.</summary>
public class DocNestException : Exception
{
    /// <summary>Initializes a new <see cref="DocNestException"/>.</summary>
    public DocNestException() { }

    /// <summary>Initializes a new <see cref="DocNestException"/> with a message.</summary>
    public DocNestException(string message) : base(message) { }

    /// <summary>Initializes a new <see cref="DocNestException"/> with a message and inner exception.</summary>
    public DocNestException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when a parser cannot extract content from a document.</summary>
public sealed class ParseException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public ParseException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public ParseException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public ParseException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when no parser supports the given file format.</summary>
public sealed class UnsupportedFormatException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public UnsupportedFormatException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public UnsupportedFormatException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public UnsupportedFormatException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when embedding generation fails.</summary>
public sealed class EmbedException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public EmbedException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public EmbedException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public EmbedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when LLM-powered enrichment fails.</summary>
public sealed class IntelligenceException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public IntelligenceException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public IntelligenceException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public IntelligenceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when writing a .udf file fails.</summary>
public sealed class UdfWriteException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public UdfWriteException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public UdfWriteException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public UdfWriteException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when reading or parsing a .udf file fails.</summary>
public sealed class UdfReadException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public UdfReadException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public UdfReadException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public UdfReadException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when the estimated .udf size exceeds the configured limit.</summary>
public sealed class SizeLimitException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public SizeLimitException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public SizeLimitException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public SizeLimitException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when a source connector fails to fetch documents.</summary>
public sealed class ConnectorException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public ConnectorException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public ConnectorException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public ConnectorException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when embedding quantization or dequantization fails.</summary>
public sealed class QuantizationException : DocNestException
{
    /// <inheritdoc cref="DocNestException()"/>
    public QuantizationException() { }
    /// <inheritdoc cref="DocNestException(string)"/>
    public QuantizationException(string message) : base(message) { }
    /// <inheritdoc cref="DocNestException(string, Exception)"/>
    public QuantizationException(string message, Exception innerException) : base(message, innerException) { }
}
