using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class LibraryStore
{
    private string? lastAutoBackupSignature;
    internal string DatabaseSignature()
    {
        // Stable, ordered streaming hash; includes unfinished-session checkpoints.
        using var db=Open();using var tx=db.BeginTransaction(deferred:true);
        using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        void Add(string value){var bytes=System.Text.Encoding.UTF8.GetBytes(value);hash.AppendData(BitConverter.GetBytes(bytes.Length));hash.AppendData(bytes);}
        foreach(var table in new[]{"games","sessions","daily_records","imports","emulator_profiles","pcs","settings","active_session"}){
            Add(table);using var c=db.CreateCommand();c.Transaction=tx;
            c.CommandText=table=="imports"?"SELECT hash,imported_at FROM imports ORDER BY hash":table=="settings"?"SELECT key,value FROM settings ORDER BY key":$"SELECT CAST(id AS TEXT),payload FROM {table} ORDER BY id";
            using var reader=c.ExecuteReader();while(reader.Read()){Add(reader.GetString(0));Add(reader.GetString(1));}
        }
        tx.Commit();return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal string? CreateAutoBackupIfChanged()
    {
        string signature=DatabaseSignature();
        if(signature==lastAutoBackupSignature&&Directory.Exists(BackupFolder)&&Directory.EnumerateFiles(BackupFolder,"auto-*.db").Any())return null;
        string file=CreateBackup("auto");lastAutoBackupSignature=signature;return file;
    }
    internal string BackupFolder=>Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"backups");
    private static SqliteConnection ReadBackup(string file)
    {
        if(!File.Exists(file))throw new FileNotFoundException("Копия не найдена");
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.GetFullPath(file),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();return connection;
    }
    internal static void ValidateBackup(string file)
    {
        using var db=ReadBackup(file);using var c=db.CreateCommand();c.CommandText="PRAGMA quick_check";
        if(!string.Equals(c.ExecuteScalar() as string,"ok",StringComparison.Ordinal))throw new InvalidDataException("Копия повреждена: проверка SQLite не пройдена");
        c.CommandText="PRAGMA user_version";if((long)c.ExecuteScalar()! !=1)throw new InvalidDataException("Версия базы не поддерживается");
        foreach(var table in new[]{"games","sessions","daily_records","imports","active_session","emulator_profiles","pcs","settings"}) {
            c.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";c.Parameters.Clear();c.Parameters.AddWithValue("$name",table);
            if((long)c.ExecuteScalar()! !=1)throw new InvalidDataException("Это не резервная копия Questlio: нет таблицы "+table);
        }
        foreach(var table in new[]{"games","sessions","daily_records","active_session","emulator_profiles","pcs"}){
            c.CommandText=$"SELECT payload FROM {table}";c.Parameters.Clear();using var r=c.ExecuteReader();while(r.Read())if(JsonNode.Parse(r.GetString(0)) is not JsonObject)throw new InvalidDataException("Повреждены записи "+table);
        }
    }
    internal string CreateBackup(string kind="manual")
    {
        if(kind is not("auto" or "manual" or "before-restore" or "before-steam"))throw new ArgumentException("Invalid backup kind");
        Directory.CreateDirectory(BackupFolder);
        string file=Path.Combine(BackupFolder,$"{kind}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.db");
        if(File.Exists(file))throw new IOException("Копия уже существует");
        try {
            using(var source=Open())using(var destination=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file,Pooling=false}.ToString())){destination.Open();source.BackupDatabase(destination);}
            ValidateBackup(file);
            if(kind=="auto")foreach(var old in new DirectoryInfo(BackupFolder).EnumerateFiles("auto-*.db").OrderByDescending(f=>f.Name,StringComparer.Ordinal).Skip(5)){
                var root=Path.GetFullPath(BackupFolder)+Path.DirectorySeparatorChar;
                if(!old.FullName.StartsWith(root,StringComparison.OrdinalIgnoreCase)||(old.Attributes&FileAttributes.ReparsePoint)!=0)continue;
                old.Delete();
            }
            return file;
        }catch{if(File.Exists(file))File.Delete(file);throw;}
    }
    internal string RestoreBackup(string file)
    {
        if(string.Equals(Path.GetFullPath(file),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Нельзя восстановить базу из самой себя");
        Directory.CreateDirectory(BackupFolder);var staged=Path.Combine(BackupFolder,".restore-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var source=ReadBackup(file))using(var destination=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=staged,Pooling=false}.ToString())){destination.Open();source.BackupDatabase(destination);}
            ValidateBackup(staged);var safety=CreateBackup("before-restore");
            using(var source=ReadBackup(staged))using(var destination=Open())source.BackupDatabase(destination);
            return safety;
        }finally{if(File.Exists(staged))File.Delete(staged);}
    }
    internal JsonArray BackupList()
    {
        if(!Directory.Exists(BackupFolder))return new();
        return new JsonArray(new DirectoryInfo(BackupFolder).EnumerateFiles("*.db").OrderByDescending(f=>f.LastWriteTimeUtc).Select(f=>(JsonNode?)new JsonObject{["name"]=f.Name,["bytes"]=f.Length,["date"]=f.LastWriteTimeUtc.ToString("O"),["kind"]=f.Name.StartsWith("auto-")?"Автоматическая":f.Name.StartsWith("before-restore-")?"Перед восстановлением":f.Name.StartsWith("before-steam-")?"Перед импортом Steam":"Ручная"}).ToArray());
    }
}
