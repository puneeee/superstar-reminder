using Dapper;
using MySqlConnector;
using SuperstarReminder.Domain;
using SuperstarReminder.Persistence;
var connection=Environment.GetEnvironmentVariable("SUPERSTAR_MYSQL_TEST_CONNECTION")??throw new InvalidOperationException("Set SUPERSTAR_MYSQL_TEST_CONNECTION to a disposable MySQL database whose name ends in _test.");
if(!new MySqlConnectionStringBuilder(connection).Database.EndsWith("_test",StringComparison.Ordinal))throw new InvalidOperationException("Tests only run against a database ending in _test.");
var checks=0;void Check(string name,bool condition){if(!condition)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
var ct=CancellationToken.None;await new DatabaseMigrator(connection).ApplyAsync(ct);await new DatabaseMigrator(connection).ApplyAsync(ct);
var repo=new MySqlReminderRepository(connection);var now=DateTimeOffset.UtcNow;var id=Guid.NewGuid();
var r=new Reminder(id,"Database check "+id,"Round trip",true,ScheduleType.Weekdays,now,45,new HashSet<DayOfWeek>{DayOfWeek.Monday,DayOfWeek.Friday},new TimeOnly(22,0),new TimeOnly(6,0),"UTC",null);
try
{
 await repo.UpsertAsync(r,ct);
 var loaded=(await repo.GetAllAsync(ct)).Single(x=>x.Id==id);
 Check("Weekdays round trip",loaded.Weekdays.SetEquals(r.Weekdays));
 Check("MySQL TIME round trip",loaded.ActiveFrom==r.ActiveFrom&&loaded.ActiveTo==r.ActiveTo);
 r=r with{ScheduleType=ScheduleType.OneTime,StartsAtUtc=now.AddSeconds(-10),NextDueAtUtc=now.AddSeconds(-10),ActiveFrom=null,ActiveTo=null};
 await repo.UpsertAsync(r,ct);await repo.GenerateDueAsync(now,ct);await repo.GenerateDueAsync(now,ct);
 var due=(await repo.GetPendingDueAsync(now,ct)).Where(x=>x.ReminderId==id).ToList();Check("Generation is idempotent",due.Count==1);
 Check("One-time next due consumed",(await repo.GetAllAsync(ct)).Single(x=>x.Id==id).NextDueAtUtc is null);
 await repo.RespondAsync(due[0],ReminderOutcome.Snoozed,now,5,ct);await repo.RespondAsync(due[0],ReminderOutcome.Snoozed,now,5,ct);
 Check("Original is no longer pending",!(await repo.GetPendingDueAsync(now,ct)).Any(x=>x.ReminderId==id));
 var later=(await repo.GetPendingDueAsync(now.AddMinutes(5),ct)).Where(x=>x.ReminderId==id).ToList();Check("Snooze persists exactly once",later.Count==1&&later[0].SnoozeMinutes==5);
 await repo.RespondAsync(later[0],ReminderOutcome.Yes,now.AddMinutes(5),null,ct);
 Check("History records snooze and yes",(await repo.GetHistoryAsync(ct)).Count(x=>x.ReminderId==id)==2);
 await repo.DeleteAsync(id,ct);Check("Delete hides reminder",!(await repo.GetAllAsync(ct)).Any(x=>x.Id==id));
 Check("Deleted reminder history keeps title",(await repo.GetHistoryAsync(ct)).Where(x=>x.ReminderId==id).All(x=>x.TitleSnapshot==r.Title));
 Console.WriteLine($"{checks} MySQL integration checks passed.");
}
finally
{
 await using var db=new MySqlConnection(connection);await db.ExecuteAsync("DELETE FROM reminder_occurrences WHERE reminder_id=@id;DELETE FROM reminders WHERE id=@id;",new{id=id.ToString()});
}
