using Android.App;
using Android.OS;
using Gjallarhorn.XamarinForms;
using Program = ElmInspiredOne.Program;

namespace ElmInspiredXF.Android
{
    [Activity(Label = "Elm Inspired One", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsApplicationActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            global::Xamarin.Forms.Forms.Init(this, bundle);            

            var info = Framework.CreateApplicationInfo(Program.applicationCore, new MainPage());            
            LoadApplication(info.CreateApp());
        }
    }
}

