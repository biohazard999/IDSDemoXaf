using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.ExpressApp.Security;
using DevExpress.Blazor.Reporting;
using DevExpress.ExpressApp.ReportsV2.Blazor;
using DevExpress.ExpressApp.Blazor.Services;
using DevExpress.Persistent.Base;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IDSDemo.Blazor.Server.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.Data.Filtering;
using IDSDemo.Module.BusinessObjects;
using System.Security.Claims;
using DevExpress.ExpressApp;
using System.Security.Principal;
using IDSDemo.Blazor.Server.Infrastructure;

namespace IDSDemo.Blazor.Server {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddHttpContextAccessor();
            services.AddSingleton<XpoDataStoreProviderAccessor>();
            services.AddScoped<CircuitHandler, CircuitHandlerProxy>();
            services.AddXaf<IDSDemoBlazorApplication>(Configuration);
            services.AddXafReporting();
            services.AddXafSecurity(options => {
                options.RoleType = typeof(DevExpress.Persistent.BaseImpl.PermissionPolicy.PermissionPolicyRole);
                options.UserType = typeof(DevExpress.Persistent.BaseImpl.PermissionPolicy.PermissionPolicyUser);
                options.Events.OnSecurityStrategyCreated = securityStrategy => ((SecurityStrategy)securityStrategy).RegisterXPOAdapterProviders();
                options.SupportNavigationPermissionsForTypes = false;
            }).AddExternalAuthentication<HttpContextPrincipalProvider>(options =>
            {
                options.Events.Authenticate = (objectSpace, externalUser) =>
                {
                    bool autoCreateUserByExternalProviderInfo = true;
                    return ProcessExternalLogin(objectSpace, externalUser, autoCreateUserByExternalProviderInfo);

                    PermissionPolicyUser ProcessExternalLogin(IObjectSpace os, IPrincipal _externalUser, bool autoCreateUser)
                    {
                        var userIdClaim = ((ClaimsPrincipal)_externalUser).FindFirst("sub") ??
                            ((ClaimsPrincipal)_externalUser).FindFirst(ClaimTypes.NameIdentifier) ??
                            throw new Exception("Unknown user id");

                        var providerUserId = userIdClaim.Value;
                        var userLoginInfo = os.FindObject<UserLoginInfo>(CriteriaOperator.And(
                                new BinaryOperator(nameof(UserLoginInfo.LoginProviderName), _externalUser.Identity.AuthenticationType),
                                new BinaryOperator(nameof(UserLoginInfo.ProviderUserKey), providerUserId)
                        ));

                        if (userLoginInfo != null)
                        {
                            return UpdateUser(os, userLoginInfo.User, _externalUser);
                        }
                        else
                        {
                            if (autoCreateUser)
                            {
                                var user = CreatePermissionPolicyUser(os, _externalUser);
                                if (user != null)
                                {
                                    user.CreateUserLoginInfo(os, _externalUser.Identity.AuthenticationType, providerUserId);
                                }
                                return user;
                            }
                        }
                        return null;
                    }

                    PermissionPolicyUser CreatePermissionPolicyUser(IObjectSpace os, IPrincipal _externalUser)
                    {
                        var user = os.CreateObject<PermissionPolicyUser>();
                        return UpdateUser(os, user, _externalUser);
                    }

                    PermissionPolicyUser UpdateUser(IObjectSpace os, PermissionPolicyUser user, IPrincipal _externalUser)
                    {
                        user.UserName = _externalUser.Identity.Name;

                        foreach (var role in user.Roles.ToList())
                        {
                            user.Roles.Remove(role);
                        }
                        if (_externalUser.Identity is ClaimsIdentity identity)
                        {
                            var roles = identity.Claims.Where(c => c.Type == identity.RoleClaimType || c.Type == ClaimTypes.Role).ToList();
                            foreach (var role in roles)
                            {
                                user.Roles.Add(os.FindObject<PermissionPolicyRole>(new BinaryOperator(nameof(PermissionPolicyRole.Name), role.Value)));
                            }
                        }

                        user.Roles.Add(os.FindObject<PermissionPolicyRole>(new BinaryOperator(nameof(PermissionPolicyRole.Name), "User")));
                        user.Roles.Add(os.FindObject<PermissionPolicyRole>(new BinaryOperator(nameof(PermissionPolicyRole.Name), "Default")));

                        os.CommitChanges();

                        return user;
                    }
                };
            })
            .AddAuthenticationStandard(options => {
                options.IsSupportChangePassword = true;
            });

            services.Configure<OpenIdConnectOptions>("Xenial",
               options => Configuration.Bind("Authentication:Xenial", options)
            );


            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => {
                options.LoginPath = "/LoginPage";
            }).AddOpenIdConnect("Xenial", "Xenial", options => { });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if(env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseXaf();
            app.UseDevExpressBlazorReporting();
            app.UseEndpoints(endpoints => {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapControllers();
            });
        }
    }
}
