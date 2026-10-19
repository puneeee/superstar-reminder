using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MySqlConnector;
using SuperstarReminder.Domain;
using SuperstarReminder.Infrastructure;
using SuperstarReminder.Services;
namespace SuperstarReminder;
public sealed record SettingsResult(string ConnectionString,LocalSettings Settings);
public sealed class SettingsWindow : Window
{
    public SettingsWindow(string? connection,LocalSettings current,ICharacterCatalog catalog)
    {
        Title="Settings & MySQL setup";Width=540;Height=780;MinHeight=450;Background=Brush.Parse("#10131B");WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Spacing=10,Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text="Settings",FontSize=26,FontWeight=FontWeight.SemiBold});
        panel.Children.Add(new TextBlock{Text="Connect to your local MySQL database. The app keeps running when you close the dashboard.",TextWrapping=TextWrapping.Wrap});
        MySqlConnectionStringBuilder b;try{b=new(connection??"");}catch(ArgumentException){b=new();}
        var server=new TextBox{Text=string.IsNullOrWhiteSpace(b.Server)?"127.0.0.1":b.Server};var port=new NumericUpDown{Minimum=1,Maximum=65535,Value=b.Port};
        var database=new TextBox{Text=string.IsNullOrWhiteSpace(b.Database)?"superstar_reminder":b.Database};var user=new TextBox{Text=string.IsNullOrWhiteSpace(b.UserID)?"superstar_app":b.UserID};var password=new TextBox{Text=b.Password,PasswordChar='●'};
        void Add(string label,Control c){panel.Children.Add(new TextBlock{Text=label,Foreground=Brush.Parse("#AAB5CC"),FontSize=12});panel.Children.Add(c);}
        Add("MySQL server",server);Add("Port",port);Add("Database",database);Add("Application user",user);Add("Password (protected with your OS user account)",password);
        var corner=new ComboBox{ItemsSource=Enum.GetValues<ScreenCorner>(),SelectedItem=current.Corner};Add("Primary monitor corner",corner);
        var size=new Slider{Minimum=.18,Maximum=.40,Value=current.HeightFraction,TickFrequency=.01};Add("Character height · 18–40% of the usable screen",size);
        var characters=catalog.All.ToList();var picker=new ComboBox{ItemsSource=characters.Select(x=>x.DisplayName).ToList(),SelectedIndex=Math.Max(0,characters.FindIndex(x=>x.Id==current.CharacterId))};Add("Character",picker);
        picker.SelectionChanged+=(_,_)=>{if(picker.SelectedIndex>=0)size.Value=characters[picker.SelectedIndex].DefaultHeightFraction;};
        var startup=new CheckBox{Content="Start at login",IsChecked=current.StartAtLogin};panel.Children.Add(startup);
        var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Salmon};
        var save=new Button{Content="Test connection & save",Background=Brush.Parse("#E6B544"),Foreground=Brushes.Black};
        save.Click+=async(_,_)=>
        {
            save.IsEnabled=false;error.Text="Connecting…";
            try
            {
                var builder=new MySqlConnectionStringBuilder{Server=server.Text?.Trim(),Port=(uint)(port.Value??3306),Database=database.Text?.Trim(),UserID=user.Text?.Trim(),Password=password.Text,ConnectionTimeout=5,DefaultCommandTimeout=10,DateTimeKind=MySqlDateTimeKind.Utc};
                if(builder.Server is not ("127.0.0.1" or "localhost" or "::1"))throw new ArgumentException("V1 connects to a local MySQL server. Use 127.0.0.1 or localhost.");
                if(string.IsNullOrWhiteSpace(builder.Database)||string.IsNullOrWhiteSpace(builder.UserID))throw new ArgumentException("Enter a database and application user.");
                if(builder.UserID.Equals("root",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Use a dedicated MySQL application user instead of root.");
                await using var db=new MySqlConnection(builder.ConnectionString);await db.OpenAsync();
                var updated=new LocalSettings{Corner=(ScreenCorner)(corner.SelectedItem??ScreenCorner.BottomRight),HeightFraction=size.Value,CharacterId=characters.Count>0?characters[Math.Max(0,picker.SelectedIndex)].Id:current.CharacterId,StartAtLogin=startup.IsChecked==true,Paused=current.Paused};
                LoginStartup.Set(updated.StartAtLogin);SecretStore.Save(builder.ConnectionString);updated.Save();
                Close(new SettingsResult(builder.ConnectionString,updated));
            }
            catch(MySqlException ex){error.Text=$"MySQL connection failed (code {ex.Number}). Check the service, database, username, and password.";}
            catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException){error.Text=ex.Message;}
            finally{save.IsEnabled=true;}
        };
        var cancel=new Button{Content="Cancel"};cancel.Click+=(_,_)=>Close();var footer=new StackPanel{Margin=new Thickness(24,8,24,20),Spacing=8,Children={error,new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=8,Children={save,cancel}}}};
        var root=new Grid{RowDefinitions=new RowDefinitions("*,Auto")};root.Children.Add(new ScrollViewer{Content=panel});Grid.SetRow(footer,1);root.Children.Add(footer);Content=root;
    }
}
