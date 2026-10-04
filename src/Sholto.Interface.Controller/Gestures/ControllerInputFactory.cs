using Sholto.Data;
using Sholto.Interface.Controller.Gestures;

namespace Sholto.Interface.Controller;

public sealed class ControllerInputFactory : IControllerInputFactory
{
    public IControllerInput Create(IControlSurface surface, ICommandSender sender, IAppThread appThread,
        IFrameClock clock)
    {
        var recognizer = new GestureRecognizer();
        return new ControllerInput(surface, recognizer, new GestureCommandTranslator(sender, recognizer),
            appThread, clock);
    }
}
