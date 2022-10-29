using System.Collections.Generic;
using UnityEngine;
using Game.Client.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class BulletDomain
    {

        BattleFacades battleFacades;

        public BulletDomain()
        {
        }

        public void Inject(BattleFacades facades)
        {
            this.battleFacades = facades;
        }

        public BulletEntity SpawnBullet(BulletType bulletType, int bulletEntityId, int masterEntityId, Vector3 startPos, Vector3 fireDir)
        {
            string bulletPrefabName = bulletType.ToString();

            if (battleFacades.Assets.BulletAsset.TryGetByName(bulletPrefabName, out GameObject prefabAsset))
            {
                var parent = battleFacades.Repo.FiledRepo.CurFieldEntity.transform;
                prefabAsset = GameObject.Instantiate(prefabAsset, parent);

                var bulletEntity = prefabAsset.GetComponent<BulletEntity>();

                bulletEntity.SetBulletType(bulletType);
                bulletEntity.SetMasterEntityId(masterEntityId);
                bulletEntity.Ctor();
                bulletEntity.gameObject.SetActive(true);

                bulletEntity.IDComponent.SetEntityId(bulletEntityId);

                var master = battleFacades.Repo.RoleRepo.Get(masterEntityId);
                bulletEntity.IDComponent.SetLeagueId(master.IDComponent.LeagueId);

                bulletEntity.MoveComponent.SetPosition(startPos);
                bulletEntity.MoveComponent.FaceTo(fireDir);
                bulletEntity.MoveComponent.ActivateMoveVelocity(fireDir);

                battleFacades.Repo.BulletRepo.Add(bulletEntity);

                if (bulletEntity is HookerEntity hookerEntity)
                {
                    hookerEntity.SetMasterGrabPoint(master.transform);
                }

                return bulletEntity;
            }

            return null;
        }

        public List<BulletEntity> Tick_BulletLife(float deltaTime)
        {
            var bulletRepo = battleFacades.Repo.BulletRepo;
            List<BulletEntity> removeList = new List<BulletEntity>();
            bulletRepo.Foreach((bulletEntity) =>
            {
                if (bulletEntity.LifeTime <= 0)
                {
                    removeList.Add(bulletEntity);
                }
                bulletEntity.ReduceLifeTime(deltaTime);
            });

            return removeList;
        }

        public void Tick_BulletMovement(float fixedDeltaTime)
        {
            var bulletRepo = battleFacades.Repo.BulletRepo;
            bulletRepo.Foreach((bullet) =>
            {
                bullet.MoveComponent.Tick_Friction(fixedDeltaTime);
                bullet.MoveComponent.Tick_Gravity(fixedDeltaTime);
                bullet.MoveComponent.Tick_Rigidbody(fixedDeltaTime);
            });
        }

        public List<HitModel> Tick_BulletHitRole(float fixedDeltaTime)
        {
            List<HitModel> attackList = new List<HitModel>();
            var physicsDomain = battleFacades.Domain.PhysicsDomain;
            var bulletRepo = battleFacades.Repo.BulletRepo;

            bulletRepo.Foreach((bullet) =>
            {
                var hitRoleList = physicsDomain.GetHitRole_ColliderList(bullet);
                Transform hookedTrans = null;
                bool hashit = false;

                hitRoleList.ForEach((ce) =>
                {
                    Debug.Log($"打中敌人");

                    if (hookedTrans == null)
                    {
                        hookedTrans = ce.gameObject.transform;
                    }

                    var role = ce.gameObject.GetComponentInParent<BattleRoleLogicEntity>();
                    if (role.HealthComponent.CheckIsDead())
                    {
                        Debug.LogWarning($"TryHitActor 角色已死亡! return");
                        return;
                    }

                    // TODO: 配置表配置 ----------------------------------------------
                    HitPowerModel hitPowerModel = bullet.HitPowerModel;
                    // ----------------------------------------------------------------

                    var hitDomain = battleFacades.Domain.HitDomain;
                    var roleIDC = role.IDComponent;
                    var bulletIDC = bullet.IDComponent;
                    if (!hitDomain.TryHitActor(bulletIDC, roleIDC, in hitPowerModel, fixedDeltaTime))
                    {
                        Debug.LogWarning($"TryHitActor 失败! return");
                        return;
                    }

                    HitModel model = new HitModel();
                    model.attackerIDC = bulletIDC;
                    model.victimIDC = roleIDC;
                    attackList.Add(model);

                    hashit = true;
                });

                if (hashit)
                {
                    ApplyBulletHitEffector(bullet, hookedTrans);
                }
            });

            return attackList;

        }

        public void ApplyBulletHitEffector(BulletEntity bullet, Transform hitTF)
        {
            // 普通子弹
            if (bullet.BulletType == BulletType.DefaultBullet)
            {
                bullet.SetLifeTime(0);
            }
            // 爪钩
            if (bullet is HookerEntity hookerEntity)
            {
                hookerEntity.TryGrabSomthing(hitTF);
            }
            // 手雷
            else if (bullet is GrenadeEntity grenadeEntity)
            {
                var moveComponent = grenadeEntity.MoveComponent;
                moveComponent.SetPersistentMove(false);
                moveComponent.SetMoveVelocity(Vector3.zero);
                moveComponent.SetGravityVelocity(0);
                moveComponent.SetIsGrounded(true);
                moveComponent.SetVelocity(Vector3.zero);

                var tf = grenadeEntity.transform;
                var collider = tf.GetComponent<Collider>();
                collider.enabled = false;
            }
        }

        #region [Grenade]

        public void GrenadeExplode(GrenadeEntity grenadeEntity, float fixedDeltaTime)
        {
            Debug.Log("爆炸");
            var roleRepo = battleFacades.Repo.RoleRepo;
            roleRepo.Foreach((role) =>
            {
                // - 根据距离HitActor
                var dis = Vector3.Distance(role.MoveComponent.Position, grenadeEntity.MoveComponent.Position);
                if (dis < grenadeEntity.ExplosionRadius)
                {
                    var dir = role.MoveComponent.Position - grenadeEntity.MoveComponent.Position;

                    HitPowerModel hitPowerModel = grenadeEntity.HitPowerModel;

                    var hitDomain = battleFacades.Domain.HitDomain;
                    if (hitDomain.TryHitActor(grenadeEntity.IDComponent, role.IDComponent, hitPowerModel, fixedDeltaTime))
                    {
                        var roleDomain = battleFacades.Domain.RoleDomain;
                        roleDomain.RoleTryReceiveDamage(role, hitPowerModel.damage);

                        var hitVelocity = dir.normalized * hitPowerModel.hitVelocityCoefficient + new Vector3(0, 2f, 0);
                        role.MoveComponent.AddExtraVelocity(hitVelocity);
                    }
                }
            });

            grenadeEntity.TearDown();
        }

        #endregion

        #region [Hooker]

        // 获得当前所有已激活的爪钩
        public List<HookerEntity> GetActiveHookerList()
        {
            List<HookerEntity> hookerEntities = new List<HookerEntity>();
            var bulletRepo = battleFacades.Repo.BulletRepo;
            bulletRepo.Foreach((bullet) =>
            {
                if (bullet is HookerEntity hookerEntity && hookerEntity.GrabPoint != null)
                {
                    hookerEntities.Add(hookerEntity);
                }
            });

            return hookerEntities;
        }

        public void Tick_ActiveHookerDraging(float fixedDeltaTime)
        {
            var activeHookers = battleFacades.Domain.BulletDomain.GetActiveHookerList();
            var rqs = battleFacades.Network.RoleReqAndRes;
            activeHookers.ForEach((hooker) =>
            {
                var master = battleFacades.Repo.RoleRepo.Get(hooker.MasterEntityId);
                if (!hooker.TickHooker(out float force))
                {
                    master.StateComponent.SetRoleState(RoleState.Normal);
                    return;
                }

                var masterMC = master.MoveComponent;
                var hookerEntityMC = hooker.MoveComponent;
                var dir = hookerEntityMC.Position - masterMC.Position;
                var dis = Vector3.Distance(hookerEntityMC.Position, masterMC.Position);
                dir.Normalize();
                var v = dir * force * fixedDeltaTime;
                masterMC.AddExtraVelocity(v);
            });
        }

        #endregion

    }


}