using OpenTK.Mathematics;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Text;

/// <summary>
/// <see cref="IChunkDb"/> implementation backed by a SQLite database.
/// Supports an optional read-only mode where writes are buffered in memory
/// instead of being persisted to disk.
/// </summary>
public class ChunkDbSqlite : IChunkDb
{
    private SQLiteConnection _conn;
    private string _databaseFile;

    /// <summary>
    /// When <see langword="true"/>, writes are stored in <see cref="_temporaryChunks"/>
    /// and never flushed to disk. Reads check the temporary store first.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>In-memory chunk buffer used when <see cref="ReadOnly"/> is active.</summary>
    private readonly Dictionary<ulong, byte[]> _temporaryChunks = [];

    // ── Connection string ─────────────────────────────────────────────────────

    private static string BuildConnectionString(string filename)
    {
        StringBuilder b = new();
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Data Source", filename);
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Version", "3");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "New", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Compress", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Journal Mode", "Off");
        return b.ToString();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Opens or creates the SQLite database at <paramref name="filename"/>.</summary>
    public void Open(string filename)
    {
        _databaseFile = filename;
        bool isNew = !File.Exists(filename);
        _conn = new SQLiteConnection(BuildConnectionString(filename));
        _conn.Open();
        if (isNew) CreateTables(_conn);
        if (!IntegrityCheck(_conn))
            Console.WriteLine("Database is possibly corrupted.");
    }

    /// <summary>Closes and disposes the underlying SQLite connection.</summary>
    public void Close()
    {
        _conn.Close();
        _conn.Dispose();
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private static void CreateTables(SQLiteConnection conn)
    {
        using SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE chunks (position integer PRIMARY KEY, data BLOB);";
        cmd.ExecuteNonQuery();
    }

    // ── Integrity ─────────────────────────────────────────────────────────────

    private static bool IntegrityCheck(SQLiteConnection conn)
    {
        Console.WriteLine($"Database: {conn.DataSource}. Running SQLite integrity check:");
        using SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        using SQLiteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string result = reader[0].ToString();
            Console.WriteLine(result);
            if (result == "ok") return true;
        }
        return false;
    }

    // ── Backup ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Backup(string backupFilename)
    {
        if (_databaseFile == backupFilename)
        {
            Console.WriteLine("Cannot overwrite current running database. Choose another destination.");
            return;
        }
        if (File.Exists(backupFilename))
            Console.WriteLine($"File {backupFilename} exists. Overwriting.");

        using SQLiteConnection backupConn = new(BuildConnectionString(backupFilename));
        backupConn.Open();
        _conn.BackupDatabase(backupConn, backupConn.Database, _conn.Database, -1, null, 10);
        Vacuum(backupConn);
    }

    private static void Vacuum(SQLiteConnection conn)
    {
        using SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "VACUUM;";
        cmd.ExecuteNonQuery();
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IEnumerable<byte[]> GetChunks(IEnumerable<Vector3i> chunkpositions)
    {
        using SQLiteTransaction tx = _conn.BeginTransaction();
        foreach (Vector3i xyz in chunkpositions)
            yield return ReadChunk(ToMapPos(xyz.X, xyz.Y, xyz.Z), _conn);
        tx.Commit();
    }

    /// <inheritdoc/>
    public byte[] GetGlobalData() => ReadChunk(ulong.MaxValue / 2, _conn);

    private byte[] ReadChunk(ulong position, SQLiteConnection conn)
    {
        if (ReadOnly && _temporaryChunks.TryGetValue(position, out byte[] cached))
            return cached;

        using SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data FROM chunks WHERE position=?";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        using SQLiteDataReader reader = cmd.ExecuteReader();
        return reader.Read() ? reader["data"] as byte[] : null;
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetChunks(IEnumerable<DbChunk> chunks)
    {
        if (ReadOnly)
        {
            foreach (DbChunk c in chunks)
                _temporaryChunks[ToMapPos(c.Position.X, c.Position.Y, c.Position.Z)] = (byte[])c.Chunk.Clone();
            return;
        }
        using SQLiteTransaction tx = _conn.BeginTransaction();
        foreach (DbChunk c in chunks)
            WriteChunk(ToMapPos(c.Position.X, c.Position.Y, c.Position.Z), c.Chunk, _conn);
        tx.Commit();
    }

    /// <inheritdoc/>
    public void SetGlobalData(byte[] data) => WriteChunk(ulong.MaxValue / 2, data, _conn);

    private static void WriteChunk(ulong position, byte[] data, SQLiteConnection conn)
    {
        using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO chunks (position, data) VALUES (?,?)";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        cmd.Parameters.Add(CreateParameter("data", DbType.Object, data, cmd));
        cmd.ExecuteNonQuery();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void DeleteChunks(IEnumerable<Vector3i> chunkpositions)
    {
        if (ReadOnly)
        {
            foreach (Vector3i xyz in chunkpositions)
                _temporaryChunks.Remove(ToMapPos(xyz.X, xyz.Y, xyz.Z));
            return;
        }
        using SQLiteTransaction tx = _conn.BeginTransaction();
        foreach (Vector3i xyz in chunkpositions)
        {
            using DbCommand cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM chunks WHERE position=?";
            cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, ToMapPos(xyz.X, xyz.Y, xyz.Z), cmd));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>Clears all in-memory temporary chunks accumulated during read-only mode.</summary>
    public void ClearTemporaryChunks() => _temporaryChunks.Clear();

    // ── File operations ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Dictionary<Vector3i, byte[]> GetChunksFromFile(IEnumerable<Vector3i> chunkpositions, string filename)
    {
        if (!File.Exists(filename))
        {
            Console.WriteLine($"File {filename} does not exist.");
            return [];
        }
        using SQLiteConnection conn = new(BuildConnectionString(filename));
        conn.Open();
        Dictionary<Vector3i, byte[]> result = [];
        using SQLiteTransaction tx = conn.BeginTransaction();
        foreach (Vector3i xyz in chunkpositions)
            result.Add(xyz, ReadChunk(ToMapPos(xyz.X, xyz.Y, xyz.Z), conn));
        tx.Commit();
        return result;
    }

    /// <inheritdoc/>
    public void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename)
    {
        if (_databaseFile == filename)
        {
            Console.WriteLine("Cannot overwrite current running database. Choose another destination.");
            return;
        }
        if (File.Exists(filename))
            Console.WriteLine($"File {filename} exists. Overwriting.");

        bool isNew = !File.Exists(filename);
        using SQLiteConnection conn = new(BuildConnectionString(filename));
        conn.Open();
        if (isNew) CreateTables(conn);

        using SQLiteTransaction tx = conn.BeginTransaction();
        foreach (DbChunk c in chunks)
            WriteChunk(ToMapPos(c.Position.X, c.Position.Y, c.Position.Z), c.Chunk, conn);
        tx.Commit();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Packs chunk-space coordinates into a single <see langword="ulong"/> key.
    /// Each axis occupies 20 bits, supporting coordinates up to 2²⁰ = 1,048,576.
    /// </summary>
    public static ulong ToMapPos(int x, int y, int z)
        => (ulong)x << 40 | (ulong)y << 20 | (ulong)z;

    private static DbParameter CreateParameter(string name, DbType type, object value, DbCommand cmd)
    {
        DbParameter p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        p.Value = value;
        return p;
    }

    public bool GetReadOnly() => ReadOnly;
    public void SetReadOnly(bool value) => ReadOnly = value;
}