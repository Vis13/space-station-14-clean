using System.Collections.Generic;
using Content.Server.Interaction;
using Content.Server.Storage.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.Storage.EntitySystems
{
    [UsedImplicitly]
    internal sealed class StorageSystem : EntitySystem
    {
        private readonly List<IPlayerSession> _sessionCache = new();

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<EntRemovedFromContainerMessage>(HandleEntityRemovedFromContainer);
            SubscribeLocalEvent<EntInsertedIntoContainerMessage>(HandleEntityInsertedIntoContainer);
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            foreach (var component in ComponentManager.EntityQuery<ServerStorageComponent>(true))
            {
                CheckSubscribedEntities(component);
            }
        }

        private static void HandleEntityRemovedFromContainer(EntRemovedFromContainerMessage message)
        {
            var oldParentEntity = message.Container.Owner;

            if (oldParentEntity.TryGetComponent(out ServerStorageComponent? storageComp))
            {
                storageComp.HandleEntityMaybeRemoved(message);
            }

            if (oldParentEntity.TryGetComponent<StorageCounterComponent>(out var newStorageComp))
            {
                newStorageComp.ContainerUpdateAppearance(message.Container);
            }
        }

        private static void HandleEntityInsertedIntoContainer(EntInsertedIntoContainerMessage message)
        {
            var oldParentEntity = message.Container.Owner;

            if (oldParentEntity.TryGetComponent(out ServerStorageComponent? storageComp))
            {
                storageComp.HandleEntityMaybeInserted(message);
            }

            if (oldParentEntity.TryGetComponent<StorageCounterComponent>(out var newStorageComp))
            {
                newStorageComp.ContainerUpdateAppearance(message.Container);
            }
        }

        private void CheckSubscribedEntities(ServerStorageComponent storageComp)
        {

            // We have to cache the set of sessions because Unsubscribe modifies the original.
            _sessionCache.Clear();
            _sessionCache.AddRange(storageComp.SubscribedSessions);

            if (_sessionCache.Count == 0)
                return;

            var storagePos = storageComp.Owner.Transform.WorldPosition;
            var storageMap = storageComp.Owner.Transform.MapID;

            foreach (var session in _sessionCache)
            {
                var attachedEntity = session.AttachedEntity;

                // The component manages the set of sessions, so this invalid session should be removed soon.
                if (attachedEntity == null || !attachedEntity.IsValid())
                    continue;

                if (storageMap != attachedEntity.Transform.MapID)
                    continue;

                var distanceSquared = (storagePos - attachedEntity.Transform.WorldPosition).LengthSquared;
                if (distanceSquared > InteractionSystem.InteractionRangeSquared)
                {
                    storageComp.UnsubscribeSession(session);
                }
            }
        }
    }
}
