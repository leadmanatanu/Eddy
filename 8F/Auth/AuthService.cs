using System;
using System.Linq;
using System.Threading.Tasks;

namespace _8F.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _repository;
        private readonly IPasswordHasher _hasher;
        private readonly ISessionContext _sessionContext;

        public AuthService(IAuthRepository repository, IPasswordHasher hasher, ISessionContext sessionContext)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        }

        public ISessionContext Session => _sessionContext;

        public async Task<int> GetCredentialCountAsync()
        {
            return await _repository.GetCredentialCountAsync();
        }

        public async Task SetInitialPasswordsAsync(string adminPw, string operatorPw)
        {
            if (string.IsNullOrWhiteSpace(adminPw) || adminPw.Length < 6)
            {
                throw new ArgumentException("Admin password must be at least 6 characters.", nameof(adminPw));
            }

            if (string.IsNullOrWhiteSpace(operatorPw) || operatorPw.Length < 6)
            {
                throw new ArgumentException("Operator password must be at least 6 characters.", nameof(operatorPw));
            }

            if (adminPw == operatorPw)
            {
                throw new InvalidOperationException("Admin and Operator passwords must be different.");
            }

            var adminHash = _hasher.HashPassword(adminPw);
            var operatorHash = _hasher.HashPassword(operatorPw);

            var adminCred = new StoredCredential
            {
                Role = Role.Admin,
                PasswordHash = adminHash.Hash,
                Salt = adminHash.Salt,
                Iterations = adminHash.Iterations,
                UpdatedByRole = "Setup"
            };

            var operatorCred = new StoredCredential
            {
                Role = Role.Operator,
                PasswordHash = operatorHash.Hash,
                Salt = operatorHash.Salt,
                Iterations = operatorHash.Iterations,
                UpdatedByRole = "Setup"
            };

            await _repository.SaveInitialCredentialsAsync(adminCred, operatorCred);
            await _repository.LogAuditAsync("FirstRunSetup", null, "Initial credentials configured");
        }

        public async Task<Role> AuthenticateAsync(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return Role.None;
            }

            var credentials = await _repository.GetAllCredentialsAsync();
            foreach (var cred in credentials)
            {
                if (_hasher.VerifyPassword(password, cred.PasswordHash, cred.Salt, cred.Iterations))
                {
                    await _repository.LogAuditAsync("LoginSuccess", cred.Role, $"Role {cred.Role} authenticated");
                    return cred.Role;
                }
            }

            await _repository.LogAuditAsync("LoginFailed", null, "Invalid password attempt");
            return Role.None;
        }

        public async Task<(bool Success, string ErrorMessage)> ChangeOwnPasswordAsync(Role current, string oldPw, string newPw)
        {
            if (current == Role.None)
            {
                return (false, "No active session.");
            }

            if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 6)
            {
                return (false, "New password must be at least 6 characters.");
            }

            if (oldPw == newPw)
            {
                return (false, "New password must differ from the old password.");
            }

            var credentials = await _repository.GetAllCredentialsAsync();
            var myCred = credentials.FirstOrDefault(c => c.Role == current);
            if (myCred == null)
            {
                return (false, "Credential record not found.");
            }

            if (!_hasher.VerifyPassword(oldPw, myCred.PasswordHash, myCred.Salt, myCred.Iterations))
            {
                return (false, "Current password is incorrect.");
            }

            // Rule 5.1: The two passwords must always differ
            Role otherRole = (current == Role.Admin) ? Role.Operator : Role.Admin;
            var otherCred = credentials.FirstOrDefault(c => c.Role == otherRole);
            if (otherCred != null && _hasher.VerifyPassword(newPw, otherCred.PasswordHash, otherCred.Salt, otherCred.Iterations))
            {
                return (false, "This password is already in use. Choose a different password.");
            }

            var newHash = _hasher.HashPassword(newPw);
            myCred.PasswordHash = newHash.Hash;
            myCred.Salt = newHash.Salt;
            myCred.Iterations = newHash.Iterations;
            myCred.UpdatedByRole = current.ToString();

            await _repository.UpdateCredentialAsync(myCred);
            await _repository.LogAuditAsync("PasswordChanged", current, $"Password changed by {current}");

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ResetOperatorPasswordAsync(string newPw)
        {
            if (!Can(Permission.ResetOtherPassword))
            {
                return (false, "Admin permission required to reset Operator password.");
            }

            if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 6)
            {
                return (false, "New password must be at least 6 characters.");
            }

            var credentials = await _repository.GetAllCredentialsAsync();
            var adminCred = credentials.FirstOrDefault(c => c.Role == Role.Admin);
            var operatorCred = credentials.FirstOrDefault(c => c.Role == Role.Operator);

            if (operatorCred == null)
            {
                return (false, "Operator credential record not found.");
            }

            // Rule 5.1: Must not match Admin password
            if (adminCred != null && _hasher.VerifyPassword(newPw, adminCred.PasswordHash, adminCred.Salt, adminCred.Iterations))
            {
                return (false, "This password is already in use. Choose a different password.");
            }

            var newHash = _hasher.HashPassword(newPw);
            operatorCred.PasswordHash = newHash.Hash;
            operatorCred.Salt = newHash.Salt;
            operatorCred.Iterations = newHash.Iterations;
            operatorCred.UpdatedByRole = Role.Admin.ToString();

            await _repository.UpdateCredentialAsync(operatorCred);
            await _repository.LogAuditAsync("PasswordReset", Role.Admin, "Operator password reset by Admin");

            return (true, string.Empty);
        }

        public bool Can(Permission p)
        {
            return PermissionMap.Can(_sessionContext.CurrentRole, p);
        }

        public void Require(Permission p)
        {
            if (!Can(p))
            {
                throw new UnauthorizedAccessException("Admin access required.");
            }
        }

        public async Task LogAuditAsync(string eventName, Role? role, string? detail)
        {
            await _repository.LogAuditAsync(eventName, role, detail);
        }
    }
}
