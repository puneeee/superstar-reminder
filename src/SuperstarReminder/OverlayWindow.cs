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
    private sealed record Clip(List<Bitmap> Frames, bool Loop, double Fps);
    private readonly Dictionary<string, Clip> _clips = [];
    private readonly Image _image;
    private readonly DispatcherTimer _timer = new();
    private Clip? _clip;
    private string _action = "idle";
    private int _index;
    public string CurrentAction => _action;
    public CharacterPlayer(Image image, CharacterDefinition character)
    {
        _image = image;
        using var json = JsonDocument.Parse(File.ReadAllText(character.ManifestPath));
        foreach (var action in json.RootElement.GetProperty("actions").EnumerateObject())
        {
            var frames = new List<Bitmap>();
            foreach (var file in action.Value.GetProperty("frames").EnumerateArray().Take(120))
            {
                var path = CharacterCatalog.FramePath(character, file.GetString() ?? "");
                if (path is null) continue;
                try { frames.Add(new Bitmap(path)); } catch (Exception e) when (e is IOException or ArgumentException) { }
            }
            var fps = action.Value.TryGetProperty("fps", out var speed) ? Math.Clamp(speed.GetDouble(), 1, 30) : 9;
            var loop = !action.Value.TryGetProperty("loop", out var repeat) || repeat.GetBoolean();
            _clips[action.Name] = new(frames, loop, fps);
        }
        _timer.Tick += (_, _) => Advance();
        Set("idle");
    }
    public bool HasFrames => _clips.Values.Any(x => x.Frames.Count > 0);
    public TimeSpan Duration(string action) => _clips.TryGetValue(action, out var clip)
        ? TimeSpan.FromSeconds(clip.Frames.Count / clip.Fps) : TimeSpan.FromMilliseconds(500);
    public void Set(string action)
    {
        _timer.Stop(); _action = action; _index = 0;
        if (!_clips.TryGetValue(action, out _clip) || _clip.Frames.Count == 0)
            _clips.TryGetValue(action.StartsWith("sadWalk", StringComparison.Ordinal) ? action.Replace("sadWalk", "walk") : "idle", out _clip);
        if (_clip is null || _clip.Frames.Count == 0) return;
        _timer.Interval = TimeSpan.FromSeconds(1 / _clip.Fps);
        _image.RenderTransform = new ScaleTransform(action.EndsWith("Left", StringComparison.Ordinal) ? -1 : 1, 1);
        _image.RenderTransformOrigin = RelativePoint.Center;
        Advance(); _timer.Start();
    }
    private void Advance()
    {
        if (_clip is null || _clip.Frames.Count == 0) return;
        if (_index >= _clip.Frames.Count)
        {
            if (!_clip.Loop) { _timer.Stop(); return; }
            _index = 0;
        }
        _image.Source = _clip.Frames[_index++];
    }
    public void Dispose() { _timer.Stop(); foreach (var bitmap in _clips.Values.SelectMany(x => x.Frames).Distinct()) bitmap.Dispose(); }
}

public sealed class DesktopOverlay : IDisposable
{
    private Window? _characterWindow;private Window? _banner;private CancellationTokenSource? _current;
    private readonly ArrivalAudio _audio=new();
    public OverlayStage Stage { get; private set; } = OverlayStage.Hidden;
    public string ActiveAction { get; private set; } = "idle";
    public async Task<OverlayResponse> PresentAsync(Window owner,Reminder reminder,CharacterDefinition character,LocalSettings settings,CancellationToken ct,Func<OverlayResponse,Task>? onResponse=null)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);_current=linked;
        var token=linked.Token;
        var screen=owner.Screens.Primary??owner.Screens.All.First();var area=screen.WorkingArea;var scale=screen.Scaling;
        var height=OverlayPresentation.CharacterHeight(area.Height/scale,settings.HeightFraction);var width=height*.66;
        var image=new Image{Stretch=Stretch.Uniform,Width=width,Height=height};
        using var player=new CharacterPlayer(image,character);
        void Play(string action) { ActiveAction=action; player.Set(action); }
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
        var bubble = new Grid { RowDefinitions = new RowDefinitions("Auto,10") };
        bubble.Children.Add(border);
        var tail = new Avalonia.Controls.Shapes.Path { Data=Geometry.Parse("M 0,0 L 20,0 L 10,10 Z"),
            Fill=Brush.Parse("#181E2D"),Width=20,Height=10,Stretch=Stretch.Fill,
            HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Left };
        Grid.SetRow(tail,1);bubble.Children.Add(tail);
        var bannerWidth=Math.Min(365,area.Width/scale-24);
        _banner=new Window{Width=bannerWidth,SizeToContent=SizeToContent.Height,Opacity=0,Content=bubble,CanResize=false,ShowInTaskbar=false,ShowActivated=false,Topmost=true,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
        var presented=DateTimeOffset.UtcNow;
        try
        {
            Stage=OverlayStage.Arriving;Play(right?"walkLeft":"walkRight");
            await Move(_characterWindow,fromX,targetX,y,1150,token);
            Stage=OverlayStage.Landing;Play("land");await Task.Delay(1000,token);
            Stage=OverlayStage.Flourish;Play("flourish");await Task.Delay(player.Duration("flourish"),token);
            Play("idle");await Task.Delay(250,token);
            // Let native fonts, button templates and wrapping finish layout while invisible.
            _banner.Position=new PixelPoint(targetX,y);_banner.Show();await Task.Delay(60,token);
            var bannerHeight=_banner.Bounds.Height;
            var bannerX=(int)Math.Clamp(targetX+(width-bannerWidth)*scale/2,area.X+12*scale,area.Right-(bannerWidth+12)*scale);
            var bannerY=Math.Max(area.Y+8,(int)(OverlayPresentation.BannerTop((y-area.Y)/scale,height,bannerHeight)*scale+area.Y));
            tail.Margin=new Thickness(Math.Clamp((targetX+width*scale/2-bannerX)/scale-10,18,bannerWidth-38),0,0,0);
            _banner.Position=new PixelPoint(bannerX,bannerY);
            Stage=OverlayStage.AwaitingResponse;
            // A brief pop/fade follows the glasses flourish; response time starts only now.
            _banner.Opacity=0;
            for(var step=1;step<=8;step++){_banner.Opacity=step/8d;await Task.Delay(20,token);}
            presented=DateTimeOffset.UtcNow;_audio.Play(reminder.ArrivalAudioPath);
            using var registration=token.Register(()=>response.TrySetCanceled(token));
            var resultTask=await Task.WhenAny(response.Task,Task.Delay(TimeSpan.FromSeconds(30),token));
            token.ThrowIfCancellationRequested();
            var result=resultTask==response.Task?await response.Task:(ReminderOutcome.Unanswered,(int?)null);
            _banner.Close();_banner=null;_audio.Stop();
            if(onResponse is not null)await onResponse(new(result.Item1,result.Item2,presented));
            Stage=OverlayStage.Reacting;
            Play(result.Item1==ReminderOutcome.Yes?"happy":"sad");
            await Task.Delay(player.Duration(result.Item1==ReminderOutcome.Yes?"happy":"sad"),token);
            Stage=OverlayStage.Saluting;Play("salute");await Task.Delay(player.Duration("salute"),token);
            Stage=OverlayStage.Departing;
            var destination=result.Item1==ReminderOutcome.Yes?(right?area.X-(int)(width*scale):area.Right):fromX;
            Play(OverlayPresentation.ExitAction(result.Item1,destination<targetX));
            var departure=OverlayPresentation.WalkMilliseconds((destination-targetX)/scale,result.Item1==ReminderOutcome.Yes);
            await Move(_characterWindow,targetX,destination,y,departure,token);
            return new(result.Item1,result.Item2,presented);
        }
        finally{_audio.Stop();_banner?.Close();_characterWindow?.Close();_banner=null;_characterWindow=null;_current=null;Stage=OverlayStage.Hidden;ActiveAction="idle";}
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
