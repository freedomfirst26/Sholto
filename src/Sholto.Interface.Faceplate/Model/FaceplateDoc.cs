namespace Sholto.Interface.Faceplate.Model;

/// <summary>The words for one controller. Loaded from JSON.
/// <para>This file describes; it never resolves. It carries no event names and no
/// match rules — the controller interface owns all
/// of that. The gesture id is the only join between the two.</para></summary>
public sealed record FaceplateDoc(
    string Device,
    IReadOnlyList<LayerDoc> Layers,
    IReadOnlyList<ControlDoc> Controls);
