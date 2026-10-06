namespace Sholto.Interface.Faceplate.Views;

/// <summary>Formats a gesture's combination as <c>[[control.id]]</c> chip markers.</summary>
public interface IComboMarkerFormatter
{
    string Format(IEnumerable<string> partnerIds, string ownerId);
}
