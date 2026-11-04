using System.Collections.Generic;

namespace _8F.Auth
{
    public static class PermissionMap
    {
        private static readonly HashSet<Permission> OperatorPermissions = new()
        {
            Permission.Balance,
            Permission.RunTest,
            Permission.ClearGraph,
            Permission.SelectChannel,
            Permission.ChangeOwnPassword,
            Permission.Logout,
            Permission.Exit
        };

        public static bool Can(Role role, Permission permission)
        {
            if (role == Role.Admin)
            {
                return true;
            }

            if (role == Role.Operator)
            {
                return OperatorPermissions.Contains(permission);
            }

            return false;
        }
    }
}
