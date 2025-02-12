using System.Collections.Generic;
using System.Linq;
using Content.Client.Animations;
using Content.Client.Items.Components;
using Content.Client.Hands;
using Content.Client.UserInterface.Controls;
using Content.Shared.DragDrop;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Players;
using static Robust.Client.UserInterface.Control;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Storage
{
    /// <summary>
    /// Client version of item storage containers, contains a UI which displays stored entities and their size
    /// </summary>
    [RegisterComponent]
    public class ClientStorageComponent : SharedStorageComponent, IDraggable
    {
        private List<IEntity> _storedEntities = new();
        private int StorageSizeUsed;
        private int StorageCapacityMax;
        private StorageWindow? _window;
        private SharedBagState _bagState;

        public override IReadOnlyList<IEntity> StoredEntities => _storedEntities;

        protected override void Initialize()
        {
            base.Initialize();

            // Hide stackVisualizer on start
            ChangeStorageVisualization(SharedBagState.Close);
        }

        protected override void OnAdd()
        {
            base.OnAdd();

            _window = new StorageWindow(this) {Title = Owner.Name};
            _window.EntityList.GenerateItem += GenerateButton;
            _window.EntityList.ItemPressed += Interact;
        }

        protected override void OnRemove()
        {
            _window?.Dispose();
            base.OnRemove();
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not StorageComponentState state)
            {
                return;
            }

            _storedEntities = state.StoredEntities
                .Select(id => Owner.EntityManager.GetEntity(id))
                .ToList();
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel channel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, channel, session);

            switch (message)
            {
                //Updates what we are storing for the UI
                case StorageHeldItemsMessage msg:
                    HandleStorageMessage(msg);
                    ChangeStorageVisualization(_bagState);
                    break;
                //Opens the UI
                case OpenStorageUIMessage _:
                    ChangeStorageVisualization(SharedBagState.Open);
                    ToggleUI();
                    break;
                case CloseStorageUIMessage _:
                    ChangeStorageVisualization(SharedBagState.Close);
                    CloseUI();
                    break;
                case AnimateInsertingEntitiesMessage msg:
                    HandleAnimatingInsertingEntities(msg);
                    break;
            }
        }

        /// <summary>
        /// Copies received values from server about contents of storage container
        /// </summary>
        /// <param name="storageState"></param>
        private void HandleStorageMessage(StorageHeldItemsMessage storageState)
        {
            _storedEntities = storageState.StoredEntities.Select(id => Owner.EntityManager.GetEntity(id)).ToList();
            StorageSizeUsed = storageState.StorageSizeUsed;
            StorageCapacityMax = storageState.StorageSizeMax;
            _window?.BuildEntityList(storageState.StoredEntities.ToList());
        }

        /// <summary>
        /// Animate the newly stored entities in <paramref name="msg"/> flying towards this storage's position
        /// </summary>
        /// <param name="msg"></param>
        private void HandleAnimatingInsertingEntities(AnimateInsertingEntitiesMessage msg)
        {
            for (var i = 0; msg.StoredEntities.Count > i; i++)
            {
                var entityId = msg.StoredEntities[i];
                var initialPosition = msg.EntityPositions[i];

                if (Owner.EntityManager.TryGetEntity(entityId, out var entity))
                {
                    ReusableAnimations.AnimateEntityPickup(entity, initialPosition, Owner.Transform.LocalPosition);
                }
            }
        }

        /// <summary>
        /// Opens the storage UI if closed. Closes it if opened.
        /// </summary>
        private void ToggleUI()
        {
            if (_window == null) return;

            if (_window.IsOpen)
                _window.Close();
            else
                _window.OpenCentered();
        }

        private void CloseUI()
        {
            _window?.Close();
        }

        private void ChangeStorageVisualization(SharedBagState state)
        {
            _bagState = state;
            if (Owner.TryGetComponent<AppearanceComponent>(out var appearanceComponent))
            {
                appearanceComponent.SetData(SharedBagOpenVisuals.BagState, state);
            }
        }

        /// <summary>
        /// Function for clicking one of the stored entity buttons in the UI, tells server to remove that entity
        /// </summary>
        /// <param name="entityUid"></param>
        private void Interact(EntityUid entityUid)
        {
            SendNetworkMessage(new RemoveEntityMessage(entityUid));
        }

        public override bool Remove(IEntity entity)
        {
            if (_storedEntities.Remove(entity))
            {
                Dirty();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Button created for each entity that represents that item in the storage UI, with a texture, and name and size label
        /// </summary>
        private void GenerateButton(EntityUid entityUid, Control button)
        {
            if (!Owner.EntityManager.TryGetEntity(entityUid, out var entity))
                return;

            entity.TryGetComponent(out ISpriteComponent? sprite);
            entity.TryGetComponent(out ItemComponent? item);

            button.AddChild(new HBoxContainer
            {
                SeparationOverride = 2,
                Children =
                {
                    new SpriteView
                    {
                        HorizontalAlignment = HAlignment.Left,
                        VerticalAlignment = VAlignment.Center,
                        MinSize = new Vector2(32.0f, 32.0f),
                        OverrideDirection = Direction.South,
                        Sprite = sprite
                    },
                    new Label
                    {
                        HorizontalExpand = true,
                        ClipText = true,
                        Text = entity.Name
                    },
                    new Label
                    {
                        Align = Label.AlignMode.Right,
                        Text = item?.Size.ToString() ?? Loc.GetString("no-item-size")
                    }
                }
            });
        }

        /// <summary>
        /// GUI class for client storage component
        /// </summary>
        private class StorageWindow : SS14Window
        {
            private Control _vBox;
            private readonly Label _information;
            public readonly EntityListDisplay EntityList;
            public ClientStorageComponent StorageEntity;

            private readonly StyleBoxFlat _hoveredBox = new() { BackgroundColor = Color.Black.WithAlpha(0.35f) };
            private readonly StyleBoxFlat _unHoveredBox = new() { BackgroundColor = Color.Black.WithAlpha(0.0f) };

            public StorageWindow(ClientStorageComponent storageEntity)
            {
                StorageEntity = storageEntity;
                SetSize = (200, 320);
                Title = Loc.GetString("comp-storage-window-title");
                RectClipContent = true;

                var containerButton = new ContainerButton
                {
                    Name = "StorageContainerButton",
                    MouseFilter = MouseFilterMode.Pass,
                };
                Contents.AddChild(containerButton);

                var innerContainerButton = new PanelContainer
                {
                    PanelOverride = _unHoveredBox,
                };

                containerButton.AddChild(innerContainerButton);
                containerButton.OnPressed += args =>
                {
                    var controlledEntity = IoCManager.Resolve<IPlayerManager>().LocalPlayer?.ControlledEntity;

                    if (controlledEntity?.TryGetComponent(out HandsComponent? hands) ?? false)
                    {
                        StorageEntity.SendNetworkMessage(new InsertEntityMessage());
                    }
                };

                _vBox = new BoxContainer()
                {
                    Orientation = LayoutOrientation.Vertical,
                    MouseFilter = MouseFilterMode.Ignore,
                };
                containerButton.AddChild(_vBox);
                _information = new Label
                {
                    Text = Loc.GetString("comp-storage-window-volume", ("itemCount", 0), ("usedVolume", 0), ("maxVolume", 0)),
                    VerticalAlignment = VAlignment.Center
                };
                _vBox.AddChild(_information);

                EntityList = new EntityListDisplay
                {
                    Name = "EntityListContainer",
                };
                _vBox.AddChild(EntityList);
                EntityList.OnMouseEntered += args =>
                {
                    innerContainerButton.PanelOverride = _hoveredBox;
                };

                EntityList.OnMouseExited += args =>
                {
                    innerContainerButton.PanelOverride = _unHoveredBox;
                };
            }

            public override void Close()
            {
                StorageEntity.SendNetworkMessage(new CloseStorageUIMessage());
                base.Close();
            }

            /// <summary>
            /// Loops through stored entities creating buttons for each, updates information labels
            /// </summary>
            public void BuildEntityList(List<EntityUid> entityUids)
            {
                EntityList.PopulateList(entityUids);

                //Sets information about entire storage container current capacity
                if (StorageEntity.StorageCapacityMax != 0)
                {
                    _information.Text = Loc.GetString("comp-storage-window-volume", ("itemCount", entityUids.Count),
                        ("usedVolume", StorageEntity.StorageSizeUsed), ("maxVolume", StorageEntity.StorageCapacityMax));
                }
                else
                {
                    _information.Text = Loc.GetString("comp-storage-window-volume-unlimited", ("itemCount", entityUids.Count));
                }
            }
        }
    }
}
