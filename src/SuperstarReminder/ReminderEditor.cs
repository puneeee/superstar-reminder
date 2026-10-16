using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SuperstarReminder.Domain;
using SuperstarReminder.Infrastructure;
namespace SuperstarReminder;
public sealed class ReminderEditor : Window
{
    private readonly TextBox _title=new(){MaxLength=120,Watermark="Drink water"};
    private readonly TextBox _message=new(){MaxLength=500,AcceptsReturn=true,Height=75,TextWrapping=TextWrapping.Wrap,Watermark="Time to drink water. Ready?"};
    private readonly ComboBox _schedule=new(){ItemsSource=Enum.GetValues<ScheduleType>(),SelectedIndex=3};
    private readonly DatePicker _date=new(){SelectedDate=DateTimeOffset.Now};
    private readonly TimePicker _time=new(){SelectedTime=DateTime.Now.AddMinutes(1).TimeOfDay,ClockIdentifier="24HourClock"};
    private readonly NumericUpDown _interval=new(){Minimum=1,Maximum=525600,Value=45};
    private readonly CheckBox _active=new(){Content="Limit to active hours"};
    private readonly TimePicker _from=new(){SelectedTime=TimeSpan.FromHours(9),ClockIdentifier="24HourClock"};
    private readonly TimePicker _to=new(){SelectedTime=TimeSpan.FromHours(21),ClockIdentifier="24HourClock"};
    private readonly CheckBox _enabled=new(){Content="Enabled",IsChecked=true};
    private readonly TextBlock _audio=new(){Text="Silent",TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock _error=new(){Foreground=Brushes.Salmon,TextWrapping=TextWrapping.Wrap};
    private readonly Dictionary<DayOfWeek,CheckBox> _days=[];
    private readonly Reminder? _original;private string? _audioPath;
    public ReminderEditor(Reminder? reminder=null)
    {
        _original=reminder;Title=reminder is null?"New reminder":"Edit reminder";Width=600;Height=800;Background=Brush.Parse("#10131B");MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Spacing=10,Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text=Title,FontSize=24,FontWeight=FontWeight.SemiBold});
        Add(panel,"Title",_title);Add(panel,"Message",_message);Add(panel,"Schedule",_schedule);
        var start=new WrapPanel{Orientation=Orientation.Horizontal,Children={_date,_time}};_date.Margin=new Thickness(0,0,12,8);_time.Margin=new Thickness(0,0,0,8);Add(panel,"Start date and local time",start);
        var intervalPanel=new StackPanel{Spacing=5};Add(intervalPanel,"Every X minutes",_interval);panel.Children.Add(intervalPanel);
        var weekdays=new WrapPanel{Orientation=Orientation.Horizontal};
        foreach(var day in Enum.GetValues<DayOfWeek>()){var box=new CheckBox{Content=day.ToString()[..3],Margin=new Thickness(0,0,10,0)};_days[day]=box;weekdays.Children.Add(box);}
        panel.Children.Add(weekdays);panel.Children.Add(_active);
        var activePanel=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Children={_from,new TextBlock{Text="to",VerticalAlignment=VerticalAlignment.Center},_to}};panel.Children.Add(activePanel);
        panel.Children.Add(new TextBlock{Text="Uses the time zone of this computer. Active hours may cross midnight.",TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Brushes.Gray});
        var choose=new Button{Content="Choose local WAV / MP3"};choose.Click+=async(_,_)=>await ChooseAudio();
        var clear=new Button{Content="Remove audio"};clear.Click+=(_,_)=>{_audioPath=null;_audio.Text="Silent";};
        panel.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={choose,clear}});panel.Children.Add(_audio);panel.Children.Add(_enabled);
        var save=new Button{Content="Save reminder",Background=Brush.Parse("#E6B544"),Foreground=Brushes.Black};save.Click+=(_,_)=>Save();
        var cancel=new Button{Content="Cancel"};cancel.Click+=(_,_)=>Close();
        var footer=new StackPanel{Margin=new Thickness(24,8,24,20),Spacing=8,Children={_error,new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={save,cancel}}}};
        var root=new Grid{RowDefinitions=new RowDefinitions("*,Auto")};root.Children.Add(new ScrollViewer{Content=panel});Grid.SetRow(footer,1);root.Children.Add(footer);Content=root;
        void Update(){intervalPanel.IsVisible=_schedule.SelectedItem is ScheduleType.Interval;weekdays.IsVisible=_schedule.SelectedItem is ScheduleType.Weekdays;activePanel.IsVisible=_active.IsChecked==true;}
        _schedule.SelectionChanged+=(_,_)=>Update();_active.IsCheckedChanged+=(_,_)=>Update();
        if(reminder is not null)
        {
            _title.Text=reminder.Title;_message.Text=reminder.Message;_schedule.SelectedItem=reminder.ScheduleType;_enabled.IsChecked=reminder.Enabled;
            var local=TimeZoneInfo.ConvertTime(reminder.StartsAtUtc,TimeZoneInfo.FindSystemTimeZoneById(reminder.TimeZoneId));_date.SelectedDate=local;_time.SelectedTime=local.TimeOfDay;
            _interval.Value=reminder.IntervalMinutes??45;foreach(var day in reminder.Weekdays)_days[day].IsChecked=true;
            _active.IsChecked=reminder.ActiveFrom.HasValue;_from.SelectedTime=reminder.ActiveFrom?.ToTimeSpan()??TimeSpan.FromHours(9);_to.SelectedTime=reminder.ActiveTo?.ToTimeSpan()??TimeSpan.FromHours(21);
            _audioPath=reminder.ArrivalAudioPath;_audio.Text=_audioPath is null?"Silent":Path.GetFileName(_audioPath);
        }
        else foreach(var day in new[]{DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday})_days[day].IsChecked=true;
        Update();
    }
    private static void Add(StackPanel panel,string label,Control control){panel.Children.Add(new TextBlock{Text=label,FontSize=12,Foreground=Brush.Parse("#AAB5CC")});panel.Children.Add(control);}
    private async Task ChooseAudio()
    {
        var result=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{AllowMultiple=false,Title="Arrival audio",FileTypeFilter=[new FilePickerFileType("Audio"){Patterns=["*.wav","*.mp3"]}]});
        if(result.Count==0)return;var path=result[0].TryGetLocalPath();if(path is null)return;
        try
        {
            if(new FileInfo(path).Length>25*1024*1024)throw new ArgumentException("Choose an audio file under 25 MB.");
            var ext=Path.GetExtension(path).ToLowerInvariant();if(ext is not (".mp3" or ".wav"))throw new ArgumentException("Choose WAV or MP3 audio.");
            var dir=Path.Combine(LocalSettings.DataDirectory,"media");Directory.CreateDirectory(dir);var destination=Path.Combine(dir,Guid.NewGuid()+ext);File.Copy(path,destination);
            _audioPath=destination;_audio.Text=Path.GetFileName(path);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_error.Text=e.Message;}
    }
    private void Save()
    {
        try
        {
            if(_date.SelectedDate is null||_time.SelectedTime is null)throw new ArgumentException("Choose a start date and time.");
            var type=(ScheduleType)(_schedule.SelectedItem??ScheduleType.Interval);
            var local=_date.SelectedDate.Value.Date+_time.SelectedTime.Value;
            var zone=_original is null?TimeZoneInfo.Local:TimeZoneInfo.FindSystemTimeZoneById(_original.TimeZoneId);
            var starts=ScheduleCalculator.ToUtc(local,zone);
            if(type==ScheduleType.OneTime&&starts<=DateTimeOffset.UtcNow)throw new ArgumentException("Choose a future time for a one-time reminder.");
            var r=new Reminder(_original?.Id??Guid.NewGuid(),_title.Text?.Trim()??"",_message.Text?.Trim()??"",_enabled.IsChecked==true,type,starts,
                type==ScheduleType.Interval?(int?)_interval.Value:null,_days.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key).ToHashSet(),
                _active.IsChecked==true?TimeOnly.FromTimeSpan(_from.SelectedTime??TimeSpan.Zero):null,
                _active.IsChecked==true?TimeOnly.FromTimeSpan(_to.SelectedTime??TimeSpan.Zero):null,zone.Id,_audioPath);
            ScheduleCalculator.Validate(r);
            r=r with{NextDueAtUtc=ScheduleCalculator.Next(r,DateTimeOffset.UtcNow)};
            if(r.Enabled&&r.NextDueAtUtc is null)throw new ArgumentException("This schedule has no valid future time within the selected active hours.");
            Close(r);
        }
        catch(Exception e)when(e is ArgumentException or TimeZoneNotFoundException){_error.Text=e.Message;}
    }
}
