using System.Threading.Tasks;

namespace AnimDL.WinUI.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
