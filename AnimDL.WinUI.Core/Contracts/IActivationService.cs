using System.Threading.Tasks;

namespace AnimDL.WinUI.Contracts;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
