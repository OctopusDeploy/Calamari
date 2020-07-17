// Required to allow a service to run a process as another user
// See http://stackoverflow.com/questions/677874/starting-a-process-with-credentials-from-a-windows-service/30687230#30687230

using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Calamari.Common.Features.Processes
{
    public class WindowStationAndDesktopAccess
    {
        public static void GrantAccessToWindowStationAndDesktop(string username, string? domainName = null)
        {
            const int windowStationAllAccess = 0x000f037f;
            GrantAccess(username, domainName, GetProcessWindowStation(), windowStationAllAccess);
            const int desktopRightsAllAccess = 0x000f01ff;
            GrantAccess(username, domainName, GetThreadDesktop(GetCurrentThreadId()), desktopRightsAllAccess);
        }

        static void GrantAccess(string username, string? domainName, IntPtr handle, int accessMask)
        {
            SafeHandle safeHandle = new NoopSafeHandle(handle);
            var security =
                new GenericSecurity(
                    false,
                    ResourceType.WindowObject,
                    safeHandle,
                    AccessControlSections.Access);

            var account = string.IsNullOrEmpty(domainName)
                ? new NTAccount(username)
                : new NTAccount(domainName, username);

            security.AddAccessRule(
                new GenericAccessRule(
                    account,
                    accessMask,
                    AccessControlType.Allow));
            security.Persist(safeHandle, AccessControlSections.Access);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetThreadDesktop(int dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetCurrentThreadId();

        // All the code to manipulate a security object is available in .NET framework,
        // but its API tries to be type-safe and handle-safe, enforcing a special implementation
        // (to an otherwise generic WinAPI) for each handle type. This is to make sure
        // only a correct set of permissions can be set for corresponding object types and
        // mainly that handles do not leak.
        // Hence the AccessRule and the NativeObjectSecurity classes are abstract.
        // This is the simplest possible implementation that yet allows us to make use
        // of the existing .NET implementation, sparing necessity to
        // P/Invoke the underlying WinAPI.

        class GenericAccessRule : AccessRule
        {
            public GenericAccessRule(
                IdentityReference identity,
                int accessMask,
                AccessControlType type)
                :
                base(identity,
                    accessMask,
                    false,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    type)
            {
            }
        }

        class GenericSecurity : NativeObjectSecurity
        {
            public GenericSecurity(
                bool isContainer,
                ResourceType resType,
                SafeHandle objectHandle,
                AccessControlSections sectionsRequested)
                : base(isContainer, resType, objectHandle, sectionsRequested)
            {
            }

            public new void Persist(SafeHandle handle, AccessControlSections includeSections)
            {
                base.Persist(handle, includeSections);
            }

            public new void AddAccessRule(AccessRule rule)
            {
                base.AddAccessRule(rule);
            }

            #region NativeObjectSecurity Abstract Method Overrides

            public override Type AccessRightType => throw new NotImplementedException();

            public override AccessRule AccessRuleFactory(
                IdentityReference identityReference,
                int accessMask,
                bool isInherited,
                InheritanceFlags inheritanceFlags,
                PropagationFlags propagationFlags,
                AccessControlType type)
            {
                throw new NotImplementedException();
            }

            public override Type AccessRuleType => typeof(AccessRule);

            public override AuditRule AuditRuleFactory(
                IdentityReference identityReference,
                int accessMask,
                bool isInherited,
                InheritanceFlags inheritanceFlags,
                PropagationFlags propagationFlags,
                AuditFlags flags)
            {
                throw new NotImplementedException();
            }

            public override Type AuditRuleType => typeof(AuditRule);

            #endregion
        }

        // Handles returned by GetProcessWindowStation and GetThreadDesktop should not be closed
        class NoopSafeHandle : SafeHandle
        {
            public NoopSafeHandle(IntPtr handle)
                :
                base(handle, false)
            {
            }

            public override bool IsInvalid => false;

            protected override bool ReleaseHandle()
            {
                return true;
            }
        }
    }
}