using System;

namespace _8F.Auth
{
    public interface ISessionContext
    {
        Role CurrentRole { get; }
        bool IsAuthenticated { get; }
        event EventHandler<Role>? SessionChanged;
        void SetSession(Role role);
        void ClearSession();
    }

    public class SessionContext : ISessionContext
    {
        private Role _currentRole = Role.None;

        public Role CurrentRole => _currentRole;
        public bool IsAuthenticated => _currentRole != Role.None;

        public event EventHandler<Role>? SessionChanged;

        public void SetSession(Role role)
        {
            _currentRole = role;
            SessionChanged?.Invoke(this, _currentRole);
        }

        public void ClearSession()
        {
            _currentRole = Role.None;
            SessionChanged?.Invoke(this, _currentRole);
        }
    }
}
