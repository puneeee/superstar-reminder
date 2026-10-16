using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SuperstarReminder.Domain;
using SuperstarReminder.Infrastructure;
using SuperstarReminder.Services;
namespace SuperstarReminder;
public sealed record OverlayResponse(ReminderOutcome Outcome,int? SnoozeMinutes,DateTimeOffset PresentedAt);

public sealed class CharacterPlayer : IDisposable
{
    private readonly Dictionary<string,List<Bitmap>> _frames=[];
    private readonly Image _image;
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(110)};
    private string _action="idle";private int _index;
    public CharacterPlayer(Image image,CharacterDefinition character)
    {
        _image=image;
        using var json=JsonDocument.Parse(File.ReadAllText(character.ManifestPath));
        foreach(var action in json.RootElement.GetProperty("actions").EnumerateObject())
        {
            var frames=new List<Bitmap>();
            foreach(var file in action.Value.GetProperty("frames").EnumerateArray().Take(120))
            {
                var path=CharacterCatalog.FramePath(character,file.GetString()??"");
                if(path is null)continue;
                try{frames.Add(new Bitmap(path));}catch(Exception e)when(e is IOException or ArgumentException){}
            }
            _frames[action.Name]=frames;
        }
        _timer.Tick+=(_,_)=>Advance();_timer.Start();Set("idle");
    }
    public bool HasFrames=>_frames.Values.Any(x=>x.Count>0);
    public void Set(string action){_action=action;_index=0;Advance();}
    private void Advance()
    {
        if(!_frames.TryGetValue(_action,out var frames)||frames.Count==0)_frames.TryGetValue("idle",out frames);
        if(frames is null||frames.Count==0)return;
        _image.Source=frames[_index++%frames.Count];
        // Dedicated left assets are preferred. Packages can opt into mirroring right-facing frames.
        _image.RenderTransform=new ScaleTransform(_action.EndsWith("Left",StringComparison.Ordinal)?-1:1,1);
        _image.RenderTransformOrigin=RelativePoint.Center;
    }
    public void Dispose(){_timer.Stop();foreach(var bitmap in _frames.Values.SelectMany(x=>x).Distinct())bitmap.Dispose();}
}

public sealed class DesktopOverlay : IDisposable
{
    private Window? _characterWindow;private Window? _banner;private CancellationTokenSource? _current;
    private readonly ArrivalAudio _audio=new();
    public async Task<OverlayResponse> PresentAsync(Window owner,Reminder reminder,CharacterDefinition character,LocalSettings settings,CancellationToken ct,Func<OverlayResponse,Task>? onResponse=null)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);_current=linked;
        var token=linked.Token;
        var screen=owner.Screens.Primary??owner.Screens.All.First();var area=screen.WorkingArea;var scale=screen.Scaling;
        var height=Math.Clamp(area.Height/scale*settings.HeightFraction,100,360);var width=height*.62;
        var image=new Image{Stretch=Stretch.Uniform,Width=width,Height=height};
        using var player=new CharacterPlayer(image,character);
        Control content=image;
        if(!player.HasFrames)content=new Border{CornerRadius=new CornerRadius(20),Background=Brushes.DarkSlateGray,Child=new TextBlock{Text="★\nCharacter assets missing",TextAlignment=TextAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap}};
        _characterWindow=new Window{Width=width,Height=height,Content=content,CanResize=false,ShowInTaskbar=false,ShowActivated=false,Topmost=true,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
        var right=settings.Corner==ScreenCorner.BottomRight;
        var targetX=right?area.Right-(int)((width+50)*scale):area.X+(int)(50*scale);
        var y=area.Bottom-(int)((height+8)*scale);var fromX=right?area.Right:area.X-(int)(width*scale);
        _characterWindow.Position=new PixelPoint(fromX,y);_characterWindow.Show();OverlayInput.MakePassive(_characterWindow);
        var response=new TaskCompletionSource<(ReminderOutcome,int?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var title=new TextBlock{Text=reminder.Title,FontWeight=FontWeight.SemiBold,FontSize=18,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap};
        var message=new TextBlock{Text=reminder.Message,Foreground=Brush.Parse("#CAD1DF"),TextWrapping=TextWrapping.Wrap,MaxHeight=90};
        var durations=new ComboBox{ItemsSource=new[]{5,10,20,30},SelectedIndex=0,Width=75};
        var yes=new Button{Content="Yes",Background=Brush.Parse("#E6B544"),Foreground=Brushes.Black};
        var no=new Button{Content="No"};var snooze=new Button{Content="Snooze"};
        yes.Click+=(_,_)=>response.TrySetResult((ReminderOutcome.Yes,null));no.Click+=(_,_)=>response.TrySetResult((ReminderOutcome.No,null));
        snooze.Click+=(_,_)=>response.TrySetResult((ReminderOutcome.Snoozed,(int?)durations.SelectedItem??5));
        var buttons=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=8,Children={yes,no,snooze,durations}};
        var panel=new StackPanel{Spacing=10,Children={title,message,buttons,new TextBlock{Text="Snooze in minutes · Closes after 30 seconds",FontSize=11,Foreground=Brush.Parse("#AAB5CC")}}};
        var border=new Border{Padding=new Thickness(18),CornerRadius=new CornerRadius(16),Background=Brush.Parse("#181E2D"),BorderBrush=Brush.Parse("#685932"),BorderThickness=new Thickness(1),Child=panel};
        _banner=new Window{Width=365,SizeToContent=SizeToContent.Height,Content=border,CanResize=false,ShowInTaskbar=false,ShowActivated=false,Topmost=true,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
        var presented=DateTimeOffset.UtcNow;
        try
        {
            player.Set(right?"walkLeft":"walkRight");await Move(_characterWindow,fromX,targetX,y,650,token);
            player.Set("idle");
            var bannerX=(int)Math.Clamp(targetX+(width*scale/2)-365*scale/2,area.X+8*scale,area.Right-373*scale);
            _banner.Position=new PixelPoint(bannerX,Math.Max(area.Y+8,y-(int)(245*scale)));_banner.Show();
            presented=DateTimeOffset.UtcNow;_audio.Play(reminder.ArrivalAudioPath);
            using var registration=token.Register(()=>response.TrySetCanceled(token));
            var resultTask=await Task.WhenAny(response.Task,Task.Delay(TimeSpan.FromSeconds(30),token));
            token.ThrowIfCancellationRequested();
            var result=resultTask==response.Task?await response.Task:(ReminderOutcome.Unanswered,(int?)null);
            _banner.Close();_banner=null;_audio.Stop();
            if(onResponse is not null)await onResponse(new(result.Item1,result.Item2,presented));
            if(result.Item1==ReminderOutcome.Yes)
            {
                player.Set("happy");await Task.Delay(650,token);player.Set(right?"runLeft":"runRight");
                var destination=right?area.X-(int)(width*scale):area.Right;
                await Move(_characterWindow,targetX,destination,y,2000,token);
            }
            else
            {
                player.Set(right?"walkRight":"walkLeft");await Move(_characterWindow,targetX,fromX,y,550,token);
            }
            return new(result.Item1,result.Item2,presented);
        }
        finally{_audio.Stop();_banner?.Close();_characterWindow?.Close();_banner=null;_characterWindow=null;_current=null;}
    }
    private static async Task Move(Window window,int from,int to,int y,int milliseconds,CancellationToken ct)
    {
        var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<milliseconds)
        {
            ct.ThrowIfCancellationRequested();var t=clock.ElapsedMilliseconds/(double)milliseconds;
            window.Position=new PixelPoint((int)(from+(to-from)*t),y);await Task.Delay(16,ct);
        }
        window.Position=new PixelPoint(to,y);
    }
    public void Cancel()=>_current?.Cancel();
    public void Dispose(){Cancel();_audio.Dispose();}
}
