namespace DocNest.Embeddings;

/// <summary>
/// Locates (and optionally downloads) the cross-encoder/ms-marco-MiniLM-L-6-v2 ONNX model + vocab for the
/// reranker (ADR-0013). The ~91 MB model is not shipped; <see cref="EnsureDownloadedAsync"/> fetches it
/// from Hugging Face on first use into a local cache. Opt-in (network), mirroring <see cref="MiniLmModel"/>
/// so offline/CI runs that haven't provisioned it skip cleanly.
/// </summary>
public static class CrossEncoderModel
{
    private const string ModelFileName = "ms-marco-MiniLM-L-6-v2.onnx";
    private const string VocabFileName = "ms-marco-MiniLM-L-6-v2.vocab.txt";
    private const string ModelUrl = "https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2/resolve/main/vocab.txt";

    /// <summary>The expected model/vocab paths inside <paramref name="cacheDir"/> (not guaranteed to exist).</summary>
    public static (string ModelPath, string VocabPath) Paths(string cacheDir)
        => (Path.Combine(cacheDir, ModelFileName), Path.Combine(cacheDir, VocabFileName));

    /// <summary>True if both the model and vocab files are present in <paramref name="cacheDir"/>.</summary>
    public static bool IsPresent(string cacheDir)
    {
        var (modelPath, vocabPath) = Paths(cacheDir);
        return File.Exists(modelPath) && File.Exists(vocabPath);
    }

    /// <summary>Download the model + vocab into <paramref name="cacheDir"/> if missing, and return their paths.</summary>
    public static async Task<(string ModelPath, string VocabPath)> EnsureDownloadedAsync(
        string cacheDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDir);
        var (modelPath, vocabPath) = Paths(cacheDir);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        if (!File.Exists(modelPath))
        {
            await DownloadAsync(http, ModelUrl, modelPath, cancellationToken).ConfigureAwait(false);
        }
        if (!File.Exists(vocabPath))
        {
            await DownloadAsync(http, VocabUrl, vocabPath, cancellationToken).ConfigureAwait(false);
        }
        return (modelPath, vocabPath);
    }

    private static async Task DownloadAsync(HttpClient http, string url, string destination, CancellationToken cancellationToken)
    {
        var temp = destination + ".tmp";
        await using (var source = await http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false))
        await using (var file = File.Create(temp))
        {
            await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temp, destination, overwrite: true);
    }
}
