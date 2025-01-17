using System.ComponentModel;
using System.Net;
using GrayMint.Common.AspNetCore.Services;
using GrayMint.Common.JobController;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace GrayMint.Common.AspNetCore;

public static class GrayMintExtension
{
    public static void AddGrayMintCommonServices(this WebApplicationBuilder builder,
        IConfiguration appConfiguration,
        RegisterServicesOptions servicesOptions)
    {
        var services = builder.Services;

        // configure settings
        var appCommonSettings = appConfiguration.Get<GrayMintAppSettings>()
                                ?? throw new Exception("Could not load App section in appsettings.json file.");
        appCommonSettings.Validate();
        services.Configure<GrayMintAppSettings>(appConfiguration);

        // cors
        if (servicesOptions.AddCors)
            services.AddCors(o => o.AddPolicy(GrayMintApp.CorsPolicyName, corsPolicyBuilder =>
            {
                corsPolicyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetPreflightMaxAge(TimeSpan.FromHours(24 * 30));
            }));

        // Add services to the container.
        if (servicesOptions.AddControllers)
            services.AddControllers(mvcOptions =>
            {
                mvcOptions.ModelMetadataDetailsProviders.Add(
                    new SuppressChildValidationMetadataProvider(typeof(IPAddress)));
            });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        if (servicesOptions.AddSwagger)
        {
            services.AddEndpointsApiExplorer();
            services.AddGrayMintSwagger(appCommonSettings.AppName, servicesOptions.AddSwaggerVersioning);
        }

        if (servicesOptions.AddMemoryCache)
            services.AddMemoryCache();

        if (servicesOptions.AddHttpClient)
            services.AddHttpClient();

        services.AddHostedService<MaintenanceService>();
    }

    public static void UseGrayMintCommonServices(this WebApplication webApplication, UseServicesOptions options)
    {
        if (options.UseCors)
            webApplication.UseCors(GrayMintApp.CorsPolicyName);

        if (options.UseSwagger)
        {
            webApplication.UseOpenApi();
            webApplication.UseSwaggerUi3();
        }

        if (options.UseAuthentication)
            webApplication.UseAuthentication();

        if (options.UseAuthorization)
            webApplication.UseAuthorization();

        if (options.UseAppExceptions)
            webApplication.UseGrayMintExceptionHandler();

        if (options.MapControllers)
            webApplication.MapControllers();
    }

    public static void ScheduleGrayMintSqlMaintenance<TContext>(this WebApplication webApplication, TimeSpan interval) where TContext : DbContext
    {
        var maintenanceService = (MaintenanceService)webApplication.Services
            .GetRequiredService<IEnumerable<IHostedService>>()
            .Single(x=>x is MaintenanceService);

        maintenanceService.SqlMaintenanceJobs.Add(Tuple.Create(typeof(TContext), new JobSection(interval)));
    }


}