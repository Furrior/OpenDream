using OpenDreamClient.Rendering;
using OpenDreamShared.Dream;
using OpenDreamShared.Rendering;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace OpenDreamClient.Input.ContextMenu;

[GenerateTypedNameReferences]
internal sealed partial class ContextMenuPopup : Popup {
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    private readonly ClientAppearanceSystem? _appearanceSystem;
    private readonly TransformSystem? _transformSystem;
    private readonly ClientVerbSystem? _verbSystem;
    private readonly EntityQuery<DMISpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<DreamMobSightComponent> _mobSightQuery;
    private readonly EntityQuery<MetaDataComponent> _metadataQuery;

    public int EntityCount => ContextMenu.ChildCount;

    private VerbMenuPopup? _currentVerbMenu;

    public ContextMenuPopup() {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _entitySystemManager.TryGetEntitySystem(out _transformSystem);
        _entitySystemManager.TryGetEntitySystem(out _verbSystem);
        _entitySystemManager.TryGetEntitySystem(out _appearanceSystem);
        _spriteQuery = _entityManager.GetEntityQuery<DMISpriteComponent>();
        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        _mobSightQuery = _entityManager.GetEntityQuery<DreamMobSightComponent>();
        _metadataQuery = _entityManager.GetEntityQuery<MetaDataComponent>();
    }

    public void RepopulateEntities(ClientObjectReference[] entities, int? turfId) {
        ContextMenu.RemoveAllChildren();

        if (_transformSystem == null)
            return;

        foreach (var objectReference in entities) {
            if (objectReference.Type == ClientObjectReference.RefType.Entity) {
                var entity = _entityManager.GetEntity(objectReference.Entity);
                if (_xformQuery.TryGetComponent(entity, out TransformComponent? transform) && !_mapManager.IsGrid(_transformSystem.GetParentUid(entity))) // Not a child of another entity
                    continue;
                if (!_spriteQuery.TryGetComponent(entity, out DMISpriteComponent? sprite)) // Has a sprite
                    continue;
                if (sprite.Icon.Appearance?.MouseOpacity == MouseOpacity.Transparent) // Not transparent to mouse clicks
                    continue;
                if (!sprite.IsVisible(transform, GetSeeInvisible())) // Not invisible
                    continue;

                var metadata = _metadataQuery.GetComponent(entity);
                if (string.IsNullOrEmpty(metadata.EntityName)) // Has a name
                    continue;

                ContextMenu.AddChild(new ContextMenuItem(this, objectReference, metadata.EntityName, sprite.Icon));
            } else if (objectReference.Type == ClientObjectReference.RefType.Turf && turfId is not null && _appearanceSystem is not null) {
                var icon = _appearanceSystem.GetTurfIcon(turfId.Value);
                if(icon.Appearance is null) continue;

                ContextMenu.AddChild(new ContextMenuItem(this, objectReference, icon.Appearance.Name, icon));
            }
        }
    }

    public void SetActiveItem(ContextMenuItem item) {
        if (_currentVerbMenu != null) {
            _currentVerbMenu.Close();
            _uiManager.ModalRoot.RemoveChild(_currentVerbMenu);
        }

        _currentVerbMenu = new VerbMenuPopup(_verbSystem, GetSeeInvisible(), item.Target);

        _currentVerbMenu.OnVerbSelected += Close;

        Vector2 desiredSize = _currentVerbMenu.DesiredSize;
        Vector2 verbMenuPos = item.GlobalPosition with { X = item.GlobalPosition.X + item.Size.X };
        _uiManager.ModalRoot.AddChild(_currentVerbMenu);
        _currentVerbMenu.Open(UIBox2.FromDimensions(verbMenuPos, desiredSize));
    }

    /// <returns>The see_invisible of our current mob</returns>
    private sbyte GetSeeInvisible() {
        if (_playerManager.LocalSession == null)
            return 127;
        if (!_mobSightQuery.TryGetComponent(_playerManager.LocalSession.AttachedEntity, out DreamMobSightComponent? sight))
            return 127;

        return sight.SeeInvisibility;
    }
}
