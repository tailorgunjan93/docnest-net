using System.Security.Cryptography;
using System.Text;

namespace DocNest.Retrieval;

/// <summary>
/// Persistent hybrid retrieval: SQLite FTS5 (BM25) + dense cosine (over an injected
/// <see cref="IEmbedder"/>) + RRF fusion + 1-hop section-graph expansion, cached to disk and
/// invalidated by a document fingerprint. Ports the Python <c>HybridRetriever</c>. With no embedder,
/// retrieval degrades to BM25 + structural graph. Operates on an in-memory <see cref="Document"/>.
/// </summary>
public sealed class HybridRetriever : IRetriever, IDisposable
{
    private const double SemanticEdgeThreshold = 0.68;
    private const int RerankPool = 12;     // top RRF candidates re-scored by the cross-encoder (ADR-0013)
    private readonly SqliteRetrievalStore _store;
    private readonly IEmbedder? _embedder;
    private readonly IReranker? _reranker;

    /// <summary>Create a retriever caching to <paramref name="cacheDir"/>; pass an
    /// <see cref="IEmbedder"/> to enable the dense + semantic-graph signals, and an optional
    /// <see cref="IReranker"/> to re-score the top candidates for precision (ADR-0013).</summary>
    public HybridRetriever(string cacheDir = ".docnest_cache", IEmbedder? embedder = null, IReranker? reranker = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheDir);
        Directory.CreateDirectory(cacheDir);
        _store = new SqliteRetrievalStore(Path.Combine(cacheDir, "retrieval.db"));
        _embedder = embedder;
        _reranker = reranker;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RetrievalHit>> RetrieveAsync(
        Document doc, string query, int k = 8, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.Sections.Count == 0)
        {
            return [];
        }

        await EnsureIndexedAsync(doc, cancellationToken).ConfigureAwait(false);

        var sectionCount = doc.Sections.Count;
        k = Math.Min(k, sectionCount);
        var pool = Math.Min(k * 3, sectionCount);

        var bm25 = _store.Bm25Rank(doc.DocId, BuildFtsQuery(query), pool);
        var dense = await DenseRankAsync(doc, query, pool, cancellationToken).ConfigureAwait(false);

        var scores = RrfFusion.Fuse(bm25, dense);
        var edges = _store.GetEdgesFrom(doc.DocId, scores.Keys.ToList());
        scores = RrfFusion.GraphExpand(scores, edges, sectionCount);

        var ordered = scores
            .Where(kv => kv.Key >= 0 && kv.Key < sectionCount)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        if (_reranker is not null && ordered.Count > 1)
        {
            return await RerankAsync(doc, query, ordered, k, cancellationToken).ConfigureAwait(false);
        }

        return ordered
            .Take(k)
            .Select(kv => new RetrievalHit(doc.Sections[kv.Key], kv.Value))
            .ToList();
    }

    /// <summary>
    /// Cross-encoder precision pass (ADR-0013): re-score the top <see cref="RerankPool"/> RRF candidates by
    /// true query↔section relevance and return the best <paramref name="k"/>. Recall still comes from
    /// BM25 + dense + graph; the reranker only reorders their pool. The returned <c>Score</c> is the CE score.
    /// </summary>
    private async Task<IReadOnlyList<RetrievalHit>> RerankAsync(
        Document doc, string query, List<KeyValuePair<int, double>> ordered, int k, CancellationToken cancellationToken)
    {
        var pool = ordered.Take(Math.Min(RerankPool, ordered.Count)).ToList();
        var passages = pool.Select(kv => $"{doc.Sections[kv.Key].Title} {doc.Sections[kv.Key].Text}").ToList();
        var ceScores = await _reranker!.ScoreAsync(query, passages, cancellationToken).ConfigureAwait(false);
        return pool
            .Select((kv, i) => (Index: kv.Key, Ce: i < ceScores.Count ? ceScores[i] : double.MinValue))
            .OrderByDescending(x => x.Ce)
            .Take(k)
            .Select(x => new RetrievalHit(doc.Sections[x.Index], x.Ce))
            .ToList();
    }

    /// <summary>Build (or rebuild) the index for <paramref name="doc"/>.</summary>
    public async Task BuildIndexAsync(Document doc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var docId = doc.DocId;
        _store.DeleteDoc(docId);
        _store.InsertFtsRows(docId, doc.Sections);

        float[][]? embeddings = null;
        if (_embedder is not null)
        {
            var texts = doc.Sections.Select(s => $"{s.Title} {s.Text}").ToList();
            var raw = await _embedder.EmbedAsync(texts, cancellationToken).ConfigureAwait(false);
            embeddings = raw.Select(Normalize).ToArray();
            for (var i = 0; i < embeddings.Length; i++)
            {
                _store.UpsertEmbedding(docId, i, ToBytes(embeddings[i]));
            }
        }

        BuildGraph(doc, embeddings);
        _store.SetHash(docId, ComputeKey(doc), doc.Sections.Count);
    }

    /// <summary>True if a valid (non-stale) index exists for <paramref name="doc"/>.</summary>
    public bool IsCached(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return _store.GetHash(doc.DocId) == ComputeKey(doc);
    }

    /// <summary>Delete all cached data for a document (forces rebuild on next query).</summary>
    public void Invalidate(string docId) => _store.DeleteDoc(docId);

    /// <summary>Index statistics for a document.</summary>
    public (bool Indexed, int SectionCount, long BuiltAt, int EdgeCount) Stats(string docId)
        => _store.Stats(docId);

    private async Task EnsureIndexedAsync(Document doc, CancellationToken cancellationToken)
    {
        if (!IsCached(doc))
        {
            await BuildIndexAsync(doc, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<int>> DenseRankAsync(Document doc, string query, int pool, CancellationToken cancellationToken)
    {
        if (_embedder is null)
        {
            return new List<int>();
        }
        var stored = _store.GetEmbeddings(doc.DocId);
        if (stored.Count == 0)
        {
            return new List<int>();
        }
        var rawQuery = await _embedder.EmbedAsync(new[] { query }, cancellationToken).ConfigureAwait(false);
        if (rawQuery.Count == 0)
        {
            return new List<int>();
        }
        var q = Normalize(rawQuery[0]);
        return stored
            .Select(e => (e.Index, Sim: Dot(q, e.Vector)))
            .OrderByDescending(x => x.Sim)
            .Take(pool)
            .Select(x => x.Index)
            .ToList();
    }

    private void BuildGraph(Document doc, float[][]? embeddings)
    {
        var docId = doc.DocId;
        var sections = doc.Sections;
        var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sections.Count; i++)
        {
            idToIndex[sections[i].Id] = i;
        }

        var edges = new List<(string, int, int, string, double)>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (!string.IsNullOrEmpty(section.ParentId) && idToIndex.TryGetValue(section.ParentId, out var parentIndex))
            {
                edges.Add((docId, i, parentIndex, "parent", 1.0));
                edges.Add((docId, parentIndex, i, "child", 1.0));
            }
            if (!string.IsNullOrEmpty(section.ParentId))
            {
                for (var j = 0; j < sections.Count; j++)
                {
                    if (j != i && sections[j].ParentId == section.ParentId)
                    {
                        edges.Add((docId, i, j, "sibling", 0.8));
                    }
                }
            }
        }

        if (embeddings is not null && sections.Count <= 1000)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                for (var j = 0; j < sections.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    var sim = Dot(embeddings[i], embeddings[j]);
                    if (sim >= SemanticEdgeThreshold)
                    {
                        edges.Add((docId, i, j, "semantic", sim));
                    }
                }
            }
        }

        _store.InsertEdges(edges);
    }

    private static string BuildFtsQuery(string query)
    {
        var (full, keywords) = RetrievalTokenizer.QueryTokens(query);
        var tokens = keywords.Count > 0 ? keywords : full;
        if (tokens.Count == 0)
        {
            return "\"" + query.Replace("\"", " ", StringComparison.Ordinal) + "\"";
        }
        // Quote each token so FTS5 treats hyphens etc. literally (not as operators).
        return string.Join(" OR ", tokens.Select(t => $"\"{t}\""));
    }

    private static string ComputeKey(Document doc)
    {
        using var buffer = new MemoryStream();
        Write(buffer, Encoding.UTF8.GetBytes(doc.DocId));
        Write(buffer, BigEndian(doc.Sections.Count));
        foreach (var section in doc.Sections)
        {
            Write(buffer, Encoding.UTF8.GetBytes(section.Id));
            var text = section.Text;
            Write(buffer, BigEndian(text.Length));
            var prefix = text.Length > 200 ? text[..200] : text;
            Write(buffer, Encoding.UTF8.GetBytes(prefix));
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();

        static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] BigEndian(int value)
        => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    private static float[] Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => (double)x * x));
        if (norm <= 1e-12)
        {
            return vector;
        }
        var result = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            result[i] = (float)(vector[i] / norm);
        }
        return result;
    }

    private static double Dot(float[] a, float[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        var sum = 0.0;
        for (var i = 0; i < n; i++)
        {
            sum += (double)a[i] * b[i];
        }
        return sum;
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * 4];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Disposes the underlying SQLite connection.</summary>
    public void Dispose() => _store.Dispose();
}
