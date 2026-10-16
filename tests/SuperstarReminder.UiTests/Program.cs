using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;
using SuperstarReminder;
using SuperstarReminder.Services;
using SuperstarReminder.Infrastructure;
using SuperstarReminder.Domain;
AppBuilder.Configure<App>().UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext());
var checks=0;
void Check(string name,bool value){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
var main=new MainWindow();main.Show();Dispatcher.UIThread.RunJobs();
Check("Dashboard loads real AXAML",main.FindControl<TextBlock>("StatusText") is not null);
Check("No fake reminder rows",main.FindControl<ListBox>("ReminderList")!.Items.Count==0);
var editor=new ReminderEditor();editor.Show();Dispatcher.UIThread.RunJobs();
Check("Editor renders",editor.Content is Grid);
var catalog=new CharacterCatalog();Check("Character catalog loads packages",catalog.All.Count>=2);
foreach(var character in catalog.All)
{
 using var player=new CharacterPlayer(new Image(),character);Check("Character frames load: "+character.Id,player.HasFrames);
}
Check("Package traversal rejected",CharacterCatalog.FramePath(catalog.Default,"../../../../../README.md") is null);
var settings=new SettingsWindow(null,new LocalSettings(),catalog);settings.Show();Dispatcher.UIThread.RunJobs();
Check("First-run MySQL setup renders",settings.Content is Grid);
var output=Environment.GetEnvironmentVariable("SUPERSTAR_TEST_SCREENSHOTS");
if(!string.IsNullOrWhiteSpace(output))
{
 Directory.CreateDirectory(output);
 using var a=main.CaptureRenderedFrame();a?.Save(Path.Combine(output,"dashboard.png"));
 using var b=editor.CaptureRenderedFrame();b?.Save(Path.Combine(output,"editor.png"));
 using var c=settings.CaptureRenderedFrame();c?.Save(Path.Combine(output,"settings.png"));
}
settings.Close();editor.Close();
var overlay=new DesktopOverlay();
void PumpUntil(Func<bool> condition,int timeoutMs)
{
 var clock=System.Diagnostics.Stopwatch.StartNew();
 while(!condition()&&clock.ElapsedMilliseconds<timeoutMs){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
 if(!condition())throw new Exception("Overlay did not reach its expected state.");
}
Window? CurrentBanner()=>typeof(DesktopOverlay).GetField("_banner",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(overlay) as Window;
foreach(var outcome in new[]{ReminderOutcome.Yes,ReminderOutcome.No,ReminderOutcome.Snoozed,ReminderOutcome.Unanswered})
{
 var request=overlay.PresentAsync(main,ReminderFactory.WaterEvery(45),catalog.Default,new LocalSettings(),CancellationToken.None);
 PumpUntil(()=>CurrentBanner()?.IsVisible==true,5000);
 var banner=CurrentBanner()!;
 if(output is not null && outcome==ReminderOutcome.Yes)
 {
  using var prompt=banner.CaptureRenderedFrame();prompt?.Save(Path.Combine(output,"reminder-banner.png"));
  var cw=(Window)typeof(DesktopOverlay).GetField("_characterWindow",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(overlay)!;
  using var actor=cw.CaptureRenderedFrame();actor?.Save(Path.Combine(output,"character.png"));
 }
 if(outcome!=ReminderOutcome.Unanswered)
 {
  var label=outcome==ReminderOutcome.Yes?"Yes":outcome==ReminderOutcome.No?"No":"Snooze";
  var button=banner.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==label);
  button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 }
 PumpUntil(()=>request.IsCompleted,35000);
 var result=request.GetAwaiter().GetResult();Check("Overlay response: "+outcome,result.Outcome==outcome);
 if(outcome==ReminderOutcome.Snoozed)Check("Snooze selection returned",result.SnoozeMinutes==5);
 Check("Overlay windows cleaned up",CurrentBanner() is null);
}
overlay.Dispose();main.Hide();
Console.WriteLine($"{checks} UI smoke checks passed.");
