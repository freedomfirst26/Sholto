namespace Sholto.Data;

/// <summary>Where the bus reports handler exceptions, missing handlers and runaway re-entrancy. The
/// bus swallows these so one misbehaving handler cannot stop the others (the same contract the old gesture bus had). A sink that itself throws is ignored.</summary>
public interface IHandlerFailureSink
{
    void Report(in HandlerFailure failure);
}
