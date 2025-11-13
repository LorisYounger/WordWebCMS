using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using WordWebCMS.Services;

namespace WordWebCMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddControllersWithViews();
            
            // Add session support
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Add memory cache for Application state replacement
            builder.Services.AddMemoryCache();
            
            // Add HTTP context accessor
            builder.Services.AddHttpContextAccessor();

            // Add custom services
            builder.Services.AddSingleton<ApplicationCache>();
            builder.Services.AddScoped<HttpContextService>();
            
            // Initialize database connections
            Conn.Initialize(builder.Configuration);
            
            // Initialize SMaster with content root path
            SMaster.Initialize(builder.Environment.ContentRootPath);

            var app = builder.Build();
            
            // Initialize Setting services
            var appCache = app.Services.GetRequiredService<ApplicationCache>();
            Setting.AppCache = appCache;

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // Serve static files from Picture directory
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(builder.Environment.ContentRootPath, "Picture")),
                RequestPath = "/Picture"
            });

            // Serve static files from Themes directory
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(builder.Environment.ContentRootPath, "Themes")),
                RequestPath = "/Themes"
            });

            app.UseRouting();

            app.UseAuthorization();
            app.UseSession();

            app.MapRazorPages();
            app.MapControllers();

            // Add rewrite rule similar to Web.config URL rewrite
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? "";
                if (!path.Contains(".") && !File.Exists(Path.Combine(app.Environment.ContentRootPath, path.TrimStart('/'))))
                {
                    context.Request.QueryString = context.Request.QueryString.Add("rootPath", path.TrimStart('/'));
                    context.Request.Path = "/Index";
                }
                await next();
            });

            app.Run();
        }
    }
}
