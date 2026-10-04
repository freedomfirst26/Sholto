namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>Builds the real <c>MainViewModel</c> + <c>MainWindow</c> pair wired to Bench's no-op fakes.</summary>
public interface IBenchAppFactory
{
    BenchApp Create();
}
