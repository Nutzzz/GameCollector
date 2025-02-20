// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;
using WinRT;

namespace GameCollector.PkgHandlers.Winget.WindowsPackageManager;

public class WindowsPackageManagerStandardFactory : WindowsPackageManagerFactory
{
    public WindowsPackageManagerStandardFactory(ClsidContext clsidContext = ClsidContext.Prod, bool allowLowerTrustRegistration = false)
        : base(clsidContext, allowLowerTrustRegistration)
    {
    }

    protected override T CreateInstance<T>(Guid clsid, Guid iid)
    {
        var pUnknown = IntPtr.Zero;
        try
        {
            var clsctx = CLSCTX.CLSCTX_LOCAL_SERVER; //CLSCTX.CLSCTX_ALL;
            if (_allowLowerTrustRegistration)
            {
                clsctx |= CLSCTX.CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION;
            }

            var hr = PInvoke.CoCreateInstance(clsid, pUnkOuter: null, clsctx, iid, out var result);

            //                     !! WARNING !!
            // An exception may be thrown on the line below if this program
            // runs as administrator and AllowLowerTrustRegistration settings is not checked
            // or when WinGet is not installed on the system.
            // It can be safely ignored if any of the conditions
            // above are met.
            Marshal.ThrowExceptionForHR(hr);

            pUnknown = Marshal.GetIUnknownForObject(result);
            return MarshalGeneric<T>.FromAbi(pUnknown);
        }
        finally
        {
            // CoCreateInstance and FromAbi both AddRef on the native object.
            // Release once to prevent memory leak.
            if (pUnknown != IntPtr.Zero)
            {
                Marshal.Release(pUnknown);
            }
        }
    }
}
