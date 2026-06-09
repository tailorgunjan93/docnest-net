using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace DocNest.Retrieval;

/// <summary>
/// SQLite-backed persistent store for the retrieval index (FTS5 BM25 + embedding blobs + graph edges
/// + a doc-hash registry). Wraps <see cref="Microsoft.Data.Sqlite"/> so no raw SQL leaks into the
/// retriever. FTS5 is provided by the bundled native SQLite.
/// </summary>
internal sealed partial class SqliteRetrievalStore : IDisposable
{
    private const string Schema = """
        PRAGMA journal_mode=WAL;
        PRAGMA synchronous=NORMAL;
        CREATE TABLE IF NOT EXISTS doc_hashes (
            doc_id TEXT PRIMARY KEY, hash TEXT NOT NULL, n_secs INTEGER NOT NULL, built_at INTEGER NOT NULL);
        CREATE VIRTUAL TABLE IF NOT EXISTS fts_sections USING fts5(
            doc_id UNINDEXED, sec_idx UNINDEXED, sec_id UNINDEXED, title, text, tokenize='porter ascii');
        CREATE TABLE IF NOT EXISTS embeddings (
            doc_id TEXT NOT NULL, sec_idx INTEGER NOT NULL, vec BLOB NOT NULL, PRIMARY KEY(doc_id, sec_idx));
        CREATE TABLE IF NOT EXISTS graph_edges (
            doc_id TEXT NOT NULL, from_idx INTEGER NOT NULL, to_idx INTEGER NOT NULL, edge_type TEXT NOT NULL, weight REAL NOT NULL);
        CREATE INDEX IF NOT EXISTS idx_graph_from ON graph_edges(doc_id, from_idx);
        """;

    private readonly SqliteConnection _connection;

    public SqliteRetrievalStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        Execute(Schema);
    }

    public string? GetHash(string docId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT hash FROM doc_hashes WHERE doc_id=$d";
        cmd.Parameters.AddWithValue("$d", docId);
        return cmd.ExecuteScalar() as string;
    }

    public void SetHash(string docId, string hash, int sectionCount)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO doc_hashes(doc_id, hash, n_secs, built_at) VALUES($d,$h,$n,$t)";
        cmd.Parameters.AddWithValue("$d", docId);
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$n", sectionCount);
        cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.ExecuteNonQuery();
    }

    public void DeleteDoc(string docId)
    {
        foreach (var table in new[] { "doc_hashes", "fts_sections", "embeddings", "graph_edges" })
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {table} WHERE doc_id=$d";
            cmd.Parameters.AddWithValue("$d", docId);
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertFtsRows(string docId, IReadOnlyList<Section> sections)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO fts_sections(doc_id, sec_idx, sec_id, title, text) VALUES($d,$i,$id,$t,$x)";
        var pd = cmd.Parameters.Add("$d", SqliteType.Text);
        var pi = cmd.Parameters.Add("$i", SqliteType.Integer);
        var pid = cmd.Parameters.Add("$id", SqliteType.Text);
        var pt = cmd.Parameters.Add("$t", SqliteType.Text);
        var px = cmd.Parameters.Add("$x", SqliteType.Text);
        for (var i = 0; i < sections.Count; i++)
        {
            pd.Value = docId;
            pi.Value = i;
            pid.Value = sections[i].Id;
            pt.Value = sections[i].Title;
            px.Value = sections[i].Text;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<int> Bm25Rank(string docId, string ftsQuery, int pool)
    {
        try
        {
            return RunMatch(docId, ftsQuery, pool);
        }
        catch (SqliteException)
        {
            var safe = NonWordRe().Replace(ftsQuery, " ").Trim();
            if (safe.Length == 0)
            {
                return new List<int>();
            }
            try
            {
                return RunMatch(docId, safe, pool);
            }
            catch (SqliteException)
            {
                return new List<int>();
            }
        }
    }

    private List<int> RunMatch(string docId, string match, int pool)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT sec_idx FROM fts_sections WHERE doc_id=$d AND fts_sections MATCH $q ORDER BY rank LIMIT $n";
        cmd.Parameters.AddWithValue("$d", docId);
        cmd.Parameters.AddWithValue("$q", match);
        cmd.Parameters.AddWithValue("$n", pool);
        using var reader = cmd.ExecuteReader();
        var result = new List<int>();
        while (reader.Read())
        {
            result.Add(reader.GetInt32(0));
        }
        return result;
    }

    public void UpsertEmbedding(string docId, int sectionIndex, byte[] vector)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO embeddings(doc_id, sec_idx, vec) VALUES($d,$i,$v)";
        cmd.Parameters.AddWithValue("$d", docId);
        cmd.Parameters.AddWithValue("$i", sectionIndex);
        cmd.Parameters.AddWithValue("$v", vector);
        cmd.ExecuteNonQuery();
    }

    public List<(int Index, float[] Vector)> GetEmbeddings(string docId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT sec_idx, vec FROM embeddings WHERE doc_id=$d ORDER BY sec_idx";
        cmd.Parameters.AddWithValue("$d", docId);
        using var reader = cmd.ExecuteReader();
        var result = new List<(int, float[])>();
        while (reader.Read())
        {
            var blob = (byte[])reader["vec"];
            var floats = new float[blob.Length / 4];
            Buffer.BlockCopy(blob, 0, floats, 0, floats.Length * 4);
            result.Add((reader.GetInt32(0), floats));
        }
        return result;
    }

    public void InsertEdges(IReadOnlyList<(string DocId, int From, int To, string Type, double Weight)> edges)
    {
        if (edges.Count == 0)
        {
            return;
        }
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO graph_edges(doc_id, from_idx, to_idx, edge_type, weight) VALUES($d,$f,$t,$e,$w)";
        var pd = cmd.Parameters.Add("$d", SqliteType.Text);
        var pf = cmd.Parameters.Add("$f", SqliteType.Integer);
        var pt = cmd.Parameters.Add("$t", SqliteType.Integer);
        var pe = cmd.Parameters.Add("$e", SqliteType.Text);
        var pw = cmd.Parameters.Add("$w", SqliteType.Real);
        foreach (var (docId, from, to, type, weight) in edges)
        {
            pd.Value = docId;
            pf.Value = from;
            pt.Value = to;
            pe.Value = type;
            pw.Value = weight;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<GraphEdge> GetEdgesFrom(string docId, IReadOnlyCollection<int> seeds)
    {
        var result = new List<GraphEdge>();
        if (seeds.Count == 0)
        {
            return result;
        }
        var seedList = seeds.ToList();
        var placeholders = string.Join(",", seedList.Select((_, i) => $"$s{i}"));
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"SELECT from_idx, to_idx, edge_type, weight FROM graph_edges " +
            $"WHERE doc_id=$d AND from_idx IN ({placeholders}) AND edge_type IN ('child','sibling','semantic')";
        cmd.Parameters.AddWithValue("$d", docId);
        for (var i = 0; i < seedList.Count; i++)
        {
            cmd.Parameters.AddWithValue($"$s{i}", seedList[i]);
        }
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new GraphEdge(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDouble(3)));
        }
        return result;
    }

    public (bool Indexed, int SectionCount, long BuiltAt, int EdgeCount) Stats(string docId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT n_secs, built_at FROM doc_hashes WHERE doc_id=$d";
        cmd.Parameters.AddWithValue("$d", docId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return (false, 0, 0, 0);
        }
        var nSecs = reader.GetInt32(0);
        var builtAt = reader.GetInt64(1);
        reader.Close();

        using var edgeCmd = _connection.CreateCommand();
        edgeCmd.CommandText = "SELECT COUNT(*) FROM graph_edges WHERE doc_id=$d";
        edgeCmd.Parameters.AddWithValue("$d", docId);
        var edgeCount = Convert.ToInt32(edgeCmd.ExecuteScalar());
        return (true, nSecs, builtAt, edgeCount);
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex NonWordRe();
}
