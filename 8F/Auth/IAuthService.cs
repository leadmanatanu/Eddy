using System.Threading.Tasks;

namespace _8F.Auth
{
    public interface IAuthService
    {
        ISessionContext Session { get; }
        Task<int> GetCredentialCountAsync();
        Task SetInitialPasswordsAsync(string adminPw, string operatorPw);
        Task<Role> AuthenticateAsync(string password);
        Task<(bool Success, string ErrorMessage)> ChangeOwnPasswordAsync(Role current, string oldPw, string newPw);
        Task<(bool Success, string ErrorMessage)> ResetOperatorPasswordAsync(string newPw);
        bool Can(Permission p);
        void Require(Permission p);
        Task LogAuditAsync(string eventName, Role? role, string? detail);
    }
}
