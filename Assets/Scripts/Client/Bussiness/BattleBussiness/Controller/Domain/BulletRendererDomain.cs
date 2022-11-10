using System.Collections.Generic;
using UnityEngine;
using Game.Client.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class BulletRendererDomain
    {

        BattleFacades battleFacades;

        public BulletRendererDomain()
        {
        }

        public void Inject(BattleFacades facades)
        {
            this.battleFacades = facades;
        }

        public void Update_AllBulletRenderer(float deltaTime)
        {
            var repo = battleFacades.Repo;
            var bulletLogicRepo = repo.BulletLogicRepo;
            var bulletRendererRepo = repo.BulletRendererRepo;

            bulletLogicRepo.Foreach((bulletLogic) =>
            {
                var bulletRenderer = bulletRendererRepo.Get(bulletLogic.IDComponent.EntityID);
                if (bulletRenderer != null)
                {
                    bulletRenderer.SetPosition(Vector3.Lerp(bulletRenderer.transform.position, bulletLogic.Position, deltaTime * bulletRenderer.posAdjust));
                    bulletRenderer.SetRotation(Quaternion.Lerp(bulletRenderer.transform.rotation, bulletLogic.Rotation, deltaTime * bulletRenderer.rotAdjust));
                }
            });
        }

        public BulletRendererEntity SpawnBulletRenderer(BulletType bulletType, int entityID)
        {
            string bulletPrefabName = bulletType.ToString() + "_Renderer";

            if (battleFacades.Assets.BulletAsset.TryGetByName(bulletPrefabName, out GameObject prefabAsset))
            {
                var repo = battleFacades.Repo;
                var parent = repo.FieldRepo.CurFieldEntity.transform;
                prefabAsset = GameObject.Instantiate(prefabAsset, parent);

                var bulletRenderer = prefabAsset.GetComponent<BulletRendererEntity>();
                bulletRenderer.Ctor();
                bulletRenderer.SetEntityID(entityID);
                bulletRenderer.SetBulletType(bulletType);

                repo.BulletRendererRepo.Add(bulletRenderer);
                return bulletRenderer;
            }

            return null;
        }

        public void TearDown(BulletRendererEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            entity.TearDown();
            var repo = battleFacades.Repo;
            var bulletRendererRepo = repo.BulletRendererRepo;
            bulletRendererRepo.TryRemove(entity);
        }

        public void TearDown(int entityID)
        {
            var repo = battleFacades.Repo;
            var bulletRendererRepo = repo.BulletRendererRepo;
            var bulletRenderer = bulletRendererRepo.Get(entityID);
            TearDown(bulletRenderer);
        }

        public void LifeOver(BulletRendererEntity bullet)
        {
            if (bullet == null)
            {
                return;
            }

            bullet.TearDown();
            var bulletRepo = battleFacades.Repo.BulletRendererRepo;
            bulletRepo.TryRemove(bullet);
            Debug.Log($"Bullet LifeOver: {bullet.EntityID}");
        }

        public void LifeOver(int bulletID)
        {
            var bullet = battleFacades.Repo.BulletRendererRepo.Get(bulletID);
            LifeOver(bullet);
        }

        public void ApplyEffector_BulletHitField(BulletRendererEntity bulletRenderer, Transform hitTF)
        {
            var bulletType = bulletRenderer.BulletType;
            if (bulletType == BulletType.DefaultBullet)
            {
                // - vfx
                var vfxGo = GameObject.Instantiate(bulletRenderer.vfxPrefab_hitField);
                vfxGo.transform.position = bulletRenderer.transform.position;
                vfxGo.GetComponent<ParticleSystem>().Play();

                TearDown(bulletRenderer);
                return;
            }

            Debug.LogWarning($"Not Handler {bulletRenderer.BulletType}");
        }

    }

}