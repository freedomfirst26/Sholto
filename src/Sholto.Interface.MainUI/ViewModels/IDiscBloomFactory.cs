namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds one <see cref="IDiscBloom"/> per deck.</summary>
public interface IDiscBloomFactory
{
    IDiscBloom Create();
}
