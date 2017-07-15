using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchard.Data.Migration;
using Orchard.Environment.Commands;
using Orchard.Environment.Navigation;
using Orchard.Environment.Shell;
using Orchard.Security;
using Orchard.Security.Permissions;
using Orchard.Security.Services;
using Orchard.Users.Commands;
using Orchard.Users.Indexes;
using Orchard.Users.Models;
using Orchard.Users.Services;
using YesSql.Indexes;
using Microsoft.AspNetCore.Authorization;

namespace Orchard.Users
{
    public class Startup : StartupBase
    {
        private const string LoginPath = "Login";

        private readonly string _tenantName;
        private readonly string _tenantPrefix;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public Startup(ShellSettings shellSettings, IDataProtectionProvider dataProtectionProvider)
        {
            _tenantName = shellSettings.Name;
            _tenantPrefix = "/" + shellSettings.RequestUrlPrefix;
            _dataProtectionProvider = dataProtectionProvider.CreateProtector(_tenantName);
        }

        public override void Configure(IApplicationBuilder builder, IRouteBuilder routes, IServiceProvider serviceProvider)
        {
            builder.UseAuthentication();

            var authenticationSchemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();

            authenticationSchemeProvider.AddScheme(new AuthenticationScheme(
                IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)));

            authenticationSchemeProvider.AddScheme(new AuthenticationScheme(
                IdentityConstants.ExternalScheme, null, typeof(CookieAuthenticationHandler)));

            var authorizationHandlerProvider = serviceProvider.GetService<IAuthorizationHandlerProvider>();

            routes.MapAreaRoute(
                name: "Login",
                areaName: "Orchard.Users",
                template: LoginPath,
                defaults: new { controller = "Account", action = "Login" }
            );
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSecurity();

            /// Adds the default token providers used to generate tokens for reset passwords, change email
            /// and change telephone number operations, and for two factor authentication token generation.

            new IdentityBuilder(typeof(User), typeof(Role), services).AddDefaultTokenProviders();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, o =>
            {
                o.Cookie.Name = "orchauth_" + _tenantName;
                o.Cookie.Path = _tenantPrefix;
                o.LoginPath = "/" + LoginPath;
                o.AccessDeniedPath = "/" + LoginPath;
                // Using a different DataProtectionProvider per tenant ensures cookie isolation between tenants
                o.DataProtectionProvider = _dataProtectionProvider;

                //o.LoginPath = new PathString("/Account/Login");
                o.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
                };
            })
            .AddCookie(IdentityConstants.ExternalScheme, o =>
            {
                o.Cookie.Name = "orchauth_" + _tenantName;
                o.Cookie.Path = _tenantPrefix;
                o.LoginPath = "/" + LoginPath;
                o.AccessDeniedPath = "/" + LoginPath;
                // Using a different DataProtectionProvider per tenant ensures cookie isolation between tenants
                o.DataProtectionProvider = _dataProtectionProvider;

                //o.Cookie.Name = IdentityConstants.ExternalScheme;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, o =>
            {
                o.Cookie.Name = "orchauth_" + _tenantName;
                o.Cookie.Path = _tenantPrefix;
                o.LoginPath = "/" + LoginPath;
                o.AccessDeniedPath = "/" + LoginPath;
                // Using a different DataProtectionProvider per tenant ensures cookie isolation between tenants
                o.DataProtectionProvider = _dataProtectionProvider;

                //o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme;
            })
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme, o =>
            {
                o.Cookie.Name = "orchauth_" + _tenantName;
                o.Cookie.Path = _tenantPrefix;
                o.LoginPath = "/" + LoginPath;
                o.AccessDeniedPath = "/" + LoginPath;
                // Using a different DataProtectionProvider per tenant ensures cookie isolation between tenants
                o.DataProtectionProvider = _dataProtectionProvider;

                //o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            // Identity services
            services.TryAddScoped<IUserValidator<User>, UserValidator<User>>();
            services.TryAddScoped<IPasswordValidator<User>, PasswordValidator<User>>();
            services.TryAddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();

            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<User>>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<User>, UserClaimsPrincipalFactory<User, Role>>();
            services.TryAddScoped<UserManager<User>>();
            services.TryAddScoped<SignInManager<User>>();

            services.TryAddScoped<IUserStore<User>, UserStore>();

            /*services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "orchauth_" + _tenantName;
                options.Cookie.Path = _tenantPrefix;
                options.LoginPath = "/" + LoginPath;
                options.AccessDeniedPath = "/" + LoginPath;
                // Using a different DataProtectionProvider per tenant ensures cookie isolation between tenants
                options.DataProtectionProvider = _dataProtectionProvider;
            })
            .ConfigureExternalCookie(options =>
            {
                options.Cookie.Name = "orchauth_" + _tenantName;
                options.Cookie.Path = _tenantPrefix;
                options.LoginPath = "/" + LoginPath;
                options.AccessDeniedPath = "/" + LoginPath;
                options.DataProtectionProvider = _dataProtectionProvider;
            })
            .Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
            {
                options.Cookie.Name = "orchauth_" + _tenantName;
                options.Cookie.Path = _tenantPrefix;
                options.LoginPath = "/" + LoginPath;
                options.AccessDeniedPath = "/" + LoginPath;
                options.DataProtectionProvider = _dataProtectionProvider;
            })
            .Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
            {
                options.Cookie.Name = "orchauth_" + _tenantName;
                options.Cookie.Path = _tenantPrefix;
                options.LoginPath = "/" + LoginPath;
                options.AccessDeniedPath = "/" + LoginPath;
                options.DataProtectionProvider = _dataProtectionProvider;
            });*/

            services.AddScoped<IIndexProvider, UserIndexProvider>();
            services.AddScoped<IDataMigration, Migrations>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IMembershipService, MembershipService>();
            services.AddScoped<SetupEventHandler>();
            services.AddScoped<ISetupEventHandler>(sp => sp.GetRequiredService<SetupEventHandler>());
            services.AddScoped<ICommandHandler, UserCommands>();

            services.AddScoped<IPermissionProvider, Permissions>();
            services.AddScoped<INavigationProvider, AdminMenu>();
        }
    }
}
