namespace AnimDL.UI.Core.Contracts;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
