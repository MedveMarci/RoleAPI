using System;
using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using RoleAPI.Internal;

namespace RoleAPI;

public class RoleApiPlugin : Plugin<RoleApiConfig>
{
    private RoleApiEventHandler? _eventHandler;
    public static RoleApiPlugin? Singleton { get; private set; }
    public override string Name => "RoleAPI";
    public override string Author => "MedveMarci";
    public override string Description => "Additive role ability and schematic framework for SCP:SL";
    public override Version Version => new(2, 0, 1);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);
    public override LoadPriority Priority => LoadPriority.High;

    public override void Enable()
    {
        Singleton = this;
        _eventHandler = new RoleApiEventHandler();
        CustomHandlersManager.RegisterEventsHandler(_eventHandler);
    }

    public override void Disable()
    {
        if (_eventHandler != null)
            CustomHandlersManager.UnregisterEventsHandler(_eventHandler);
        _eventHandler = null;
        Singleton = null;
    }
}