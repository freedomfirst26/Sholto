using Sholto.Controller;
using Sholto.Controller.Gestures;
using Xunit;

namespace Sholto.Controller.Tests;

public class GestureBusTests
{
    private static Gesture Play() =>
        new(GestureIds.PlayPress, 0, new ControllerEvent.PlayPressed(0));

    private static GestureBindings Table(string name, List<string> log, params string[] ids)
        => new(name, ids.ToDictionary(id => id, id => new Action<Gesture>(_ => log.Add($"{name}:{id}"))));

    [Fact]
    public void Every_enabled_table_receives_the_gesture()
    {
        var log = new List<string>();
        var bus = new GestureBus();
        bus.Register(Table("app", log, GestureIds.PlayPress));
        bus.Register(Table("guide", log, GestureIds.PlayPress));

        bus.Dispatch(Play());

        Assert.Equal(["app:play.press", "guide:play.press"], log);
    }

    [Fact]
    public void Tables_run_in_registration_order_so_the_app_acts_before_observers_watch()
    {
        var log = new List<string>();
        var bus = new GestureBus();
        bus.Register(Table("first", log, GestureIds.PlayPress));
        bus.Register(Table("second", log, GestureIds.PlayPress));
        bus.Dispatch(Play());
        Assert.Equal("first:play.press", log[0]);
    }

    [Fact]
    public void A_disabled_table_receives_nothing_but_stays_registered()
    {
        var log = new List<string>();
        var bus = new GestureBus();
        var app = Table("app", log, GestureIds.PlayPress);
        bus.Register(app);

        app.Enabled = false;
        bus.Dispatch(Play());
        Assert.Empty(log);

        app.Enabled = true;          // Inspect mode ends; the decks come back
        bus.Dispatch(Play());
        Assert.Equal(["app:play.press"], log);
    }

    [Fact]
    public void A_gesture_no_table_binds_is_simply_ignored()
    {
        var bus = new GestureBus();
        bus.Register(Table("app", [], GestureIds.CrossfaderMove));
        bus.Dispatch(Play());        // must not throw
    }

    [Fact]
    public void One_table_throwing_does_not_rob_the_others()
    {
        // A crash in the guide overlay must never stop the decks responding.
        var log = new List<string>();
        var bus = new GestureBus();
        bus.Register(new GestureBindings("bad",
            new Dictionary<string, Action<Gesture>>
            { [GestureIds.PlayPress] = _ => throw new InvalidOperationException("boom") }));
        bus.Register(Table("app", log, GestureIds.PlayPress));

        bus.Dispatch(Play());

        Assert.Equal(["app:play.press"], log);
    }

    [Fact]
    public void Unregister_removes_a_table()
    {
        var log = new List<string>();
        var bus = new GestureBus();
        var t = Table("app", log, GestureIds.PlayPress);
        bus.Register(t);
        bus.Unregister(t);
        bus.Dispatch(Play());
        Assert.Empty(log);
    }

    [Fact]
    public void BoundIds_reports_what_a_table_answers_to()
    {
        var t = Table("app", [], GestureIds.PlayPress, GestureIds.CrossfaderMove);
        Assert.Equal([GestureIds.CrossfaderMove, GestureIds.PlayPress], t.BoundIds.OrderBy(x => x));
    }
}
