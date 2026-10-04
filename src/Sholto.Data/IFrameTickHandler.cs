namespace Sholto.Data;

/// <summary>Receives the frame clock's tick.</summary>
public interface IFrameTickHandler
{
    void OnFrame(DateTime now);
}
