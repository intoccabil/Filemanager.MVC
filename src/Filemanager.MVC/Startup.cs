using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Filemanager.MVC.Startup))]
namespace Filemanager.MVC
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
