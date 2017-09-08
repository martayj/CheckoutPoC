using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(CheckoutPoC.Startup))]
namespace CheckoutPoC
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
