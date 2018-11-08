using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using KnotDiary.Common;
using KnotDiary.Common.Messaging;
using KnotDiary.Common.Web.Extensions;
using KnotDiary.Common.Web.Infrastructure;
using KnotDiary.Common.Web.Infrastructure.Logging;
using KnotDiary.Services;
using KnotDiary.UsersApi.Data;
using KnotDiary.UsersApi.Services;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Splunk;
using ServiceStack.Redis;
using System;
using System.Net;

namespace KnotDiary.UsersApi
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        const string ServiceName = "KnotDiary Users API";

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;

            var splunkToken = Configuration.GetValue<string>("Logging:SplunkToken");
            var splunkUrl = Configuration.GetValue<string>("Logging:SplunkCollectorUrl");
            var splunkFormatter = new CompactSplunkJsonFormatter(true, environment.EnvironmentName, "api_log", Environment.MachineName);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
                .WriteTo.EventCollector(splunkUrl, splunkToken, splunkFormatter)
                .WriteTo.Console()
                .CreateLogger();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<StorageConfiguration>(Configuration);

            services.AddSingleton(Log.Logger);
            services.AddSingleton<IConfigurationHelper, ConfigurationHelperJson>();
            services.AddSingleton<ICacheManager, RedisCacheManager>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<IRedisClientsManager>(c => new BasicRedisClientManager(Configuration["Cache:Redis:ConnectionString"]));

            services.AddSingleton<ITopicConnection, TopicConnection>();
            services.AddSingleton<ITopicConsumer, TopicConsumer>();
            services.AddSingleton<ITopicProducer, TopicProducer>();

            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IStorageService, StorageService>();

            services.AddMvc(options => options.Filters.Add(typeof(ExceptionFilter)))
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    opt.SerializerSettings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
                    opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                });

            services.AddSwaggerDocumentation(ServiceName);

            // cors setup
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                    );
            });

            // Add framework services.
            services.AddMvc().AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            app.UseMiddleware<HeaderLogger>();
            app.UseMiddleware<RequestLogger>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // cors config
            app.UseCors(c =>
            {
                c.AllowAnyHeader();
                c.AllowAnyMethod();
                c.AllowAnyOrigin();
                c.AllowCredentials();
            });

            // swagger configuration
            app.UseSwaggerDocumentation(ServiceName);

            // global exception handler
            app.UseExceptionHandler(options =>
            {
                options.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    var ex = context.Features.Get<IExceptionHandlerFeature>();
                    if (ex != null)
                    {
                        await context.Response.WriteAsync(ex.Error.Message).ConfigureAwait(false);
                    }
                });
            });

            // identity server config
            app.UseMvc();
        }
    }
}
