using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict;
using Orchard.Data.Migration;
using Orchard.DisplayManagement.Handlers;
using Orchard.Environment.Navigation;
using Orchard.Environment.Shell;
using Orchard.OpenId.Drivers;
using Orchard.OpenId.Indexes;
using Orchard.OpenId.Models;
using Orchard.OpenId.Recipes;
using Orchard.OpenId.Services;
using Orchard.OpenId.Settings;
using Orchard.Recipes;
using Orchard.Security.Permissions;
using Orchard.Settings;
using YesSql.Indexes;

namespace Orchard.OpenId
{
    public class Startup : StartupBase
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly ILogger<Startup> _logger;

        public Startup(
            ShellSettings shellSettings,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<Startup> logger)
        {
            _dataProtectionProvider = dataProtectionProvider.CreateProtector(shellSettings.Name);
            _logger = logger;
        }

        public override void Configure(IApplicationBuilder builder, IRouteBuilder routes, IServiceProvider serviceProvider)
        {
            var openIdService = serviceProvider.GetService<IOpenIdService>();
            var settings = openIdService.GetOpenIdSettingsAsync().GetAwaiter().GetResult();
            if (!openIdService.IsValidOpenIdSettings(settings))
            {
                _logger.LogWarning("The OpenID Connect module is not correctly configured.");
                return;
            }

            // Admin
            routes.MapAreaRoute(
                name: "AdminOpenId",
                areaName: "Orchard.OpenId",
                template: "Admin/OpenIdApps/{id?}/{action}",
                defaults: new { controller = "Admin" }
            );
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            // for testing, to be able to use 'OpenIdService'
            services.AddScoped<IOpenIdService, OpenIdService>();
            var serviceProvider = services.BuildServiceProvider();
            var openIdService = serviceProvider.GetService<IOpenIdService>();

            var settings = openIdService.GetOpenIdSettingsAsync().GetAwaiter().GetResult();
            var validOpenIdSettings = openIdService.IsValidOpenIdSettings(settings);
            (serviceProvider as IDisposable).Dispose();

            if (validOpenIdSettings)
            {
                var authenticationBuilder = services.AddAuthentication();

                switch (settings.AccessTokenFormat)
                {
                    case OpenIdSettings.TokenFormat.JWT:
                    {
                        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                        {
                            o.RequireHttpsMetadata = !settings.TestingModeEnabled;
                            o.Authority = settings.Authority;
                            o.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidAudiences = settings.Audiences
                            };
                        });

                        break;
                    }

                    case OpenIdSettings.TokenFormat.Encrypted:
                    {
                        authenticationBuilder.AddOAuthValidation(o =>
                        {
                            o.Audiences.UnionWith(settings.Audiences);
                            o.DataProtectionProvider = _dataProtectionProvider;
                        });

                        break;
                    }

                    default:
                    {
                        Debug.Fail("An unsupported access token format was specified.");
                        break;
                    }
                }
            }

            services.AddScoped<IDataMigration, Migrations>();
            services.AddScoped<IPermissionProvider, Permissions>();
            services.AddScoped<IIndexProvider, OpenIdApplicationIndexProvider>();
            services.AddScoped<IIndexProvider, OpenIdTokenIndexProvider>();
            services.AddScoped<INavigationProvider, AdminMenu>();

            services.AddScoped<IDisplayDriver<ISite>, OpenIdSiteSettingsDisplayDriver>();
            //services.AddScoped<IOpenIdService, OpenIdService>();
            services.AddRecipeExecutionStep<OpenIdSettingsStep>();
            services.AddRecipeExecutionStep<OpenIdApplicationStep>();

            services.AddScoped<OpenIdApplicationIndexProvider>();
            services.AddScoped<OpenIdTokenIndexProvider>();

            services.AddScoped<OpenIdApplicationStore>();

            if (validOpenIdSettings)
            {
                services.AddOpenIddict<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken>(builder =>
                {
                    builder.AddApplicationStore<OpenIdApplicationStore>()
                           .AddTokenStore<OpenIdTokenStore>();

                    builder.UseDataProtectionProvider(_dataProtectionProvider);

                    builder.RequireClientIdentification()
                           .EnableRequestCaching();

                    builder.Configure(o =>
                    {
                        // for testing, to apply settings before configure
                        OpenIdConfiguration.ConfigureOptions(o, settings);
                        o.ApplicationCanDisplayErrors = true;
                    });
                });
            }

            services.AddScoped<IConfigureOptions<OpenIddictOptions>, OpenIdConfiguration>();
        }
    }
}
