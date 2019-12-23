namespace K2Bridge
{
    using System;
    using K2Bridge.KustoConnector;
    using K2Bridge.Models;
    using K2Bridge.RewriteRules;
    using K2Bridge.Visitors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Rewrite;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.OpenApi.Models;
    using Serilog;

    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddScoped<KustoConnectionDetails>(s => KustoConnectionDetails.MakeFromConfiguration(this.Configuration as IConfigurationRoot));
            services.AddSingleton<ListenerDetails>(s => ListenerDetails.MakeFromConfiguration(this.Configuration as IConfigurationRoot));
            services.AddTransient<ITranslator, QueryTranslator>();
            services.AddTransient<IQueryExecutor, KustoManager>();
            services.AddTransient<IVisitor, ElasticSearchDSLVisitor>();

            // use this http client factory to issue requests to the fallback elastic instance
            services.AddHttpClient("elasticFallback", (svcProvider, elasticClient) =>
            {
                var listenerDetails = svcProvider.GetRequiredService<ListenerDetails>();
                elasticClient.BaseAddress = new Uri(listenerDetails.MetadataEndpoint);
            });

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo() { Title = "K2Bridge API", Version = "v1" });
            });

            // required on ASP.NET Core 3 https://docs.microsoft.com/en-us/aspnet/core/migration/22-to-30?view=aspnetcore-2.2&tabs=visual-studio#jsonnet-support
            services.AddMvcCore().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.IsEnvironment("local"))
            {
                app.UseDeveloperExceptionPage();
            }

            // detailed request logging
            app.UseSerilogRequestLogging();

            // rewrite URL in case there is a dot ('.') in the path (and add a trailing slash)
            // without the trailing slash ASP.NET interprets this is a file request and
            // blocks the request. Needed for the Kibana passtrough requests
            var options = new RewriteOptions()
                .Add(new RewriteRequestsMissingTrailingSlashesRule());
            app.UseRewriter(options);
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToController("Passthrough", "Metadata");
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "K2Bridge API v0.1-alpha");
            });
        }
    }
}
