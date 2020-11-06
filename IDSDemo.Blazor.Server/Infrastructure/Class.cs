using DevExpress.ExpressApp;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;

using IDSDemo.Module.BusinessObjects;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDSDemo.Blazor.Server.Infrastructure
{
    public static class PermissionPolicyUserExtensions
    {
        public static bool IsAuthenticationStandardEnabled(this PermissionPolicyUser user, IObjectSpace os)
        {
            return false;
        }

        public static void CreateUserLoginInfo(this PermissionPolicyUser user, IObjectSpace os, string providerName, string providerUserKey)
        {
            var userLoginInfo = os.CreateObject<UserLoginInfo>();
            userLoginInfo.ProviderUserKey = providerUserKey;
            userLoginInfo.LoginProviderName = providerName;
            userLoginInfo.User = user;
            os.CommitChanges();
        }
    }
}
