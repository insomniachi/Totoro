namespace Totoro.UI.Core.Contracts;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
