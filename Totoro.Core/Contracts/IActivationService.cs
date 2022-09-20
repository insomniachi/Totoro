namespace Totoro.Core.Contracts;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
