using Sholto.Data;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class WaveformStyleViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly F9RecordingCommandHandler<ChooseWaveformStyle> _sent = new();
    private readonly IWaveformStyles _styles = new WaveformStylesFactory().Create();
    private readonly WaveformStyleViewModel _vm;
    private readonly List<string?> _changed = [];

    public WaveformStyleViewModelTests()
    {
        _bus.Register<ChooseWaveformStyle>(_sent);
        _vm = new WaveformStyleViewModel(_styles, _bus);
        _vm.PropertyChanged += (_, e) => _changed.Add(e.PropertyName);
    }

    private IWaveformStyleStrategy Rgb => _styles.ById("rgb");

    [Fact]
    public void Starts_on_the_default_style()
    {
        Assert.Same(_styles.Default, _vm.Shown);
        Assert.Same(_styles.Default, _vm.Chosen);
    }

    [Fact]
    public void Choosing_shows_it_and_tells_the_app_once()
    {
        _vm.Choose(Rgb);
        _vm.Choose(Rgb);

        Assert.Same(Rgb, _vm.Shown);
        Assert.Same(Rgb, _vm.Chosen);
        Assert.Equal(["rgb"], _sent.Received.Select(c => c.Id));
        Assert.Equal([nameof(IWaveformStyleViewModel.Shown), nameof(IWaveformStyleViewModel.Chosen)], _changed);
    }

    [Fact]
    public void A_preview_shows_without_choosing_and_choosing_it_after_still_tells_the_app()
    {
        _vm.Preview(Rgb);
        Assert.Same(Rgb, _vm.Shown);
        Assert.Same(_styles.Default, _vm.Chosen);
        Assert.Empty(_sent.Received);

        _vm.Choose(Rgb);
        Assert.Equal(["rgb"], _sent.Received.Select(c => c.Id));
    }

    [Fact]
    public void Previewing_the_chosen_style_again_reverts_a_preview()
    {
        _vm.Preview(Rgb);
        _vm.Preview(_vm.Chosen);
        Assert.Same(_styles.Default, _vm.Shown);
        Assert.Empty(_sent.Received);
    }

    [Fact]
    public void Restoring_a_saved_id_applies_it_without_sending_it_back()
    {
        _vm.Restore("rgb");
        Assert.Same(Rgb, _vm.Shown);
        Assert.Same(Rgb, _vm.Chosen);
        Assert.Empty(_sent.Received);
    }

    [Fact]
    public void Restoring_an_unknown_id_keeps_the_default()
    {
        _vm.Restore("blue");
        Assert.Same(_styles.Default, _vm.Shown);
        Assert.Empty(_changed);
    }
}
