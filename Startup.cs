using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeeKoASPCore.Utility;

namespace TeeKoASPCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddSignalR(hubOptions =>
            {
                hubOptions.KeepAliveInterval = System.TimeSpan.FromMinutes(10);
                hubOptions.HandshakeTimeout = System.TimeSpan.FromMinutes(5);
            }
            );
            services.AddSingleton<IUserIdProvider, IdProvider>();
            services.AddSingleton<GameHubEventHandler>();

            services
               .AddAuthentication(options =>
               {
                   options.DefaultChallengeScheme = "CookieAuth";
                   options.DefaultSignInScheme = "CookieAuth";
                   options.DefaultAuthenticateScheme = "CookieAuth";
                   
               })
               .AddCookie("CookieAuth", options =>
               {
                   
                   options.SlidingExpiration = false;
                   options.LoginPath = "/login/login";
                    //options.AccessDeniedPath = new PathString("/Home/Forbidden/");
                   options.Cookie.Name = ".myapp.cookie";
                   
                   
                  
               });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.SubFolder)
               ;

            services.AddSingleton<GamesList>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStatusCodePages();

            var supportedCultures = new[]
           {
                new CultureInfo("en-US"),
                new CultureInfo("en-GB"),
                new CultureInfo("en"),
                new CultureInfo("ru-RU"),
                new CultureInfo("ru")
            };
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("en-US"),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();
            
            app.UseMvc(routes =>
            {
                

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                

            });

            
            app.UseSignalR(config => {
                config.MapHub<GameHub>("/game");
            });

           
        }
    }
}
