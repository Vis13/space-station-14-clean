using System.Linq;
using System.Threading.Tasks;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.IntegrationTests.Tests.Destructible.DestructibleTestPrototypes;

namespace Content.IntegrationTests.Tests.Destructible
{
    public class DestructibleDestructionTest : ContentIntegrationTest
    {
        [Test]
        public async Task Test()
        {
            var server = StartServerDummyTicker(new ServerContentIntegrationOption
            {
                ExtraPrototypes = Prototypes,
                ContentBeforeIoC = () =>
                {
                    IoCManager.Resolve<IComponentFactory>().RegisterClass<TestThresholdListenerComponent>();
                }
            });

            await server.WaitIdleAsync();

            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sMapManager = server.ResolveDependency<IMapManager>();
            var sPrototypeManager = server.ResolveDependency<IPrototypeManager>();

            IEntity sDestructibleEntity = null;
            IDamageableComponent sDamageableComponent = null;
            TestThresholdListenerComponent sThresholdListenerComponent = null;

            await server.WaitPost(() =>
            {
                var mapId = new MapId(1);
                var coordinates = new MapCoordinates(0, 0, mapId);
                sMapManager.CreateMap(mapId);

                sDestructibleEntity = sEntityManager.SpawnEntity(DestructibleDestructionEntityId, coordinates);
                sDamageableComponent = sDestructibleEntity.GetComponent<IDamageableComponent>();
                sThresholdListenerComponent = sDestructibleEntity.GetComponent<TestThresholdListenerComponent>();
            });

            await server.WaitAssertion(() =>
            {
                var coordinates = sDestructibleEntity.Transform.Coordinates;
                var bruteDamageGroup = sPrototypeManager.Index<DamageGroupPrototype>("TestBrute");

                Assert.DoesNotThrow(() =>
                {
                    Assert.True(sDamageableComponent.TryChangeDamage(bruteDamageGroup, 50, true));
                });

                Assert.That(sThresholdListenerComponent.ThresholdsReached.Count, Is.EqualTo(1));

                var threshold = sThresholdListenerComponent.ThresholdsReached[0].Threshold;

                Assert.That(threshold.Triggered, Is.True);
                Assert.That(threshold.Behaviors.Count, Is.EqualTo(3));

                var spawnEntitiesBehavior = (SpawnEntitiesBehavior) threshold.Behaviors.Single(b => b is SpawnEntitiesBehavior);

                Assert.That(spawnEntitiesBehavior.Spawn.Count, Is.EqualTo(1));
                Assert.That(spawnEntitiesBehavior.Spawn.Keys.Single(), Is.EqualTo(SpawnedEntityId));
                Assert.That(spawnEntitiesBehavior.Spawn.Values.Single(), Is.EqualTo(new MinMax {Min = 1, Max = 1}));

                var entitiesInRange = IoCManager.Resolve<IEntityLookup>().GetEntitiesInRange(coordinates, 2);
                var found = false;

                foreach (var entity in entitiesInRange)
                {
                    if (entity.Prototype == null)
                    {
                        continue;
                    }

                    if (entity.Prototype.Name != SpawnedEntityId)
                    {
                        continue;
                    }

                    found = true;
                    break;
                }

                Assert.That(found, Is.True);
            });
        }
    }
}
