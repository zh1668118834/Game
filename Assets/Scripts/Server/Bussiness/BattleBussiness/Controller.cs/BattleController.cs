using System.Collections.Generic;
using UnityEngine;
using Game.Protocol.Battle;
using Game.Infrastructure.Generic;
using Game.Client.Bussiness.BattleBussiness;
using Game.Server.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;
using Game.Server.Bussiness.EventCenter;
using Game.Client.Bussiness;

namespace Game.Server.Bussiness.BattleBussiness
{

    public class BattleController
    {
        BattleServerFacades battleServerFacades;
        float fixedDeltaTime;  //0.03f

        // Scene Spawn Trigger
        bool hasBattleBegin;
        bool hasSpawnBegin;

        // NetWork Info
        public int ServeFrame => battleServerFacades.Network.ServeFrame;
        List<int> ConnIDList => battleServerFacades.Network.connIdList;

        // ====== 角色 ======
        // - 所有生成帧
        Dictionary<long, FrameBattleRoleSpawnReqMsg> roleSpawnMsgDic;
        // -所有操作帧
        Dictionary<long, FrameRoleMoveReqMsg> roleMoveMsgDic;
        Dictionary<long, FrameRoleRotateReqMsg> roleRotateMsgDic;
        // - 所有跳跃帧
        Dictionary<long, FrameRollReqMsg> rollOptMsgDic;

        // ====== 子弹 ======
        // - 所有拾取物件帧
        Dictionary<long, FrameItemPickReqMsg> itemPickUpMsgDic;

        public BattleController()
        {
            ServerNetworkEventCenter.battleSerConnect += ((connId) =>
            {
                ConnIDList.Add(connId); //添加至连接名单
                hasBattleBegin = true;
            });

            roleSpawnMsgDic = new Dictionary<long, FrameBattleRoleSpawnReqMsg>();
            roleMoveMsgDic = new Dictionary<long, FrameRoleMoveReqMsg>();
            roleRotateMsgDic = new Dictionary<long, FrameRoleRotateReqMsg>();
            rollOptMsgDic = new Dictionary<long, FrameRollReqMsg>();

            itemPickUpMsgDic = new Dictionary<long, FrameItemPickReqMsg>();
        }

        public void Inject(BattleServerFacades battleFacades, float fixedDeltaTime)
        {
            this.battleServerFacades = battleFacades;
            this.fixedDeltaTime = fixedDeltaTime;

            var battleRqs = battleFacades.Network.BattleReqAndRes;

            var roleRqs = battleFacades.Network.RoleReqAndRes;
            roleRqs.RegistReq_RoleMove(OnRoleMove);
            roleRqs.RegistReq_RoleRotate(OnRoleRotate);

            roleRqs.RegistReq_Jump(OnRoleJump);
            roleRqs.RegistReq_BattleRoleSpawn(OnRoleSpawn);

            var itemRqs = battleFacades.Network.ItemReqAndRes;
            itemRqs.RegistReq_ItemPickUp(OnItemPickUp);

        }

        public void Tick()
        {
            if (!hasBattleBegin)
            {
                return;
            }

            if (hasBattleBegin)
            {
                SpawBattleScene();
            }

            var CurFieldEntity = battleServerFacades.BattleFacades.Repo.FiledRepo.CurFieldEntity;
            if (CurFieldEntity == null)
            {
                return;
            }
            // ====== Bullet
            Tick_BulletLifeCycle();

            // ====== Role
            Tick_RoleSpawn();
            Tick_RoleRollOpt();
            Tick_RoleMoveRotateOpt();
            ApplyAllRoleState();

            // ====== Item
            Tick_ItemPickUp();

            // ====== Broadcast
            BroadcastAllRoleState();
        }

        #region [Tick Request]

        #region [Role]

        void BroadcastAllRoleState()
        {
            var roleRqs = battleServerFacades.Network.RoleReqAndRes;
            var roleRepo = battleServerFacades.BattleFacades.Repo.RoleRepo;
            ConnIDList.ForEach((connID) =>
            {
                roleRepo.Foreach((role) =>
                {
                    roleRqs.SendUpdate_RoleState(connID, role);
                });
            });
        }

        void Tick_RoleSpawn()
        {
            ConnIDList.ForEach((connId) =>
            {
                long key = GetCurFrameKey(connId);

                if (roleSpawnMsgDic.TryGetValue(key, out var msg))
                {
                    if (msg == null)
                    {
                        return;
                    }

                    roleSpawnMsgDic[key] = null;

                    var battleFacades = battleServerFacades.BattleFacades;
                    var weaponRepo = battleFacades.Repo.WeaponRepo;
                    var roleRepo = battleFacades.Repo.RoleRepo;
                    var entityId = roleRepo.Size;

                    // 服务器逻辑
                    var roleEntity = battleFacades.Domain.RoleDomain.SpawnRoleLogic(entityId);
                    roleEntity.SetConnId(connId);
                    Debug.Log($"服务器逻辑[生成角色] serveFrame:{ServeFrame} wRid:{entityId} 位置:{roleEntity.MoveComponent.Position}");

                    var itemRqs = battleServerFacades.Network.ItemReqAndRes;
                    itemRqs.SendRes_ItemSpawn(connId, ServeFrame, battleServerFacades.ItemTypeByteList, battleServerFacades.SubTypeList, battleServerFacades.EntityIDList);
                }
            });
        }

        void Tick_RoleMoveRotateOpt()
        {
            ConnIDList.ForEach((connId) =>
            {
                long key = GetCurFrameKey(connId);

                var roleRepo = battleServerFacades.BattleFacades.Repo.RoleRepo;
                var rqs = battleServerFacades.Network.RoleReqAndRes;

                if (roleMoveMsgDic.TryGetValue(key, out var msg))
                {

                    var realMsg = msg.msg;

                    var rid = (byte)(realMsg >> 48);
                    var role = roleRepo.Get(rid);

                    // ------------移动 -> Input
                    Vector3 dir = new Vector3((short)(ushort)(realMsg >> 32) / 100f, (short)(ushort)(realMsg >> 16) / 100f, (short)(ushort)realMsg / 100f);
                    role.InputComponent.SetMoveDir(dir);
                }

                if (roleRotateMsgDic.TryGetValue(key, out var rotateMsg))
                {
                    // ------------转向 -> Input（基于客户端鉴权的同步） 
                    var realMsg = rotateMsg.msg;
                    var rid = (byte)(realMsg >> 48);
                    var role = roleRepo.Get(rid);
                    Vector3 eulerAngle = new Vector3((short)(realMsg >> 32), (short)(realMsg >> 16), (short)realMsg);
                    role.InputComponent.SetFaceDir(eulerAngle);
                }

            });

        }

        void Tick_RoleRollOpt()
        {
            ConnIDList.ForEach((connId) =>
            {
                long key = GetCurFrameKey(connId);

                if (rollOptMsgDic.TryGetValue(key, out var msg))
                {
                    if (msg == null)
                    {
                        return;
                    }

                    rollOptMsgDic[key] = null;

                    var wRid = msg.entityId;
                    var roleRepo = battleServerFacades.BattleFacades.Repo.RoleRepo;
                    var role = roleRepo.Get(wRid);
                    var rqs = battleServerFacades.Network.RoleReqAndRes;
                    Vector3 dir = new Vector3(msg.dirX / 10000f, msg.dirY / 10000f, msg.dirZ / 10000f);
                    role.InputComponent.SetRollDir(dir);
                }
            });

        }

        void ApplyAllRoleState()
        {
            var roleStateDomain = battleServerFacades.BattleFacades.Domain.RoleStateDomain;
            roleStateDomain.ApplyAllRoleState();
        }

        #endregion

        #region [Bullet]

        void Tick_BulletLifeCycle()
        {
            Tick_DeadLifeBulletTearDown();
            Tick_BulletHitRole();
            Tick_BulletHitField();
            Tick_ActiveHookerDraging();
        }

        void Tick_DeadLifeBulletTearDown()
        {
            var bulletDomain = battleServerFacades.BattleFacades.Domain.BulletDomain;
            var tearDownList = bulletDomain.Tick_BulletLife(NetworkConfig.FIXED_DELTA_TIME);

            tearDownList.ForEach((bulletEntity) =>
            {

                var bulletType = bulletEntity.BulletType;

                if (bulletType == BulletType.DefaultBullet)
                {
                    bulletEntity.TearDown();
                }

                if (bulletEntity is GrenadeEntity grenadeEntity)
                {
                    var bulletDomain = battleServerFacades.BattleFacades.Domain.BulletDomain;
                    bulletDomain.GrenadeExplode(grenadeEntity, fixedDeltaTime);
                }

                if (bulletEntity is HookerEntity hookerEntity)
                {
                    hookerEntity.TearDown();
                }

                var bulletRepo = battleServerFacades.BattleFacades.Repo.BulletRepo;
                bulletRepo.TryRemove(bulletEntity);

                var bulletRqs = battleServerFacades.Network.BulletReqAndRes;
                ConnIDList.ForEach((connId) =>
                {
                    // 广播子弹销毁消息
                    bulletRqs.SendRes_BulletLifeFrameOver(connId, bulletEntity);
                });

            });
        }

        void Tick_BulletHitField()
        {
            var physicsDomain = battleServerFacades.BattleFacades.Domain.PhysicsDomain;
            var hitFieldList = physicsDomain.Tick_BulletHitField();

            Transform field = null;
            var bulletRepo = battleServerFacades.BattleFacades.Repo.BulletRepo;
            var bulletRqs = battleServerFacades.Network.BulletReqAndRes;
            hitFieldList.ForEach((hitFieldModel) =>
            {
                var bulletIDC = hitFieldModel.hitter;
                var bullet = bulletRepo.Get(bulletIDC.EntityID);

                ConnIDList.ForEach((connId) =>
                {
                    bulletRqs.SendRes_BulletHitField(connId, bullet);
                });
                field = hitFieldModel.fieldCE.GetCollider().transform;
            });
        }

        void Tick_BulletHitRole()
        {
            var bulletDomain = battleServerFacades.BattleFacades.Domain.BulletDomain;
            var bulletRqs = battleServerFacades.Network.BulletReqAndRes;
            var hitRoleList = bulletDomain.Tick_BulletHitRole(fixedDeltaTime);

            ConnIDList.ForEach((connId) =>
            {
                hitRoleList.ForEach((attackModel) =>
                {
                    bulletRqs.SendRes_BulletHitRole(connId, attackModel.attackerIDC.EntityID, attackModel.victimIDC.EntityID);
                });
            });
        }

        void Tick_ActiveHookerDraging()
        {
            var bulletDomain = battleServerFacades.BattleFacades.Domain.BulletDomain;
            bulletDomain.Tick_ActiveHookerDraging(fixedDeltaTime);
        }

        #endregion

        #region [Item]

        void Tick_ItemPickUp()
        {

            ConnIDList.ForEach((connId) =>
            {
                long key = GetCurFrameKey(connId);

                if (itemPickUpMsgDic.TryGetValue(key, out var msg))
                {
                    if (msg == null)
                    {
                        return;
                    }

                    itemPickUpMsgDic[key] = null;

                    // TODO:Add judgement like 'Can He Pick It Up?'
                    EntityType entityType = (EntityType)msg.entityType;

                    var itemDomain = battleServerFacades.BattleFacades.Domain.ItemDomain;
                    if (itemDomain.TryPickUpItem(entityType, msg.entityId, msg.entityID))
                    {
                        var rqs = battleServerFacades.Network.ItemReqAndRes;
                        ConnIDList.ForEach((connId) =>
                        {
                            rqs.SendRes_ItemPickUp(connId, ServeFrame, msg.entityID, entityType, msg.entityId);
                        });
                    }
                    else
                    {
                        Debug.Log($"{entityType.ToString()}物品拾取失败");
                    }
                }
            });

        }

        #endregion

        #endregion

        #region [Network]

        #region [Role]

        void OnRoleMove(int connId, FrameRoleMoveReqMsg msg)
        {
            lock (roleMoveMsgDic)
            {
                long key = GetCurFrameKey(connId);

                if (!roleMoveMsgDic.TryGetValue(key, out var opt))
                {
                    roleMoveMsgDic[key] = msg;
                }

            }
        }

        void OnRoleJump(int connId, FrameRollReqMsg msg)
        {
            lock (rollOptMsgDic)
            {
                long key = GetCurFrameKey(connId);

                if (!rollOptMsgDic.TryGetValue(key, out var opt))
                {
                    rollOptMsgDic[key] = msg;
                }

            }
        }

        void OnRoleRotate(int connId, FrameRoleRotateReqMsg msg)
        {
            lock (roleRotateMsgDic)
            {
                long key = GetCurFrameKey(connId);

                if (!roleRotateMsgDic.TryGetValue(key, out var opt))
                {
                    roleRotateMsgDic[key] = msg;
                }
            }
        }

        void OnRoleSpawn(int connId, FrameBattleRoleSpawnReqMsg msg)
        {
            lock (roleSpawnMsgDic)
            {
                long key = GetCurFrameKey(connId);

                Debug.Log($"[战斗Controller] 战斗角色生成请求 key:{key}");
                if (!roleSpawnMsgDic.TryGetValue(key, out var _))
                {
                    roleSpawnMsgDic[key] = msg;
                }
            }
        }
        #endregion

        #region [Item]

        void OnItemPickUp(int connId, FrameItemPickReqMsg msg)
        {
            lock (itemPickUpMsgDic)
            {
                long key = GetCurFrameKey(connId);

                if (!itemPickUpMsgDic.TryGetValue(key, out var _))
                {
                    itemPickUpMsgDic[key] = msg;
                }
            }
        }
        #endregion

        #region [Scene Spawn Method]

        async void SpawBattleScene()
        {
            if (hasSpawnBegin)
            {
                return;
            }
            hasSpawnBegin = true;

            // Load Scene And Spawn Field
            var domain = battleServerFacades.BattleFacades.Domain;
            var fieldEntity = await domain.SceneDomain.SpawnGameFightScene();
            fieldEntity.SetEntityId(1);
            var fieldEntityRepo = battleServerFacades.BattleFacades.Repo.FiledRepo;
            fieldEntityRepo.Add(fieldEntity);
            fieldEntityRepo.SetPhysicsScene(fieldEntity.gameObject.scene.GetPhysicsScene());

            // 生成资源
            GenerateRandomAssetData(fieldEntity, out var assetPointEntities);
            InitAllAssetRepo(assetPointEntities);
        }

        void InitAllAssetRepo(AssetPointEntity[] assetPointEntities)
        {
            var entityTypeList = battleServerFacades.EntityTypeList;
            var entityIDList = battleServerFacades.EntityIDList;
            var itemTypeByteList = battleServerFacades.ItemTypeByteList;
            var subTypeList = battleServerFacades.SubTypeList;

            int count = entityTypeList.Count;
            Debug.Log($"物件资源开始生成[数量:{count}]----------------------------------------------------");

            var battleFacades = battleServerFacades.BattleFacades;
            for (int i = 0; i < entityTypeList.Count; i++)
            {
                var entityType = entityTypeList[i];
                var subtype = subTypeList[i];
                var parent = assetPointEntities[i];

                // - 获取资源ID
                var idService = battleFacades.IDService;
                var entityID = idService.GetAutoIDByEntityType(entityType);

                // - 生成资源
                var itemDomain = battleFacades.Domain.ItemDomain;
                itemDomain.SpawnItem(entityType, subtype, entityID, parent.transform);

                // - 记录
                entityIDList.Add(entityID);
                itemTypeByteList.Add((byte)entityType);
            }

            Debug.Log($"物件资源生成完毕******************************************************");
        }

        void GenerateRandomAssetData(FieldEntity fieldEntity, out AssetPointEntity[] assetPointEntities)
        {
            assetPointEntities = fieldEntity.transform.GetComponentsInChildren<AssetPointEntity>();
            for (int i = 0; i < assetPointEntities.Length; i++)
            {
                var assetPoint = assetPointEntities[i];
                AssetGenProbability[] itemGenProbabilities = assetPoint.itemGenProbabilityArray;
                float totalWeight = 0;
                for (int j = 0; j < itemGenProbabilities.Length; j++) totalWeight += itemGenProbabilities[j].weight;
                float lRange = 0;
                float rRange = 0;
                float randomNumber = Random.Range(0f, 1f);
                for (int j = 0; j < itemGenProbabilities.Length; j++)
                {
                    AssetGenProbability igp = itemGenProbabilities[j];
                    if (igp.weight <= 0) continue;
                    rRange = lRange + igp.weight / totalWeight;
                    if (randomNumber >= lRange && randomNumber < rRange)
                    {
                        battleServerFacades.EntityTypeList.Add(igp.entityType);
                        battleServerFacades.SubTypeList.Add(igp.subType);
                        break;
                    }
                    lRange = rRange;
                }
            }
        }
        #endregion

        #endregion

        #region [Private Func]

        long GetCurFrameKey(int connId)
        {
            long key = (long)ServeFrame << 32;
            key |= (long)connId;
            return key;
        }

        #endregion

    }


}