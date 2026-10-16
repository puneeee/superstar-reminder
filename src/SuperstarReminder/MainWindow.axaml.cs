using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MySqlConnector;
using SuperstarReminder.Domain;
using SuperstarReminder.Infrastructure;
using SuperstarReminder.Persistence;
using SuperstarReminder.Services;
namespace SuperstarReminder;
public partial class MainWindow : Window
{
    private LocalSettings _settings=LocalSettings.Load();
    private readonly CharacterCatalog _catalog=new();
    private readonly DesktopOverlay _overlay=new();
    private readonly CancellationTokenSource _lifetime=new();
    private readonly SemaphoreSlim _gate=new(1,1);
    private IReminderRepository? _repository;private string? _connection;
    private IReadOnlyList<Reminder> _reminders=[];private IReadOnlyList<ReminderOccurrence> _history=[];
    private bool _allowClose;private bool _wasBlocked;private bool _presenting;private bool _initialized;
    private readonly DispatcherTimer _sessionWatch=new(){Interval=TimeSpan.FromMilliseconds(750)};
    private static string RecoveryPath=>Path.Combine(LocalSettings.DataDirectory,"response-recovery.json");
    public MainWindow()
    {
        InitializeComponent();
        Opened+=async(_,_)=>
        {
            if(_initialized)return;_initialized=true;
            try{_connection=SecretStore.Load();}catch(Exception e)when(e is IOException or System.Security.Cryptography.CryptographicException){SetStatus("Saved login could not be unlocked. Open Settings to reconnect.");}
            if(!string.IsNullOrWhiteSpace(_connection))await Guard(()=>ConnectAsync(_connection));
            else SetStatus("MySQL setup required. Open Settings to connect your local database. Preview works without a database.");
            UpdatePause();_sessionWatch.Start();_ = LoopAsync(_lifetime.Token);
            if(Environment.GetCommandLineArgs().Contains("--background")&&_repository is not null)Hide();
        };
        Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;Hide();}};
        _sessionWatch.Tick+=(_,_)=>
        {
            if(_presenting&&!CanPresent()){_wasBlocked=true;_overlay.Cancel();}
        };
    }
    public void ShowDashboard(){Show();WindowState=WindowState.Normal;Activate();}
    private void SetStatus(string message)=>StatusText.Text=message;
    private bool CanPresent()
    {
        var screen=Screens.Primary??Screens.All.FirstOrDefault();return screen is not null&&SessionState.CanPresent(screen.WorkingArea,screen.Bounds);
    }
    private CharacterDefinition SelectedCharacter=>_catalog.All.FirstOrDefault(x=>x.Id==_settings.CharacterId)??_catalog.Default;
    private async Task ConnectAsync(string connection)
    {
        await new DatabaseMigrator(connection).ApplyAsync(_lifetime.Token);
        _repository=new MySqlReminderRepository(connection);_connection=connection;
        await RecoverAsync();await RefreshAsync();SetStatus("Connected to MySQL · Reminders are running on your primary monitor.");
    }
    private async Task Guard(Func<Task> action)
    {
        if(!_gate.Wait(0)) { SetStatus("Waiting for the current reminder to finish…");await _gate.WaitAsync(); }
        try{await action();}
        catch(MySqlException ex){SetStatus($"MySQL unavailable (code {ex.Number}). Check the service and Settings. Unsaved responses will retry automatically.");}
        catch(OperationCanceledException)when(!_lifetime.IsCancellationRequested){}
        catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException){SetStatus(ex.Message);}
        finally{_gate.Release();}
    }
    private async Task RefreshAsync()
    {
        if(_repository is null)return;
        var selected=SelectedReminder?.Id;
        _reminders=await _repository.GetAllAsync(_lifetime.Token);_history=await _repository.GetHistoryAsync(_lifetime.Token);
        ReminderList.Items.Clear();
        foreach(var reminder in _reminders)
        {
            var detail=reminder.ScheduleType==ScheduleType.Interval?$"Every {reminder.IntervalMinutes} minutes":reminder.ScheduleType.ToString();
            if(reminder.ScheduleType==ScheduleType.Weekdays)detail+=" · "+string.Join(", ",reminder.Weekdays.Select(x=>x.ToString()[..3]));
            if(reminder.ActiveFrom.HasValue)detail+=$" · {reminder.ActiveFrom:HH:mm}–{reminder.ActiveTo:HH:mm}";
            var due=reminder.NextDueAtUtc?.ToLocalTime().ToString("dd MMM, HH:mm")??"No upcoming time";
            var panel=new StackPanel{Spacing=5,Children={new TextBlock{Text=reminder.Title,FontSize=18,FontWeight=FontWeight.SemiBold},new TextBlock{Text=reminder.Message,TextWrapping=TextWrapping.Wrap,Foreground=Brush.Parse("#C1CADB")},new TextBlock{Text=$"{detail} · {(reminder.Enabled?due:"Disabled")}",FontSize=12,Foreground=Brush.Parse("#E6B544")}}};
            var item=new ListBoxItem{Tag=reminder,Content=new Border{Padding=new Thickness(14),Margin=new Thickness(0,0,0,8),CornerRadius=new CornerRadius(10),Background=Brush.Parse("#20283A"),Child=panel},HorizontalContentAlignment=HorizontalAlignment.Stretch};
            ReminderList.Items.Add(item);if(selected==reminder.Id)ReminderList.SelectedItem=item;
        }
        if(_reminders.Count==0)ReminderList.Items.Add(new ListBoxItem{Content="No reminders yet. Create your first reminder above.",IsEnabled=false});
        RenderHistory();
    }
    private Reminder? SelectedReminder=>(ReminderList.SelectedItem as ListBoxItem)?.Tag as Reminder;
    private void RenderHistory()
    {
        HistoryList.Items.Clear();var filter=HistoryFilter.Text?.Trim()??"";
        foreach(var item in _history.Where(x=>filter.Length==0||$"{x.TitleSnapshot} {x.Outcome}".Contains(filter,StringComparison.OrdinalIgnoreCase)))
        {
            var when=item.RespondedAtUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm")??"";
            HistoryList.Items.Add(new ListBoxItem{Content=new StackPanel{Spacing=4,Margin=new Thickness(8),Children={new TextBlock{Text=$"{item.TitleSnapshot} · {item.Outcome}",FontWeight=FontWeight.SemiBold},new TextBlock{Text=$"Due {item.DueAtUtc.ToLocalTime():dd MMM HH:mm} · {when}"+(item.SnoozeMinutes.HasValue?$" · {item.SnoozeMinutes} min snooze":""),FontSize=12,Foreground=Brush.Parse("#AAB5CC")}}}});
        }
        if(HistoryList.Items.Count==0)HistoryList.Items.Add(new ListBoxItem{Content="No matching responses yet.",IsEnabled=false});
    }
    private async Task LoopAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5),ct);
                if(_repository is null||_settings.Paused||!await _gate.WaitAsync(0,ct))continue;
                try
                {
                    await RecoverAsync();
                    await _repository.GenerateDueAsync(DateTimeOffset.UtcNow,ct);
                    var due=await _repository.GetPendingDueAsync(DateTimeOffset.UtcNow,ct);
                    if(!CanPresent()){_wasBlocked=true;continue;}
                    if(due.Count==0){_wasBlocked=false;continue;}
                    if(_wasBlocked||due.Any(x=>DateTimeOffset.UtcNow-x.DueAtUtc>TimeSpan.FromMinutes(1)))
                    {
                        await _repository.DeferAsync(due,ct);_wasBlocked=false;await RefreshAsync();
                        SetStatus($"{due.Count} missed reminders were grouped into one catch-up summary. Review Response history.");
                        await CatchUpNotification.ShowAsync(due.Count,ct);continue;
                    }
                    var all=await _repository.GetAllAsync(ct);
                    foreach(var occurrence in due)
                    {
                        var r=all.FirstOrDefault(x=>x.Id==occurrence.ReminderId&&x.Enabled);if(r is null)continue;
                        if(!CanPresent()){_wasBlocked=true;break;}
                        _presenting=true;OverlayResponse response;
                        try
                        {
                            response=await _overlay.PresentAsync(this,r,SelectedCharacter,_settings,ct,async selected=>
                            {
                                // Persist the action before playing the exit animation.
                                var saved=new ResponseRecovery(occurrence with{PresentedAtUtc=selected.PresentedAt},selected.Outcome,selected.SnoozeMinutes,DateTimeOffset.UtcNow);
                                Directory.CreateDirectory(LocalSettings.DataDirectory);
                                await File.WriteAllTextAsync(RecoveryPath+".tmp",JsonSerializer.Serialize(saved),ct);File.Move(RecoveryPath+".tmp",RecoveryPath,true);
                                try{await RecoverAsync();}
                                catch(MySqlException){SetStatus("Response retained locally. Waiting for MySQL to save it.");}
                            });
                        }
                        finally{_presenting=false;}
                        await RecoverAsync();SetStatus($"{r.Title}: {response.Outcome} saved.");
                    }
                    await RefreshAsync();
                }
                finally{_gate.Release();}
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(OperationCanceledException){_wasBlocked=true;}
            catch(MySqlException ex){SetStatus($"MySQL unavailable (code {ex.Number}). Responses are retained for retry. Check the service or Settings.");}
            catch(Exception ex)when(ex is IOException or InvalidOperationException or ArgumentException or JsonException){SetStatus("Reminder engine needs attention: "+ex.Message);}
        }
    }
    private async Task RecoverAsync()
    {
        if(_repository is null||!File.Exists(RecoveryPath))return;
        var response=JsonSerializer.Deserialize<ResponseRecovery>(await File.ReadAllTextAsync(RecoveryPath,_lifetime.Token))??throw new InvalidOperationException("Response recovery file could not be read.");
        await _repository.RespondAsync(response.Occurrence,response.Outcome,response.At,response.SnoozeMinutes,_lifetime.Token);File.Delete(RecoveryPath);
    }
    private async void NewClicked(object? sender,RoutedEventArgs e)=>await EditReminder(null);
    private async void EditClicked(object? sender,RoutedEventArgs e){if(SelectedReminder is { } r)await EditReminder(r);else SetStatus("Select a reminder to edit.");}
    private async Task EditReminder(Reminder? original)
    {
        if(_repository is null){SetStatus("Connect MySQL in Settings before creating reminders.");return;}
        var result=await new ReminderEditor(original).ShowDialog<Reminder?>(this);if(result is null)return;
        await Guard(async()=>{await _repository.UpsertAsync(result,_lifetime.Token);await RefreshAsync();SetStatus("Reminder saved to MySQL.");});
    }
    private async void ToggleClicked(object? sender,RoutedEventArgs e)
    {
        if(SelectedReminder is not { } r||_repository is null)return;
        await Guard(async()=>{var updated=r with{Enabled=!r.Enabled};updated=updated with{NextDueAtUtc=ScheduleCalculator.Next(updated,DateTimeOffset.UtcNow)};await _repository.UpsertAsync(updated,_lifetime.Token);await RefreshAsync();});
    }
    private async void DeleteClicked(object? sender,RoutedEventArgs e)
    {
        if(SelectedReminder is not { } r||_repository is null)return;
        var confirm=new Window{Title="Delete reminder?",Width=400,Height=190,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var yes=new Button{Content="Delete reminder"};var no=new Button{Content="Cancel"};yes.Click+=(_,_)=>confirm.Close(true);no.Click+=(_,_)=>confirm.Close(false);
        confirm.Content=new StackPanel{Margin=new Thickness(22),Spacing=15,Children={new TextBlock{Text=$"Delete {r.Title}? Response history will be kept.",TextWrapping=TextWrapping.Wrap},new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={yes,no}}}};
        if(await confirm.ShowDialog<bool>(this))await Guard(async()=>{await _repository.DeleteAsync(r.Id,_lifetime.Token);await RefreshAsync();SetStatus("Reminder deleted. History retained.");});
    }
    private async void PreviewClicked(object? sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        var reminder=SelectedReminder??ReminderFactory.WaterEvery(45);
        SetStatus("Preview · Responses here do not change reminder history.");
        await _overlay.PresentAsync(this,reminder,SelectedCharacter,_settings,_lifetime.Token);
    });
    private async void SettingsClicked(object? sender,RoutedEventArgs e)
    {
        var result=await new SettingsWindow(_connection,_settings,_catalog).ShowDialog<SettingsResult?>(this);
        if(result is null)return;_settings=result.Settings;UpdatePause();await Guard(()=>ConnectAsync(result.ConnectionString));
    }
    private void UpdatePause()=>PauseButton.Content=_settings.Paused?"Resume reminders":"Pause reminders";
    public void TogglePause(){_settings.Paused=!_settings.Paused;_settings.Save();UpdatePause();SetStatus(_settings.Paused?"Reminders paused.":"Reminders resumed.");}
    private void PauseClicked(object? sender,RoutedEventArgs e)=>TogglePause();
    private async void RefreshClicked(object? sender,RoutedEventArgs e)=>await Guard(RefreshAsync);
    private void HistoryFilterChanged(object? sender,TextChangedEventArgs e){if(HistoryList is not null)RenderHistory();}
    private void QuitClicked(object? sender,RoutedEventArgs e)=>Quit();
    public void Quit()
    {
        _allowClose=true;_lifetime.Cancel();_sessionWatch.Stop();_overlay.Dispose();Close();
        if(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)desktop.Shutdown();
    }
    private sealed record ResponseRecovery(ReminderOccurrence Occurrence,ReminderOutcome Outcome,int? SnoozeMinutes,DateTimeOffset At);
}
