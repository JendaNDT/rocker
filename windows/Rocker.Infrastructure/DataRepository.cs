// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Data.Sqlite;
using Rocker.Domain;
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record DataSnapshot(IReadOnlyList<DataDocument> Documents, IReadOnlyList<string> Warnings);
public sealed class DataRepository
{
    public const int SchemaVersion = 1;
    public string DatabasePath { get; }
    private readonly SemaphoreSlim writer = new(1,1);
    private DataRepository(string path) => DatabasePath = path;
    public static async Task<DataRepository> OpenAsync(UserFiles files, CancellationToken cancellation = default)
    {
        var repository = new DataRepository(Path.Combine(files.DirectoryFor("database"),"rocker.db"));
        await Task.Run(() => repository.Initialize(),cancellation); return repository;
    }
    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false, DefaultTimeout = 5 }.ToString());
        connection.Open(); return connection;
    }
    private static object? Scalar(SqliteConnection connection, string sql) { using var cmd = connection.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar(); }
    private static void Execute(SqliteConnection connection, string sql) { using var cmd = connection.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
    private void Initialize()
    {
        using var connection = Connect();
        long version = (long)(Scalar(connection,"PRAGMA user_version") ?? 0L);
        if (version > SchemaVersion) throw new InvalidDataException("Databáze pochází z novější verze Rockeru. Nebude přepsána.");
        if (version == 0 && (long)(Scalar(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") ?? 0L) > 0)
            throw new InvalidDataException("Databáze má neznámé schéma. Nebude přepsána.");
        Execute(connection,"PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;");
        if (version == 0)
        {
            using var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = """
                CREATE TABLE documents (
                    kind TEXT NOT NULL, id TEXT NOT NULL, schema_version INTEGER NOT NULL CHECK(schema_version>0),
                    payload TEXT NOT NULL CHECK(length(payload)<=1048576 AND json_valid(payload)),
                    updated_utc TEXT NOT NULL, PRIMARY KEY(kind,id));
                CREATE INDEX documents_updated ON documents(updated_utc);
                PRAGMA user_version=1;
                """;
            cmd.ExecuteNonQuery(); transaction.Commit();
        }
        if ((string?)Scalar(connection,"PRAGMA quick_check") != "ok") throw new InvalidDataException("Kontrola databáze zjistila poškození. Použij zálohu; původní soubor zůstává zachovaný.");
    }
    public async Task SaveAsync(IEnumerable<DataDocument> documents, bool replaceAll = false, CancellationToken cancellation = default)
    {
        // Freeze the caller's batch before scheduling work; JsonElements own cloned values.
        var owned = documents.Select(d => d with { Value = d.Value.Clone() }).ToArray();
        await writer.WaitAsync(cancellation);
        try
        {
            await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (owned.Length > 10000) throw new InvalidDataException("Příliš mnoho záznamů v jedné operaci.");
                var normalized = owned.Select(UserData.Normalize).ToArray();
                if (normalized.Sum(d => (long)d.Value.GetRawText().Length) > 32*1024*1024 || normalized.Select(d => (d.Kind,d.Id)).Distinct().Count() != normalized.Length)
                    throw new InvalidDataException("Datová operace je příliš velká nebo obsahuje duplicitní ID.");
                using var connection = Connect(); Execute(connection,"PRAGMA synchronous=FULL;");
                if ((long)(Scalar(connection,"PRAGMA user_version") ?? 0L) != SchemaVersion) throw new InvalidDataException("Schéma databáze se změnilo.");
                using var transaction = connection.BeginTransaction();
                if (replaceAll) { using var clear = connection.CreateCommand(); clear.Transaction = transaction; clear.CommandText = "DELETE FROM documents"; clear.ExecuteNonQuery(); }
                foreach (var document in normalized)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO documents(kind,id,schema_version,payload,updated_utc) VALUES($kind,$id,1,$payload,$time) ON CONFLICT(kind,id) DO UPDATE SET schema_version=excluded.schema_version,payload=excluded.payload,updated_utc=excluded.updated_utc";
                    cmd.Parameters.AddWithValue("$kind",document.Kind); cmd.Parameters.AddWithValue("$id",document.Id);
                    cmd.Parameters.AddWithValue("$payload",document.Value.GetRawText()); cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));
                    cmd.ExecuteNonQuery();
                }
                cancellation.ThrowIfCancellationRequested(); transaction.Commit();
            },cancellation);
        }
        finally { writer.Release(); }
    }
    public async Task<DataSnapshot> SnapshotAsync(CancellationToken cancellation = default)
    {
        await writer.WaitAsync(cancellation);
        try { return await SnapshotCoreAsync(cancellation); } finally { writer.Release(); }
    }
    private Task<DataSnapshot> SnapshotCoreAsync(CancellationToken cancellation) => Task.Run(() =>
    {
        cancellation.ThrowIfCancellationRequested();
        using var connection = Connect();
        if ((long)(Scalar(connection,"PRAGMA user_version") ?? 0L) != SchemaVersion) throw new InvalidDataException("Schéma databáze není podporované.");
        using var transaction = connection.BeginTransaction(deferred:true);
        using var cmd = connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = "SELECT kind,id,schema_version,payload FROM documents ORDER BY kind,id";
        using var reader = cmd.ExecuteReader(); var documents = new List<DataDocument>(); var warnings = new List<string>();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            string kind = reader.GetString(0), id = reader.GetString(1);
            try
            {
                using var json = JsonDocument.Parse(reader.GetString(3),new JsonDocumentOptions { MaxDepth = 32 });
                var original = new DataDocument(kind,id,reader.GetInt32(2),json.RootElement.Clone()); var normalized = UserData.Normalize(original);
                documents.Add(normalized);
                if (!JsonElement.DeepEquals(original.Value,normalized.Value)) warnings.Add($"Záznam {kind}/{id} byl při načtení normalizován.");
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
            { warnings.Add($"Záznam {kind}/{id} byl vynechán kvůli neplatným datům."); }
            if (documents.Count + warnings.Count > 20000) throw new InvalidDataException("Databáze obsahuje příliš mnoho záznamů.");
        }
        transaction.Commit(); return new DataSnapshot(documents,warnings);
    },cancellation);
    public Task RemoveAsync(string kind,string id,CancellationToken cancellation=default)=>RemoveManyAsync([(kind,id)],cancellation);
    public async Task RemoveManyAsync(IEnumerable<(string Kind,string Id)> keys,CancellationToken cancellation=default)
    {
        var owned=keys.Distinct().ToArray();if(owned.Length>10000 || owned.Any(k=>!UserData.Kinds.Contains(k.Kind)||!UserData.ValidId(k.Id)))throw new ArgumentException("Neplatné záznamy.");
        await writer.WaitAsync(cancellation);try
        {
            await Task.Run(()=>{using var connection=Connect();Execute(connection,"PRAGMA synchronous=FULL;");if((long)(Scalar(connection,"PRAGMA user_version")??0L)!=SchemaVersion)throw new InvalidDataException("Schéma databáze se změnilo.");using var transaction=connection.BeginTransaction();foreach(var key in owned){cancellation.ThrowIfCancellationRequested();using var cmd=connection.CreateCommand();cmd.Transaction=transaction;cmd.CommandText="DELETE FROM documents WHERE kind=$kind AND id=$id";cmd.Parameters.AddWithValue("$kind",key.Kind);cmd.Parameters.AddWithValue("$id",key.Id);cmd.ExecuteNonQuery();}cancellation.ThrowIfCancellationRequested();transaction.Commit();},cancellation);
        }finally{writer.Release();}
    }
    public async Task<T> ExclusiveAsync<T>(Func<DataSnapshot,Task<T>> operation, CancellationToken cancellation = default)
    {
        await writer.WaitAsync(cancellation);
        try { return await operation(await SnapshotCoreAsync(cancellation)); } finally { writer.Release(); }
    }
    public async Task BackupDatabaseAsync(string destination, CancellationToken cancellation = default)
    {
        await writer.WaitAsync(cancellation);
        try { await Task.Run(() => { using var source = Connect(); using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString()); target.Open(); source.BackupDatabase(target); },cancellation); }
        finally { writer.Release(); }
    }
}
