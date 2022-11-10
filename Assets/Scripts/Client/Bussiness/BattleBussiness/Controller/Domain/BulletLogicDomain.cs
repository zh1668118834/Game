using System.Collections.Generic;
using UnityEngine;
using Game.Client.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class BulletLogicDomain
    {

        BattleFacades battleFacades;

        public BulletLogicDomain()
        {
        }

        public void Inject(BattleFacades facades)
        {
            this.battleFacades = facades;
        }

        public BulletEntity SpawnLogic(BulletType bulletType, int bulletEntityID, Vector3 pos)
        {
            string prefabName = bulletType.ToString() + "_Logic";
            if (!battleFacades.Assets.BulletAsset.TryGetByName(prefabName, out GameObject go))
            {
                Debug.LogError($"{prefabName} Spawn Failed!");
                return null;
            }

            var repo = battleFacades.Repo;
            var parent = repo.FieldRepo.CurFieldEntity.transform;
            go = GameObject.Instantiate(go, parent);

            var entity = go.GetComponent<BulletEntity>();
            entity.Ctor();
            entity.gameObject.SetActive(true);
            entity.SetBulletType(bulletType);
            entity.SetEntityID(bulletEntityID);
            entity.SetPosition(pos);

            repo.BulletLogicRepo.Add(entity);

            return entity;

        }

        public void ShootByWeapon(BulletEntity bulletEntity, int weaponEntityID, Vector3 fireDir)
        {
            var repo = battleFacades.Repo;
            bulletEntity.SetWeaponID(weaponEntityID);
            var weapon = repo.WeaponRepo.Get(weaponEntityID);
            bulletEntity.SetLeagueID(weapon.IDComponent.LeagueId);
            bulletEntity.FaceTo(fireDir);
            bulletEntity.LocomotionComponent.ApplyMoveVelocity(fireDir);
            if (bulletEntity is HookerEntity hookerEntity)
            {
                hookerEntity.SetMasterGrabPoint(weapon.transform);
            }
        }

        public void TearDown(BulletEntity bullet)
        {
            if (bullet == null)
            {
                return;
            }

            if (bullet is GrenadeEntity grenadeEntity)
            {
                var bulletDomain = battleFacades.Domain.BulletLogicDomain;
                bulletDomain.GrenadeExplodeTearDown(grenadeEntity);
            }
            else
            {
                bullet.TearDown();
            }

            battleFacades.Repo.BulletLogicRepo.Remove(bullet);
        }

        public void TearDown(int entityID)
        {
            var repo = battleFacades.Repo;
            var bulletLogicRepo = repo.BulletLogicRepo;
            var bulletLogic = bulletLogicRepo.Get(entityID);
            TearDown(bulletLogic);
        }

        public void LifeOver(BulletEntity bullet)
        {
            if (bullet == null)
            {
                return;
            }

            var bulletType = bullet.BulletType;
            if (bulletType == BulletType.DefaultBullet)
            {
                bullet.TearDown();
            }
            else if (bullet is GrenadeEntity grenadeEntity)
            {
                GrenadeExplodeTearDown(grenadeEntity);
            }
            else if (bullet is HookerEntity hookerEntity)
            {
                hookerEntity.TearDown();
            }

            var bulletRepo = battleFacades.Repo.BulletLogicRepo;
            bulletRepo.TryRemove(bullet);
            Debug.Log($"Bullet LifeOver: {bullet.IDComponent.EntityID}");
        }

        public void LifeOver(int bulletID)
        {
            var bullet = battleFacades.Repo.BulletLogicRepo.Get(bulletID);
            LifeOver(bullet);
        }

        public void Tick_Physics_All(float fixedDeltaTime)
        {
            // - Normal Physics
            var bulletRepo = battleFacades.Repo.BulletLogicRepo;
            bulletRepo.Foreach((bullet) =>
            {
                bullet.LocomotionComponent.Tick_AllPhysics(fixedDeltaTime);
            });

            // - Hooker Physics
            Tick_Physics_AllHookers(fixedDeltaTime);
        }

        public List<BulletEntity> Tick_LifeTime_All(float deltaTime)
        {
            var bulletRepo = battleFacades.Repo.BulletLogicRepo;
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

        public List<HitModel> Tick_HitModels_All(EntityType hitEntityType, float fixedDeltaTime)
        {
            List<HitModel> hitModelList = new List<HitModel>();
            var physicsDomain = battleFacades.Domain.PhysicsDomain;
            var bulletRepo = battleFacades.Repo.BulletLogicRepo;

            bulletRepo.ForAll((bullet) =>
            {
                List<CollisionExtra> hitCEList = null;
                if (hitEntityType == EntityType.BattleRole)
                {
                    hitCEList = physicsDomain.GetHitRole_ColliderList(bullet);
                }
                else if (hitEntityType == EntityType.Aridrop)
                {
                    hitCEList = physicsDomain.GetHitAirdrop_ColliderList(bullet);
                }

                Transform hookedTrans = null;
                bool hashit = false;

                hitCEList.ForEach((ce) =>
                {
                    if (bullet.BulletType == BulletType.Grenade)
                    {
                        // - Grenade Ignore
                        return;
                    }

                    if (hookedTrans == null)
                    {
                        hookedTrans = ce.gameObject.transform;
                    }

                    IDComponent victimIDC = null;
                    if (hitEntityType == EntityType.BattleRole)
                    {
                        var role = ce.gameObject.GetComponentInParent<BattleRoleLogicEntity>();
                        victimIDC = role.IDComponent;
                    }
                    else if (hitEntityType == EntityType.Aridrop)
                    {
                        var airdrop = ce.gameObject.GetComponentInParent<BattleAirdropEntity>();
                        victimIDC = airdrop.IDComponent;
                    }
                    else
                    {
                        Debug.LogError("未处理情况");
                    }

                    var hitDomain = battleFacades.Domain.HitDomain;
                    var bulletIDC = bullet.IDComponent;
                    HitPowerModel hitPowerModel = bullet.HitPowerModel;
                    if (!hitDomain.TryHitActor(bulletIDC, victimIDC, in hitPowerModel))
                    {
                        return;
                    }

                    HitModel model = new HitModel();
                    model.attackerIDC = bulletIDC;
                    model.victimIDC = victimIDC;
                    hitModelList.Add(model);
                    hashit = true;
                });

                if (hashit)
                {
                    ApplyEffector_BulletHitField(bullet, hookedTrans);
                }
            });

            return hitModelList;
        }

        public void ApplyEffector_BulletHitField(BulletEntity bullet, Transform hitTF)
        {
            // 普通子弹
            if (bullet.BulletType == BulletType.DefaultBullet)
            {
                TearDown(bullet);
            }
            // 爪钩
            if (bullet is HookerEntity hookerEntity)
            {
                hookerEntity.TryGrabSomthing(hitTF);
            }
            // 手雷
            else if (bullet is GrenadeEntity grenadeEntity)
            {
                var moveComponent = grenadeEntity.LocomotionComponent;
                moveComponent.SetStatic();

                var tf = grenadeEntity.transform;
                var collider = tf.GetComponent<Collider>();
                collider.enabled = false;
            }
        }

        #region [Grenade]

        public void GrenadeExplodeTearDown(GrenadeEntity grenadeEntity)
        {
            Debug.Log("爆炸");
            grenadeEntity.isExploded = true;

            var roleRepo = battleFacades.Repo.RoleLogicRepo;
            roleRepo.Foreach((role) =>
            {
                // - 根据距离HitActor
                var dis = Vector3.Distance(role.LocomotionComponent.Position, grenadeEntity.LocomotionComponent.Position);
                if (dis < grenadeEntity.ExplosionRadius)
                {
                    HitPowerModel hitPowerModel = grenadeEntity.HitPowerModel;

                    var hitDomain = battleFacades.Domain.HitDomain;
                    if (hitDomain.TryHitActor(grenadeEntity.IDComponent, role.IDComponent, hitPowerModel))
                    {
                        var roleDomain = battleFacades.Domain.RoleLogicDomain;
                        roleDomain.TryReceiveDamage(role, hitPowerModel.damage);
                        if (role.HealthComponent.CheckIsDead())
                        {
                            roleDomain.RoleState_EnterDead(role);
                        }
                    }
                }
            });

            grenadeEntity.TearDown();
        }

        #endregion

        #region [Hooker]

        void Tick_Physics_AllHookers(float fixedDeltaTime)
        {
            var activeHookers = battleFacades.Domain.BulletLogicDomain.GetActivatedHookerList();
            var rqs = battleFacades.Network.RoleReqAndRes;
            activeHookers.ForEach((hooker) =>
            {
                var master = battleFacades.Repo.RoleLogicRepo.Get(hooker.WeaponID);
                if (!hooker.TickHooker(out float force))
                {
                    master.StateComponent.SetRoleState(RoleState.Normal);
                    return;
                }

                var masterMC = master.LocomotionComponent;
                var hookerEntityMC = hooker.LocomotionComponent;
                var dir = hookerEntityMC.Position - masterMC.Position;
                var dis = Vector3.Distance(hookerEntityMC.Position, masterMC.Position);
                dir.Normalize();
                var v = dir * force * fixedDeltaTime;
                masterMC.AddExtraVelocity(v);
            });
        }

        List<HookerEntity> GetActivatedHookerList()
        {
            List<HookerEntity> hookerEntities = new List<HookerEntity>();
            var bulletRepo = battleFacades.Repo.BulletLogicRepo;
            bulletRepo.Foreach((bullet) =>
            {
                if (bullet is HookerEntity hookerEntity && hookerEntity.GrabPoint != null)
                {
                    hookerEntities.Add(hookerEntity);
                }
            });

            return hookerEntities;
        }


        #endregion

    }


}