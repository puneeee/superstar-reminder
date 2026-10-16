using Dapper;
using MySqlConnector;
namespace SuperstarReminder.Persistence;
public sealed class DatabaseMigrator(string connectionString)
{
    public async Task ApplyAsync(CancellationToken ct)
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"db");
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException("Database migration files were not included in the application.");
        await using var db=new MySqlConnection(connectionString);await db.OpenAsync(ct);
        // DDL auto-commits in MySQL. Each migration is recoverable if the app exits partway through.
        await db.ExecuteAsync(new CommandDefinition("CREATE TABLE IF NOT EXISTS schema_versions(version INT PRIMARY KEY, applied_at_utc DATETIME(6) NOT NULL);",cancellationToken:ct));
        foreach(var file in Directory.EnumerateFiles(directory,"*.sql").OrderBy(Path.GetFileName))
        {
            var version=int.Parse(Path.GetFileName(file).Split('_')[0]);
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM schema_versions WHERE version=@version;",new{version},cancellationToken:ct))>0)continue;
            if(version==2)
            {
                async Task Add(string table,string column,string definition)
                {
                    var found=await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=@table AND column_name=@column;",new{table,column},cancellationToken:ct));
                    // Identifiers and definitions here are fixed migration constants, never user input.
                    if(found==0)await db.ExecuteAsync(new CommandDefinition($"ALTER TABLE {table} ADD COLUMN {column} {definition};",cancellationToken:ct));
                }
                await Add("reminders","weekdays_json","VARCHAR(64) NOT NULL DEFAULT '[]'");
                await Add("reminders","next_due_at_utc","DATETIME(6) NULL");
                await Add("reminders","deleted","BOOLEAN NOT NULL DEFAULT FALSE");
                await Add("reminder_occurrences","title_snapshot","VARCHAR(120) NOT NULL DEFAULT 'Reminder'");
                await db.ExecuteAsync(new CommandDefinition("UPDATE reminders SET next_due_at_utc=starts_at_utc WHERE next_due_at_utc IS NULL AND enabled=1 AND deleted=0;",cancellationToken:ct));
                var index=await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='reminder_occurrences' AND index_name='ux_occurrence_due';",cancellationToken:ct));
                if(index==0)await db.ExecuteAsync(new CommandDefinition("CREATE UNIQUE INDEX ux_occurrence_due ON reminder_occurrences(reminder_id,due_at_utc);",cancellationToken:ct));
            }
            else await db.ExecuteAsync(new CommandDefinition(await File.ReadAllTextAsync(file,ct),cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO schema_versions(version,applied_at_utc) VALUES(@version,UTC_TIMESTAMP(6));",new{version},cancellationToken:ct));
        }
    }
}
