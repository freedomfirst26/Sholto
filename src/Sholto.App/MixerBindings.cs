using Sholto.Controller;
using Sholto.Controller.Gestures;

namespace Sholto.App;

/// <summary>Gesture bindings for the mixer: EQ, per-stem levels, filter, channel
/// volume, tempo, crossfader. Extracted verbatim from
/// <c>Orchestrator.HandleGesture</c> — see <see cref="TransportBindings"/>'s doc for
/// why. Depends only on <see cref="IApplication"/>, so it is testable with a fake
/// deck and no real Orchestrator.</summary>
public static class MixerBindings
{
    public static void Add(Dictionary<string, Action<Gesture>> map, IApplication app)
    {
        map[GestureIds.CrossfaderMove] = g =>
            app.Crossfader = ((ControllerEvent.CrossfaderMoved)g.Source).Position;

        map[GestureIds.VolumeMove] = g =>
            app.DeckFor(g.Deck).ChannelGain = ((ControllerEvent.ChannelVolumeMoved)g.Source).Value;

        map[GestureIds.EqTurn] = g =>
        {
            var e = (ControllerEvent.EqMoved)g.Source;
            app.DeckFor(g.Deck).Player.SetEq((int)e.Band, e.Value);
        };

        // HI → Drums, MID → Vocals, LOW → Instrumental, on either deck.
        map[GestureIds.EqStemLevelTurn] = g =>
        {
            var e = (ControllerEvent.EqMoved)g.Source;
            var deckVm = app.DeckFor(g.Deck);
            switch (e.Band)
            {
                case EqBand.High: deckVm.DrumsLevel        = e.Value; break;
                case EqBand.Mid:  deckVm.VocalsLevel       = e.Value; break;
                default:          deckVm.InstrumentalLevel = e.Value; break;
            }
        };

        map[GestureIds.FilterTurn] = g =>
            app.DeckFor(g.Deck).Player.SetFilter(((ControllerEvent.FilterMoved)g.Source).Position);

        map[GestureIds.TempoMove] = g =>
            app.DeckFor(g.Deck).SetTempoPosition(((ControllerEvent.TempoMoved)g.Source).Position);
    }
}
