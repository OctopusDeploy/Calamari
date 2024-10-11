using System;
using System.Collections.Generic;
using Calamari.Integration.Certificates.WindowsNative;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using static Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;


namespace Calamari.Integration.Certificates
{
    #if !NETFX
       [Flags]
    public enum CryptoKeyRights
    {
        ReadData = 1,
        WriteData = 2,
        ReadExtendedAttributes = 8,
        WriteExtendedAttributes = 16, // 0x00000010
        ReadAttributes = 128, // 0x00000080
        WriteAttributes = 256, // 0x00000100
        Delete = 65536, // 0x00010000
        ReadPermissions = 131072, // 0x00020000
        ChangePermissions = 262144, // 0x00040000
        TakeOwnership = 524288, // 0x00080000
        Synchronize = 1048576, // 0x00100000
        FullControl = Synchronize | TakeOwnership | ChangePermissions | ReadPermissions | Delete | WriteAttributes | ReadAttributes | WriteExtendedAttributes | ReadExtendedAttributes | WriteData | ReadData, // 0x001F019B
        GenericAll = 268435456, // 0x10000000
        GenericExecute = 536870912, // 0x20000000
        GenericWrite = 1073741824, // 0x40000000
        GenericRead = -2147483648, // 0x80000000
    }
    
    public sealed class CryptoKeyAccessRule : AccessRule
    {
        public CryptoKeyAccessRule(
            IdentityReference identity,
            CryptoKeyRights cryptoKeyRights,
            AccessControlType type)
            : this(identity, CryptoKeyAccessRule.AccessMaskFromRights(cryptoKeyRights, type), false, InheritanceFlags.None, PropagationFlags.None, type)
        {
        }

        public CryptoKeyAccessRule(
            string identity,
            CryptoKeyRights cryptoKeyRights,
            AccessControlType type)
            : this((IdentityReference) new NTAccount(identity), CryptoKeyAccessRule.AccessMaskFromRights(cryptoKeyRights, type), false, InheritanceFlags.None, PropagationFlags.None, type)
        {
        }

        private CryptoKeyAccessRule(
            IdentityReference identity,
            int accessMask,
            bool isInherited,
            InheritanceFlags inheritanceFlags,
            PropagationFlags propagationFlags,
            AccessControlType type)
            : base(identity, accessMask, isInherited, inheritanceFlags, propagationFlags, type)
        {
        }

        public CryptoKeyRights CryptoKeyRights
        {
            get => CryptoKeyAccessRule.RightsFromAccessMask(this.AccessMask);
        }

        private static int AccessMaskFromRights(
            CryptoKeyRights cryptoKeyRights,
            AccessControlType controlType)
        {
            switch (controlType)
            {
                case AccessControlType.Allow:
                    cryptoKeyRights |= CryptoKeyRights.Synchronize;
                    break;
                case AccessControlType.Deny:
                    if (cryptoKeyRights != CryptoKeyRights.FullControl)
                    {
                        cryptoKeyRights &= ~CryptoKeyRights.Synchronize;
                        break;
                    }
                    break;
                default:
                    throw new ArgumentException("Argument_InvalidEnumValue");
            }
            return (int) cryptoKeyRights;
        }

        internal static CryptoKeyRights RightsFromAccessMask(int accessMask)
        {
            return (CryptoKeyRights) accessMask;
        }
    }
    
    public sealed class CryptoKeySecurity : NativeObjectSecurity
  {
    private const ResourceType s_ResourceType = ResourceType.FileObject;

    public CryptoKeySecurity()
      : base(false, ResourceType.FileObject)
    {
    }

 
    public override sealed AccessRule AccessRuleFactory(
      IdentityReference identityReference,
      int accessMask,
      bool isInherited,
      InheritanceFlags inheritanceFlags,
      PropagationFlags propagationFlags,
      AccessControlType type)
    {
      return (AccessRule) new CryptoKeyAccessRule(identityReference, CryptoKeyAccessRule.RightsFromAccessMask(accessMask), type);
    }

    public override sealed AuditRule AuditRuleFactory(
      IdentityReference identityReference,
      int accessMask,
      bool isInherited,
      InheritanceFlags inheritanceFlags,
      PropagationFlags propagationFlags,
      AuditFlags flags)
    {
      return (AuditRule) new CryptoKeyAuditRule(identityReference, CryptoKeyAuditRule.RightsFromAccessMask(accessMask), flags);
    }

    public void AddAccessRule(CryptoKeyAccessRule rule) => this.AddAccessRule((AccessRule) rule);

    public void SetAccessRule(CryptoKeyAccessRule rule) => this.SetAccessRule((AccessRule) rule);

    public void ResetAccessRule(CryptoKeyAccessRule rule)
    {
      this.ResetAccessRule((AccessRule) rule);
    }

    public bool RemoveAccessRule(CryptoKeyAccessRule rule)
    {
      return this.RemoveAccessRule((AccessRule) rule);
    }

    public void RemoveAccessRuleAll(CryptoKeyAccessRule rule)
    {
      this.RemoveAccessRuleAll((AccessRule) rule);
    }

    public void RemoveAccessRuleSpecific(CryptoKeyAccessRule rule)
    {
      this.RemoveAccessRuleSpecific((AccessRule) rule);
    }

    public void AddAuditRule(CryptoKeyAuditRule rule) => this.AddAuditRule((AuditRule) rule);

    public void SetAuditRule(CryptoKeyAuditRule rule) => this.SetAuditRule((AuditRule) rule);

    public bool RemoveAuditRule(CryptoKeyAuditRule rule) => this.RemoveAuditRule((AuditRule) rule);

    public void RemoveAuditRuleAll(CryptoKeyAuditRule rule)
    {
      this.RemoveAuditRuleAll((AuditRule) rule);
    }

    public void RemoveAuditRuleSpecific(CryptoKeyAuditRule rule)
    {
      this.RemoveAuditRuleSpecific((AuditRule) rule);
    }

    public override Type AccessRightType => typeof (CryptoKeyRights);

    public override Type AccessRuleType => typeof (CryptoKeyAccessRule);

    public override Type AuditRuleType => typeof (CryptoKeyAuditRule);

    internal AccessControlSections ChangedAccessControlSections
    {
      [SecurityCritical] get
      {
        AccessControlSections accessControlSections = AccessControlSections.None;
        bool flag = false;
        RuntimeHelpers.PrepareConstrainedRegions();
        try
        {
          RuntimeHelpers.PrepareConstrainedRegions();
          try
          {
          }
          finally
          {
            this.ReadLock();
            flag = true;
          }
          if (this.AccessRulesModified)
            accessControlSections |= AccessControlSections.Access;
          if (this.AuditRulesModified)
            accessControlSections |= AccessControlSections.Audit;
          if (this.GroupModified)
            accessControlSections |= AccessControlSections.Group;
          if (this.OwnerModified)
            accessControlSections |= AccessControlSections.Owner;
        }
        finally
        {
          if (flag)
            this.ReadUnlock();
        }
        return accessControlSections;
      }
    }
  }
    
    public sealed class CryptoKeyAuditRule : AuditRule
    {
        public CryptoKeyAuditRule(
            IdentityReference identity,
            CryptoKeyRights cryptoKeyRights,
            AuditFlags flags)
            : this(identity, CryptoKeyAuditRule.AccessMaskFromRights(cryptoKeyRights), false, InheritanceFlags.None, PropagationFlags.None, flags)
        {
        }

        public CryptoKeyAuditRule(string identity, CryptoKeyRights cryptoKeyRights, AuditFlags flags)
            : this((IdentityReference) new NTAccount(identity), CryptoKeyAuditRule.AccessMaskFromRights(cryptoKeyRights), false, InheritanceFlags.None, PropagationFlags.None, flags)
        {
        }

        private CryptoKeyAuditRule(
            IdentityReference identity,
            int accessMask,
            bool isInherited,
            InheritanceFlags inheritanceFlags,
            PropagationFlags propagationFlags,
            AuditFlags flags)
            : base(identity, accessMask, isInherited, inheritanceFlags, propagationFlags, flags)
        {
        }

        public CryptoKeyRights CryptoKeyRights
        {
            get => CryptoKeyAuditRule.RightsFromAccessMask(this.AccessMask);
        }

        private static int AccessMaskFromRights(CryptoKeyRights cryptoKeyRights)
        {
            return (int) cryptoKeyRights;
        }

        internal static CryptoKeyRights RightsFromAccessMask(int accessMask)
        {
            return (CryptoKeyRights) accessMask;
        }
    }
#endif
    public static class CryptoKeySecurityAccessRules
    {

        internal static void AddPrivateKeyAccessRules(ICollection<PrivateKeyAccessRule> accessRules, SafeCertContextHandle certificate)
        {
            try
            {
                var keyProvInfo = certificate.GetCertificateProperty<KeyProviderInfo>(CertificateProperty.KeyProviderInfo);

                // If it is a CNG key
                if (keyProvInfo.dwProvType == 0)
                {
                    SetCngPrivateKeySecurity(certificate, accessRules);
                }
                else
                {
                    SetCspPrivateKeySecurity(certificate, accessRules);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not set security on private-key", ex);
            }
        }

        static void SetCngPrivateKeySecurity(SafeCertContextHandle certificate, ICollection<PrivateKeyAccessRule> accessRules)
        {
            using (var key = CertificatePal.GetCngPrivateKey(certificate))
            {
                var security = GetCngPrivateKeySecurity(certificate);

                foreach (var cryptoKeyAccessRule in accessRules.Select(ToCryptoKeyAccessRule))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();
                var gcHandle = GCHandle.Alloc(securityDescriptorBytes, GCHandleType.Pinned);

                var errorCode = NCryptSetProperty(key,
                                                  WindowsX509Native.NCryptProperties.SecurityDescriptor,
                                                  gcHandle.AddrOfPinnedObject(),
                                                  securityDescriptorBytes.Length,
                                                  (int)WindowsX509Native.NCryptFlags.Silent | (int)WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION);

                gcHandle.Free();

                if (errorCode != 0)
                {
                    throw new CryptographicException(errorCode);
                }
            }
        }

        static void SetCspPrivateKeySecurity(SafeCertContextHandle certificate, ICollection<PrivateKeyAccessRule> accessRules)
        {
            using (var cspHandle = CertificatePal.GetCspPrivateKey(certificate))
            {
                var security = GetCspPrivateKeySecurity(certificate);

                foreach (var cryptoKeyAccessRule in accessRules.Select(ToCryptoKeyAccessRule))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();

                if (!CryptSetProvParam(cspHandle,
                                       WindowsX509Native.CspProperties.SecurityDescriptor,
                                       securityDescriptorBytes,
                                       WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
            }
        }

        static CryptoKeyAccessRule ToCryptoKeyAccessRule(PrivateKeyAccessRule privateKeyAccessRule)
        {
            switch (privateKeyAccessRule.Access)
            {
                case PrivateKeyAccess.ReadOnly:
                    return new CryptoKeyAccessRule(privateKeyAccessRule.GetIdentityReference(), CryptoKeyRights.GenericRead, AccessControlType.Allow);

                case PrivateKeyAccess.FullControl:
                    // We use 'GenericAll' here rather than 'FullControl' as 'FullControl' doesn't correctly set the access for CNG keys
                    return new CryptoKeyAccessRule(privateKeyAccessRule.GetIdentityReference(), CryptoKeyRights.GenericAll, AccessControlType.Allow);

                default:
                    throw new ArgumentOutOfRangeException(nameof(privateKeyAccessRule.Access));
            }
        }

        static CryptoKeySecurity GetCngPrivateKeySecurity(SafeCertContextHandle certificate)
        {
            using (var key = CertificatePal.GetCngPrivateKey(certificate))
            {
                var security = new CryptoKeySecurity();
                security.SetSecurityDescriptorBinaryForm(CertificatePal.GetCngPrivateKeySecurity(key),
                                                         AccessControlSections.Access);
                return security;
            }
        }

        static CryptoKeySecurity GetCspPrivateKeySecurity(SafeCertContextHandle certificate)
        {
            using (var cspHandle = CertificatePal.GetCspPrivateKey(certificate))
            {
                var security = new CryptoKeySecurity();
                security.SetSecurityDescriptorBinaryForm(CertificatePal.GetCspPrivateKeySecurity(cspHandle), AccessControlSections.Access);
                return security;
            }
        }


        public static CryptoKeySecurity GetPrivateKeySecurity(string thumbprint, StoreLocation storeLocation, string storeName)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            store.Close();

            if (found.Count == 0)
                throw new Exception(
                                    $"Could not find certificate with thumbprint '{thumbprint}' in store Cert:\\{storeLocation}\\{storeName}");

            var certificate = new SafeCertContextHandle(found[0].Handle, false);

            if (!certificate.HasPrivateKey())
                throw new Exception("Certificate does not have a private-key");

            var keyProvInfo =
                certificate.GetCertificateProperty<WindowsX509Native.KeyProviderInfo>(WindowsX509Native.CertificateProperty.KeyProviderInfo);

            // If it is a CNG key
            return keyProvInfo.dwProvType == 0
                ? GetCngPrivateKeySecurity(certificate)
                : GetCspPrivateKeySecurity(certificate);
        }
    }
}