namespace MachineInfo.Application.Interfaces
{
    public interface IAppSecretProvider
    {
        byte[] GetSecret();
    }
}
