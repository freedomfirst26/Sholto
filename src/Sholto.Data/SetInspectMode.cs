namespace Sholto.Data;

/// <summary>Turn Inspect mode on or off. While it is on, commands from the physical input interfaces
/// (controller, keyboard) are not executed: the App echoes each one as <see cref="CommandReceived"/>
/// instead, so the guide can explain a control without the control acting. Sent by the Faceplate when
/// it is mounted and unmounted.</summary>
public readonly record struct SetInspectMode(bool On, Origin Origin) : ICommand;
