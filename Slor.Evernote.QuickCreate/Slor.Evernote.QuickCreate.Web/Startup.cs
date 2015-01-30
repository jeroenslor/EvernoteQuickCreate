using Microsoft.AspNet.Builder;
using Microsoft.AspNet.StaticFiles;

namespace Slor.Evernote.QuickCreate.Web
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseErrorPage();

            app.UseDefaultFiles(new DefaultFilesOptions() { DefaultFileNames = new[] { "index.html" } });

            app.UseStaticFiles();            

            // that's all folkes since we just have a single html page for now :]
        }
    }
}
