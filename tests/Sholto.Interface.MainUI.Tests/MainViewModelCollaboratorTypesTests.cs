using System.Reflection;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Pins that the main view model and its factory hold the deck, library-rows and output-picker
/// view models through their interfaces, not the concrete classes.</summary>
public class MainViewModelCollaboratorTypesTests
{
    private static readonly Type[] Concrete = [typeof(DeckViewModel), typeof(LibraryRowsViewModel), typeof(OutputPickerViewModel)];

    [Fact]
    public void The_concrete_view_models_implement_the_interfaces()
    {
        Assert.True(typeof(IDeckViewModel).IsAssignableFrom(typeof(DeckViewModel)));
        Assert.True(typeof(ILibraryRowsViewModel).IsAssignableFrom(typeof(LibraryRowsViewModel)));
        Assert.True(typeof(IOutputPickerViewModel).IsAssignableFrom(typeof(OutputPickerViewModel)));
    }

    [Fact]
    public void MainViewModel_constructor_takes_no_concrete_collaborator()
    {
        var parameters = typeof(MainViewModel).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parameters, p => Concrete.Contains(p.ParameterType));
        Assert.Equal(2, parameters.Count(p => p.ParameterType == typeof(IDeckViewModel)));
        Assert.Single(parameters, p => p.ParameterType == typeof(ILibraryRowsViewModel));
        Assert.Single(parameters, p => p.ParameterType == typeof(IOutputPickerViewModel));
    }

    [Fact]
    public void MainViewModelFactory_constructor_and_fields_use_the_interfaces()
    {
        var parameters = typeof(MainViewModelFactory).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parameters, p => Concrete.Contains(p.ParameterType));
        Assert.Equal(2, parameters.Count(p => p.ParameterType == typeof(IDeckViewModel)));
        Assert.Single(parameters, p => p.ParameterType == typeof(ILibraryRowsViewModel));
        Assert.Single(parameters, p => p.ParameterType == typeof(IOutputPickerViewModel));

        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = typeof(MainViewModelFactory).GetFields(Private);
        Assert.DoesNotContain(fields, f => Concrete.Contains(f.FieldType));
        Assert.Equal(typeof(ILibraryRowsViewModel), fields.Single(f => f.Name == "_rows").FieldType);
        Assert.Equal(typeof(IOutputPickerViewModel), fields.Single(f => f.Name == "_outputPicker").FieldType);
        Assert.Equal(typeof(IDeckViewModel), fields.Single(f => f.Name == "_deck1").FieldType);
        Assert.Equal(typeof(IDeckViewModel), fields.Single(f => f.Name == "_deck2").FieldType);
    }

    [Fact]
    public void MainViewModel_exposes_the_interfaces()
    {
        Assert.Equal(typeof(IDeckViewModel), typeof(MainViewModel).GetProperty(nameof(MainViewModel.Deck1))!.PropertyType);
        Assert.Equal(typeof(IDeckViewModel), typeof(MainViewModel).GetProperty(nameof(MainViewModel.Deck2))!.PropertyType);
        Assert.Equal(typeof(IDeckViewModel), typeof(MainViewModel).GetProperty(nameof(MainViewModel.EditingDeck))!.PropertyType);
        Assert.Equal(typeof(IDeckViewModel), typeof(MainViewModel).GetMethod(nameof(MainViewModel.DeckFor))!.ReturnType);
        Assert.Equal(typeof(IOutputPickerViewModel), typeof(MainViewModel).GetProperty(nameof(MainViewModel.OutputPicker))!.PropertyType);
    }
}
