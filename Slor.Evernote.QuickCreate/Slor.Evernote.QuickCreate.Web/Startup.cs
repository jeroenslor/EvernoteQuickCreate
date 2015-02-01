using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Routing;
using Microsoft.AspNet.StaticFiles;
using Microsoft.Framework.DependencyInjection;
using Microsoft.AspNet.Http;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.AspNet.Routing.Template;
using System.IO;
using Microsoft.AspNet.WebUtilities;
using System.Text;
using EvernoteSDK;
using System.Security.Claims;
using Microsoft.AspNet.Http.Security;
using Newtonsoft.Json;

namespace Slor.Evernote.QuickCreate.Web
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDataProtection();
            services.AddRoutingFix();
        }

        public void Configure(IApplicationBuilder app)
        {        
            app.UseErrorPage().UseStaticFiles();

            app.UseDefaultFiles(new DefaultFilesOptions() { DefaultFileNames = new[] { "index.html" } });

            app.UseCookieAuthentication(options =>
            {                
                options.LoginPath = new PathString("/");    
            });

            var routeBuilder = new RouteBuilder() { ServiceProvider = app.ApplicationServices };
            routeBuilder.MapSimpleRoute("signin", "signing", null, null, new { httpMethod = new HttpMethodContraint("post") }, async context =>
              {
                  var data = await GetSigninData(context.HttpContext.Response.Body);

                  ENSession.SetSharedSessionDeveloperToken(data.Item1, data.Item2); // TODO doe OAuth flow

                  var isAuthenticated = ENSession.SharedSession.IsAuthenticated;
                  if (isAuthenticated)
                  {
                      context.HttpContext.Response.SignIn(new AuthenticationProperties() { IsPersistent = true }, new ClaimsIdentity(new[] { new Claim("name", ENSession.SharedSession.UserDisplayName) }));
                  }                  

                  context.HttpContext.Response.ContentType = "application/json";
                  await context.HttpContext.Response.WriteAsync(await JsonConvert.SerializeObjectAsync(new { success = ENSession.SharedSession.IsAuthenticated, name = ENSession.SharedSession.UserDisplayName }));
              });            

            // Deny anonymous request beyond this point
            app.Use(async (context, next) => {
                if (!context.User.Identity.IsAuthenticated)
                {
                    await context.Response.WriteAsync("You need to be authenticated");
                    //context.Response.Challenge();
                    return;
                }
                await next();
            });

            app.Map("/foo", fooApp =>
            {
                fooApp.Run(async context => await context.Response.WriteAsync("Only when authed"));
            });            

            // that's all folkes since we just have a single html page for now :]
        }        

        public static async Task<Tuple<string, string>> GetSigninData(Stream responseBody)
        {
            string result = null;
            using (var readStream = responseBody)
            {
                using (var reader = new StreamReader(readStream, Encoding.UTF8))
                {
                    await reader.ReadToEndAsync();
                }
            }

            var form = FormHelpers.ParseForm(result);
            var token = form["token"] as string;
            var storeUrl = form["url"] as string;

            return new Tuple<string, string>(token, storeUrl);
        }

        public static void InitCustomRoute(IApplicationBuilder app)
        {
            var route = new RouteBuilder()
            {
                DefaultHandler = null,
                ServiceProvider = app.ApplicationServices
            };

            route.MapSimpleRoute("notes", "api/user/{userId}/note/{noteId}", async context =>
            {
                await context.HttpContext.Response.WriteAsync(context.RouteData.Values["userId"] as string);
            });            

            app.UseRouter(route.Build());
        }

    }

    public class HttpMethodContraint : IRouteConstraint
    {
        public string Method { get; set; }

        public HttpMethodContraint(string method)
        {
            Method = method;
        }

        public bool Match(HttpContext httpContext, IRouter route, string routeKey, IDictionary<string, object> values, RouteDirection routeDirection)
        {            
            return httpContext.Request.Method.Equals(Method, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class DelegatedRouter : IRouter
    {
        public Func<RouteContext, Task> RouteFunc { get; set; }

        public DelegatedRouter(Func<RouteContext, Task> routeFunc)
        {
            RouteFunc = routeFunc;
        }

        public string GetVirtualPath(VirtualPathContext context)
        {
            context.IsBound = true;
            return null;
        }

        public async Task RouteAsync(RouteContext context)
        {
            await RouteFunc(context);
            context.IsHandled = true;
        }
    }

    public static class DictionaryExtensions
    {
        public static string Print(this IDictionary<string, object> routeValues)
        {
            var values = routeValues.Select(kvp => kvp.Key + ":" + kvp.Value.ToString());

            return string.Join(" ", values);
        }
    }

    public static class RoutingServices
    {
        public static IServiceCollection AddRoutingFix(this IServiceCollection services, IConfiguration config = null, Action<RouteOptions> configureOptions = null)
        {
            var describe = new ServiceDescriber(config);

            services.AddOptions(config);
            services.TryAdd(describe.Transient<IInlineConstraintResolver, DefaultInlineConstraintResolver>());

            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            return services;
        }
    }

    public static class RouteBuilderExtensions
    {
        public static IRouteBuilder MapSimpleRoute(this IRouteBuilder routeCollectionBuilder, string name, string template, Func<RouteContext, Task> routeFunc)
        {
            return MapSimpleRoute(routeCollectionBuilder, name, template, null, null, null, routeFunc);
        }
                
        public static IRouteBuilder MapSimpleRoute(this IRouteBuilder routeCollectionBuilder,
                                             string name,
                                             string template,
                                             object defaults,
                                             object constraints,
                                             object dataTokens, Func<RouteContext, Task> routeFunc)
        {
            var inlineConstraintResolver = routeCollectionBuilder
                                                        .ServiceProvider
                                                        .GetRequiredService<IInlineConstraintResolver>();
            routeCollectionBuilder.Routes.Add(new TemplateRoute(new DelegatedRouter(routeFunc),
                                                                name,
                                                                template,
                                                                ObjectToDictionary(defaults),
                                                                ObjectToDictionary(constraints),
                                                                ObjectToDictionary(dataTokens),
                                                                inlineConstraintResolver));

            return routeCollectionBuilder;
        }

        private static IDictionary<string, object> ObjectToDictionary(object value)
        {
            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                return dictionary;
            }

            return new RouteValueDictionary(value);
        }
    }
}
