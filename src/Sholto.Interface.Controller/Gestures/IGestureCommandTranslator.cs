namespace Sholto.Interface.Controller.Gestures;

/// <summary>Turns a recognised gesture into the command the App understands and sends it.</summary>
internal interface IGestureCommandTranslator
{
    void Translate(in Gesture gesture);
}
