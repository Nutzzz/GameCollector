﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace GameCollector.StoreHandlers.Xbox
{
    internal static class WindowsUtils
    {
        internal static IEnumerable<Package> GetUWPPackages()
        {
            var manager = new PackageManager();
            var user = WindowsIdentity.GetCurrent().User;
            if (user == null)
                return new List<Package>();

            //requires admin privileges if we don't supply the current user
            var packages = manager.FindPackagesForUser(user.Value)
                .Where(x => !x.IsFramework && !x.IsResourcePackage && x.SignatureKind == PackageSignatureKind.Store)
                .Where(x => x.InstalledLocation != null);
            return packages;
        }
    }
}
