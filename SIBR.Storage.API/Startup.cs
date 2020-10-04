using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Serialization.SystemTextJson;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API
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
            Database.Init();
            services
                .AddSibrStorage();

            services.AddControllers().AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                
                // Can't specify this in an attribute, otherwise it'll recurse
                opts.JsonSerializerOptions.Converters.Add(new PageTokenJsonConverter());
            });
            services.AddApiVersioning();

            services.AddCors();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) 
                app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseCors(cors => cors.WithMethods("GET").AllowAnyOrigin().Build());
                
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}