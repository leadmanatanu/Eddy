using System.Collections.Generic;
using System.Threading.Tasks;

namespace _8F.Auth
{
    public interface IAuthRepository
    {
        Task EnsureAuthTablesCreatedAsync();
        Task<int> GetCredentialCountAsync();
        Task<List<StoredCredential>> GetAllCredentialsAsync();
        Task<StoredCredential?> GetCredentialByRoleAsync(Role role);
        Task SaveInitialCredentialsAsync(StoredCredential adminCred, StoredCredential operatorCred);
        Task UpdateCredentialAsync(StoredCredential cred);
        Task LogAuditAsync(string eventName, Role? role, string? detail);
    }
}
