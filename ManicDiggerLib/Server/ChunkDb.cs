using System.Text;
using System.Data.Common;
using System.Data;
using System.Data.SQLite;

public struct Xyz
{
    public int X;
    public int Y;
    public int Z;

    public override readonly int GetHashCode()
    {
        return X ^ Y ^ Z;
    }

    public override readonly bool Equals(object? obj)
    {
        if (obj is Xyz)
        {
            Xyz other = (Xyz)obj;
            return this.X == other.X && this.Y == other.Y && this.Z == other.Z;
        }
        return base.Equals(obj);
    }
}

public struct DbChunk
{
    public Xyz Position;
    public byte[] Chunk;
}

/// <summary>
/// Used for operations on different chunk storage variants.
/// Does some checks when accessing data.
/// </summary>
public static class ChunkDb
{
    public static byte[] GetChunk(IChunkDb db, int x, int y, int z)
    {
        List<byte[]> chunks = [.. db.GetChunks([new Xyz() { X = x, Y = y, Z = z }])];
        if (chunks.Count > 1)
        {
            throw new Exception();
        }
        if (chunks.Count == 0)
        {
            return null;
        }
        return chunks[0];
    }

    public static void SetChunk(IChunkDb db, int x, int y, int z, byte[] c)
    {
        db.SetChunks([new DbChunk() { Position = new Xyz() { X = x, Y = y, Z = z }, Chunk = c }]);
    }

    public static void DeleteChunk(IChunkDb db, int x, int y, int z)
    {
        db.DeleteChunks([new Xyz() { X = x, Y = y, Z = z }]);
    }

    public static void DeleteChunks(IChunkDb db, List<Xyz> chunks)
    {
        db.DeleteChunks(chunks.ToArray());
    }

    public static Dictionary<Xyz, byte[]> GetChunksFromFile(IChunkDb db, List<Xyz> chunksPositions, string filename)
    {
        return db.GetChunksFromFile([.. chunksPositions], filename);
    }

    public static byte[] GetChunkFromFile(IChunkDb db, int x, int y, int z, string filename)
    {
        Dictionary<Xyz, byte[]> chunks = db.GetChunksFromFile([new Xyz() { X = x, Y = y, Z = z }], filename);
        if (chunks.Count > 1)
        {
            throw new Exception();
        }
        if (chunks.Count == 0)
        {
            return null;
        }
        return chunks[new Xyz() { X = x, Y = y, Z = z }];
    }
}

/// <summary>
/// Interface implemented by all variants of chunk storage.
/// </summary>
public interface IChunkDb
{
    void Open(string filename);
    void Backup(string backupFilename);
    IEnumerable<byte[]> GetChunks(IEnumerable<Xyz> chunkpositions);
    void SetChunks(IEnumerable<DbChunk> chunks);
    void DeleteChunks(IEnumerable<Xyz> chunkpositions);
    byte[] GetGlobalData();
    void SetGlobalData(byte[] data);
    Dictionary<Xyz, byte[]> GetChunksFromFile(IEnumerable<Xyz> chunkpositions, string filename);
    void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename);
    bool GetReadOnly();
    void SetReadOnly(bool value);
}

/// <summary>
/// Wrapper to compress data passed on to the actual chunk storage
/// </summary>
public class ChunkDbCompressed : IChunkDb
{
    public IChunkDb d_ChunkDb;
    public ICompression d_Compression;
    #region IChunkDb Members
    public void Open(string filename)
    {
        d_ChunkDb.Open(filename);
    }
    public void Backup(string backupFilename)
    {
        d_ChunkDb.Backup(backupFilename);
    }
    public IEnumerable<byte[]> GetChunks(IEnumerable<Xyz> chunkpositions)
    {
        foreach (byte[] b in d_ChunkDb.GetChunks(chunkpositions))
        {
            if (b == null)
            {
                yield return null;
            }
            else
            {
                yield return d_Compression.Decompress(b);
            }
        }
    }

    public Dictionary<Xyz, byte[]> GetChunksFromFile(IEnumerable<Xyz> chunkpositions, string filename)
    {
        Dictionary<Xyz, byte[]> decompressedChunks = [];
        foreach (var k in d_ChunkDb.GetChunksFromFile(chunkpositions, filename))
        {
            byte[] c;
            if (k.Value == null)
            {
                c = null;
            }
            else
            {
                c = d_Compression.Decompress(k.Value);
            }
            decompressedChunks.Add(k.Key, c);
        }
        return decompressedChunks;
    }

    public void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename)
    {
        List<DbChunk> compressed = [];
        foreach (DbChunk c in chunks)
        {
            byte[] b;
            if (c.Chunk == null)
            {
                b = null;
            }
            else
            {
                b = d_Compression.Compress(c.Chunk);
            }
            compressed.Add(new DbChunk() { Position = c.Position, Chunk = b });
        }
        d_ChunkDb.SetChunksToFile(compressed, filename);
    }

    public void DeleteChunks(IEnumerable<Xyz> chunkpositions)
    {
        d_ChunkDb.DeleteChunks(chunkpositions);
    }

    public void SetChunks(IEnumerable<DbChunk> chunks)
    {
        List<DbChunk> compressed = [];
        foreach (DbChunk c in chunks)
        {
            byte[] b;
            if (c.Chunk == null)
            {
                b = null;
            }
            else
            {
                b = d_Compression.Compress(c.Chunk);
            }
            compressed.Add(new DbChunk() { Position = c.Position, Chunk = b });
        }
        d_ChunkDb.SetChunks(compressed);
    }

    public byte[] GetGlobalData()
    {
        byte[] globaldata = d_ChunkDb.GetGlobalData();
        if (globaldata == null)
        {
            return null;
        }
        else
        {
            return d_Compression.Decompress(globaldata);
        }
    }

    public void SetGlobalData(byte[] data)
    {
        if (data == null)
        {
            d_ChunkDb.SetGlobalData(null);
        }
        else
        {
            d_ChunkDb.SetGlobalData(d_Compression.Compress(data));
        }
    }

    #endregion

    public bool GetReadOnly() { return d_ChunkDb.GetReadOnly(); }
    public void SetReadOnly(bool value) { d_ChunkDb.SetReadOnly(value); }
}

/// <summary>
/// Chunk storage using SQLite to store data
/// </summary>
public class ChunkDbSqlite : IChunkDb
{
    private SQLiteConnection sqliteConn;
    private string databasefile;

    public void Open(string filename)
    {
        databasefile = filename;
        bool newdatabase = false;
        if (!File.Exists(databasefile))
        {
            newdatabase = true;
        }
        StringBuilder b = new();
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Data Source", databasefile);
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Version", "3");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "New", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Compress", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Journal Mode", "Off");
        sqliteConn = new SQLiteConnection(b.ToString());
        sqliteConn.Open();
        if (newdatabase)
        {
            CreateTables(sqliteConn);
        }
        if (!IntegrityCheck(sqliteConn))
        {
            Console.WriteLine("Database is possibly corrupted.");
            //repair(sqliteConn);
        }
        //vacuum(sqliteBckConn);
    }

    public void Close()
    {
        sqliteConn.Close();
        sqliteConn.Dispose();
    }

    private static void CreateTables(SQLiteConnection sqliteConn)
    {
        SQLiteCommand sqlite_cmd;
        sqlite_cmd = sqliteConn.CreateCommand();
        sqlite_cmd.CommandText = "CREATE TABLE chunks (position integer PRIMARY KEY, data BLOB);";
        sqlite_cmd.ExecuteNonQuery();
    }

    public void Backup(string backupFilename)
    {
        if (databasefile == backupFilename)
        {
            Console.WriteLine(string.Format("Cannot overwrite current running database. Chose another destination."));
            return;
        }
        if (File.Exists(backupFilename))
        {
            Console.WriteLine(string.Format("File {0} exists. Overwriting file.", backupFilename));
        }
        StringBuilder b = new();
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Data Source", backupFilename);
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Version", "3");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "New", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Compress", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Journal Mode", "Off");
        SQLiteConnection sqliteBckConn = new(b.ToString());
        sqliteBckConn.Open();
        sqliteConn.BackupDatabase(sqliteBckConn, sqliteBckConn.Database, sqliteConn.Database, -1, null, 10);
        // shrink database
        Vacuum(sqliteBckConn);
        sqliteBckConn.Close();
        sqliteBckConn.Dispose();
    }

    private static void Vacuum(SQLiteConnection sqliteConn)
    {
        using SQLiteCommand command = sqliteConn.CreateCommand();
        command.CommandText = "vacuum;";
        command.ExecuteNonQuery();
    }

    private static bool IntegrityCheck(SQLiteConnection sqliteConn)
    {
        bool okay = false;
        using (SQLiteCommand command = sqliteConn.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check";
            SQLiteDataReader sqlite_datareader = command.ExecuteReader();
            Console.WriteLine(string.Format("Database: {0}. Running SQLite integrity check:", sqliteConn.DataSource));
            while (sqlite_datareader.Read())
            {
                Console.WriteLine(sqlite_datareader[0].ToString());
                if (sqlite_datareader[0].ToString() == "ok")
                {
                    okay = true;
                    break;
                }
            }
        }
        return okay;
    }

    public IEnumerable<byte[]> GetChunks(IEnumerable<Xyz> chunkpositions)
    {
        using SQLiteTransaction transaction = sqliteConn.BeginTransaction();
        foreach (var xyz in chunkpositions)
        {
            ulong pos = MapUtil.ToMapPos(xyz.X, xyz.Y, xyz.Z);
            yield return GetChunk(pos);
        }
        transaction.Commit();
    }

    public byte[] GetChunk(ulong position)
    {
        if (ReadOnly)
        {
            if (temporaryChunks.TryGetValue(position, out byte[]? value))
            {
                return value;
            }
        }
        SQLiteCommand cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT data FROM chunks WHERE position=?";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        SQLiteDataReader sqlite_datareader = cmd.ExecuteReader();
        while (sqlite_datareader.Read())
        {
            object data = sqlite_datareader["data"];
            return data as byte[];
        }
        return null;
    }

    public void DeleteChunks(IEnumerable<Xyz> chunkpositions)
    {
        using SQLiteTransaction transaction = sqliteConn.BeginTransaction();
        foreach (var xyz in chunkpositions)
        {
            DeleteChunk(MapUtil.ToMapPos(xyz.X, xyz.Y, xyz.Z));
        }
        transaction.Commit();
    }

    public void DeleteChunk(ulong position)
    {
        if (ReadOnly)
        {
            temporaryChunks.Remove(position);
            return;
        }
        DbCommand cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks WHERE position=?";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        cmd.ExecuteNonQuery();
    }

    public void SetChunks(IEnumerable<DbChunk> chunks)
    {
        if (ReadOnly)
        {
            foreach (DbChunk c in chunks)
            {
                ulong pos = MapUtil.ToMapPos(c.Position.X, c.Position.Y, c.Position.Z);
                temporaryChunks[pos] = (byte[])c.Chunk.Clone();
            }
            return;
        }
        using SQLiteTransaction transaction = sqliteConn.BeginTransaction();
        foreach (DbChunk c in chunks)
        {
            ulong pos = MapUtil.ToMapPos(c.Position.X, c.Position.Y, c.Position.Z);
            InsertChunk(pos, c.Chunk);
        }
        transaction.Commit();
    }

    public Dictionary<Xyz, byte[]> GetChunksFromFile(IEnumerable<Xyz> chunkpositions, string filename)
    {
        Dictionary<Xyz, byte[]> chunks = [];
        if (!File.Exists(filename))
        {
            Console.WriteLine(string.Format("File {0} does not exist.", filename));
            return null;
        }
        StringBuilder b = new();
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Data Source", filename);
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Version", "3");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "New", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Compress", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Journal Mode", "Off");
        SQLiteConnection conn = new(b.ToString());
        conn.Open();
        using (SQLiteTransaction transaction = conn.BeginTransaction())
        {
            foreach (var xyz in chunkpositions)
            {
                ulong pos = MapUtil.ToMapPos(xyz.X, xyz.Y, xyz.Z);
                chunks.Add(xyz, GetChunkFromFile(pos, conn));
            }
            transaction.Commit();
            conn.Close();
            conn.Dispose();
        }
        return chunks;
    }

    private static byte[] GetChunkFromFile(ulong position, SQLiteConnection conn)
    {
        SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data FROM chunks WHERE position=?";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        SQLiteDataReader sqlite_datareader = cmd.ExecuteReader();
        while (sqlite_datareader.Read())
        {
            object data = sqlite_datareader["data"];
            return data as byte[];
        }
        return null;
    }

    public void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename)
    {
        bool newDatabase = true;
        if (databasefile == filename)
        {
            Console.WriteLine(string.Format("Cannot overwrite current running database. Chose another destination."));
            return;
        }

        if (File.Exists(filename))
        {
            Console.WriteLine(string.Format("File {0} exists. Overwriting file.", filename));
            newDatabase = false;
        }

        StringBuilder b = new();
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Data Source", filename);
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Version", "3");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "New", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Compress", "True");
        DbConnectionStringBuilder.AppendKeyValuePair(b, "Journal Mode", "Off");
        SQLiteConnection sqliteConn = new(b.ToString());
        sqliteConn.Open();

        if (newDatabase)
        {
            CreateTables(sqliteConn);
        }

        using (SQLiteTransaction transaction = sqliteConn.BeginTransaction())
        {
            foreach (DbChunk c in chunks)
            {
                ulong pos = MapUtil.ToMapPos(c.Position.X, c.Position.Y, c.Position.Z);
                DbCommand cmd = sqliteConn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO chunks (position, data) VALUES (?,?)";
                cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, pos, cmd));
                cmd.Parameters.Add(CreateParameter("data", DbType.Object, c.Chunk, cmd));
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        sqliteConn.Close();
        sqliteConn.Dispose();
    }

    //when read only don't save this to disk
    public Dictionary<ulong, byte[]> temporaryChunks = [];
    private void InsertChunk(ulong position, byte[] data)
    {
        DbCommand cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO chunks (position, data) VALUES (?,?)";
        cmd.Parameters.Add(CreateParameter("position", DbType.UInt64, position, cmd));
        cmd.Parameters.Add(CreateParameter("data", DbType.Object, data, cmd));
        cmd.ExecuteNonQuery();
    }

    private static DbParameter CreateParameter(string parameterName, DbType dbType, object value, DbCommand command)
    {
        DbParameter p = command.CreateParameter();
        p.ParameterName = parameterName;
        p.DbType = dbType;
        p.Value = value;
        return p;
    }

    public byte[] GetGlobalData()
    {
        return GetChunk(ulong.MaxValue / 2);
    }

    public void SetGlobalData(byte[] data)
    {
        InsertChunk(ulong.MaxValue / 2, data);
    }

    private bool ReadOnly;
    public bool GetReadOnly() { return ReadOnly; }
    public void SetReadOnly(bool value) { ReadOnly = value; }
}