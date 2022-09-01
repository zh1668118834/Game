using System.Collections.Generic;
using UnityEngine;
using Game.Server.Bussiness.WorldBussiness.Facades;
using Game.Protocol.World;
using Game.Infrastructure.Network;
using Game.Client.Bussiness.WorldBussiness;
using Game.Infrastructure.Generic;

namespace Game.Server.Bussiness.WorldBussiness
{

    public class WorldController
    {
        WorldFacades worldFacades;
        int worldServeFrame;
        float fixedDeltaTime = 0.02f;

        // 记录当前所有ConnId
        List<int> connIdList;

        // 记录所有操作帧
        struct FrameReqOptMsgStruct
        {
            public int connId;
            public FrameOptReqMsg msg;
        }
        Dictionary<int, Queue<FrameReqOptMsgStruct>> wRoleOptQueueDic;

        // 移动记录所有跳跃帧
        struct FrameReqJumpMsgStruct
        {
            public int connId;
            public FrameJumpReqMsg msg;
        }
        Dictionary<int, FrameReqJumpMsgStruct> jumpOptDic;//TODO: --> Queue

        // 记录所有生成帧
        struct FrameReqWRoleSpawnMsgStruct
        {
            public int connId;
            public FrameWRoleSpawnReqMsg msg;
        }
        Dictionary<int, FrameReqWRoleSpawnMsgStruct> wRoleSpawnDic;//TODO: --> Queue

        // 记录所有子弹生成帧
        struct FrameReqBulletSpawnMsgStruct
        {
            public int connId;
            public FrameBulletSpawnReqMsg msg;
        }
        Dictionary<int, FrameReqBulletSpawnMsgStruct> bulletSpawnDic;   //TODO: --> Queue

        bool sceneSpawnTrigger;
        bool isSceneSpawn;

        public WorldController()
        {
            connIdList = new List<int>();
            wRoleOptQueueDic = new Dictionary<int, Queue<FrameReqOptMsgStruct>>();
            jumpOptDic = new Dictionary<int, FrameReqJumpMsgStruct>();
            wRoleSpawnDic = new Dictionary<int, FrameReqWRoleSpawnMsgStruct>();
            bulletSpawnDic = new Dictionary<int, FrameReqBulletSpawnMsgStruct>();
        }

        public void Inject(WorldFacades worldFacades)
        {
            this.worldFacades = worldFacades;

            var roleRqs = worldFacades.Network.WorldRoleReqAndRes;
            roleRqs.RegistReq_WorldRoleMove(OnWoldRoleMove);
            roleRqs.RegistReq_Jump(OnWoldRoleJump);
            roleRqs.RegistReq_WolrdRoleSpawn(OnWoldRoleSpawn);

            var bulletRqs = worldFacades.Network.BulletReqAndRes;
            bulletRqs.RegistReq_BulletSpawn(OnBulletSpawn);
        }

        public void Tick()
        {
            // Tick的过滤条件
            if (sceneSpawnTrigger && !isSceneSpawn)
            {
                SpawWorldChooseScene();
                sceneSpawnTrigger = false;
            }
            if (!isSceneSpawn) return;

            // CLIENT REQUEST
            Tick_WRoleSpawn();
            Tick_BulletSpawn();

            Tick_JumpOpt();
            Tick_Opt();

            // Tick_RoleStateSync();
            Tick_BulletLife();

            // Physics Simulation
            if (worldFacades.ClientWorldFacades.Repo.FiledEntityRepo.CurFieldEntity == null) return;
            Tick_BulletHitRole();
            Tick_RoleMovement();
            Tick_BulletMovement();
            var physicsScene = worldFacades.ClientWorldFacades.Repo.FiledEntityRepo.CurPhysicsScene;
            physicsScene.Simulate(fixedDeltaTime);
        }

        #region [Client Requst]
        // ====== ROLE
        void Tick_WRoleSpawn()
        {
            int nextFrameIndex = worldServeFrame + 1;
            if (wRoleSpawnDic.TryGetValue(nextFrameIndex, out var spawn))
            {
                worldServeFrame = nextFrameIndex;

                var msg = spawn.msg;
                var connId = spawn.connId;
                var clientFrameIndex = msg.clientFrameIndex;

                var clientFacades = worldFacades.ClientWorldFacades;
                var repo = clientFacades.Repo;
                var fieldEntity = repo.FiledEntityRepo.Get(1);
                var rqs = worldFacades.Network.WorldRoleReqAndRes;
                var roleRepo = repo.WorldRoleRepo;
                var wrid = roleRepo.Size;

                // 服务器逻辑
                var roleEntity = clientFacades.Domain.WorldRoleSpawnDomain.SpawnWorldRole(fieldEntity.transform);
                roleEntity.SetWRid(wrid);
                roleEntity.SetConnId(connId);
                Debug.Log($"服务器逻辑[Spawn Role] frame:{worldServeFrame} wRid:{wrid}  roleEntity.MoveComponent.CurPos:{roleEntity.MoveComponent.CurPos}");

                if (clientFrameIndex + 1 < worldServeFrame)
                {

                    // ====== 补发数据包
                    // Debug.Log("所有人物操作包数据========================================");
                    // for (int frameIndex = clientFrameIndex + 1; frameIndex < worldServeFrameIndex; frameIndex++)
                    // {
                    //     if (!optDic.TryGetValue(frameIndex, out var optMsgStruct)) continue;

                    //     rqs.ResendRes_WorldRoleMove(connId, frameIndex, optMsgStruct.msg);
                    // }

                    // ====== 发送其他角色的状态同步帧给请求者
                    var allEntity = roleRepo.GetAll();
                    for (int i = 0; i < allEntity.Length; i++)
                    {
                        var otherRole = allEntity[i];
                        rqs.SendUpdate_WRoleState(connId, nextFrameIndex, otherRole, otherRole.RoleState, otherRole.transform.position, otherRole.transform.rotation, otherRole.MoveComponent.Velocity);
                    }

                    // ====== 广播请求者创建的角色给其他人
                    connIdList.ForEach((otherConnId) =>
                    {
                        if (otherConnId != connId)
                        {
                            rqs.SendUpdate_WRoleState(otherConnId, nextFrameIndex, roleEntity, roleEntity.RoleState, roleEntity.transform.position, roleEntity.transform.rotation, roleEntity.MoveComponent.Velocity);
                        }
                    });

                    // ====== 回复请求者创建的角色
                    rqs.SendUpdate_WRoleState(connId, nextFrameIndex, roleEntity, roleEntity.RoleState, roleEntity.transform.position, roleEntity.transform.rotation, roleEntity.MoveComponent.Velocity);

                }
                else
                {
                    Debug.Log($"服务端回复消息[生成帧] {nextFrameIndex}--------------------------------------------------------------------------");
                    rqs.SendRes_WorldRoleSpawn(connId, nextFrameIndex, wrid, true);
                }

                roleRepo.Add(roleEntity);
            }
        }

        // void Tick_RoleStateSync()
        // {
        //     int nextFrameIndex = worldServeFrame + 1;
        //     //人物静止和运动 2个状态
        //     bool isNextFrame = false;
        //     var WorldRoleRepo = worldFacades.ClientWorldFacades.Repo.WorldRoleRepo;
        //     WorldRoleRepo.Foreach((roleEntity) =>
        //     {
        //         if (roleEntity.IsStateChange())
        //         {
        //             isNextFrame = true;
        //             roleEntity.UpdateRoleStatus();

        //             var rqs = worldFacades.Network.WorldRoleReqAndRes;
        //             connIdList.ForEach((connId) =>
        //             {
        //                 rqs.SendUpdate_WRoleState(connId, nextFrameIndex, roleEntity, roleEntity.RoleState, roleEntity.MoveComponent.LastSyncFramePos, roleEntity.transform.rotation, roleEntity.MoveComponent.Velocity);
        //             });
        //         }
        //     });

        //     if (isNextFrame)
        //     {
        //         worldServeFrame = nextFrameIndex;
        //     }
        // }

        void Tick_Opt()
        {
            int nextFrameIndex = worldServeFrame + 1;
            if (!wRoleOptQueueDic.TryGetValue(nextFrameIndex, out var optQueue)) return;
            worldServeFrame = nextFrameIndex;

            while (optQueue.TryDequeue(out var opt)) //保证操作连续性的情况下
            {
                var msg = opt.msg;
                var realMsg = msg.msg;
                var connId = opt.connId;

                var rid = (byte)(realMsg >> 24);
                var roleRepo = worldFacades.ClientWorldFacades.Repo.WorldRoleRepo;
                var roleEntity = roleRepo.Get(rid);
                var optTypeId = opt.msg.optTypeId;
                var rqs = worldFacades.Network.WorldRoleReqAndRes;
                if (optTypeId == 1)
                {
                    Vector3 dir = new Vector3((sbyte)(realMsg >> 16), (sbyte)(realMsg >> 8), (sbyte)realMsg);
                    // 人物状态同步
                    roleEntity.SetRoleStatus(RoleState.Move);
                    //发送状态同步帧
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendUpdate_WRoleState(connId, nextFrameIndex, roleEntity, RoleState.Move, roleEntity.transform.position, roleEntity.transform.rotation, roleEntity.MoveComponent.Velocity);
                    });

                    //服务器逻辑Move + 物理模拟
                    var curPhysicsScene = worldFacades.ClientWorldFacades.Repo.FiledEntityRepo.CurPhysicsScene;
                    roleEntity.MoveComponent.SetFrameMoveDir(dir);
                    roleEntity.MoveComponent.FaceTo(dir);
                    roleEntity.MoveComponent.Tick(fixedDeltaTime);
                    curPhysicsScene.Simulate(fixedDeltaTime);
                    Debug.Log($"Move物理模拟 fixedDeltaTime:{fixedDeltaTime} ");
                    roleEntity.MoveComponent.SetFrameMoveDir(Vector3.zero);
                }
            }
        }

        void Tick_JumpOpt()
        {
            int nextFrameIndex = worldServeFrame + 1;
            if (jumpOptDic.TryGetValue(nextFrameIndex, out var jumpOpt))
            {
                worldServeFrame = nextFrameIndex;

                var wRid = jumpOpt.msg.wRid;
                var roleRepo = worldFacades.ClientWorldFacades.Repo.WorldRoleRepo;
                var roleEntity = roleRepo.Get(wRid);
                var rqs = worldFacades.Network.WorldRoleReqAndRes;

                //服务器逻辑Jump
                roleEntity.MoveComponent.Jump();
                roleEntity.SetRoleStatus(RoleState.Jump);

                //发送状态同步帧
                connIdList.ForEach((connId) =>
                {
                    rqs.SendUpdate_WRoleState(connId, nextFrameIndex, roleEntity, RoleState.Jump, roleEntity.transform.position, roleEntity.transform.rotation, roleEntity.MoveComponent.Velocity);
                });
            }
        }

        // ====== Bullet
        void Tick_BulletSpawn()
        {
            int nextFrameIndex = worldServeFrame + 1;
            if (bulletSpawnDic.TryGetValue(nextFrameIndex, out var bulletSpawn))
            {
                worldServeFrame = nextFrameIndex;

                int connId = bulletSpawn.connId;
                var msg = bulletSpawn.msg;

                var bulletType = msg.bulletType;
                byte wRid = msg.wRid;
                float targetPosX = msg.targetPosX / 10000f;
                float targetPosY = msg.targetPosY / 10000f;
                float targetPosZ = msg.targetPosZ / 10000f;
                Vector3 targetPos = new Vector3(targetPosX, targetPosY, targetPosZ);
                targetPos.y = 0;
                var roleEntity = worldFacades.ClientWorldFacades.Repo.WorldRoleRepo.Get(msg.wRid);
                var moveComponent = roleEntity.MoveComponent;
                var shootStartPoint = roleEntity.ShootPointPos;
                Vector3 dir = targetPos - shootStartPoint;
                dir.Normalize();

                // 服务器逻辑
                var clientFacades = worldFacades.ClientWorldFacades;
                var fieldEntity = clientFacades.Repo.FiledEntityRepo.Get(1);
                var bulletEntity = clientFacades.Domain.BulletDomain.SpawnBullet(fieldEntity.transform, (BulletType)bulletType);
                var bulletRepo = clientFacades.Repo.BulletEntityRepo;
                var bulletId = bulletRepo.BulletCount;
                GrenadeEntity grenadeEntity = bulletEntity as GrenadeEntity;
                bulletEntity.MoveComponent.SetCurPos(shootStartPoint);
                bulletEntity.MoveComponent.SetFrameMoveDir(dir);
                bulletEntity.SetWRid(wRid);
                bulletEntity.SetBulletId(bulletId);
                bulletRepo.Add(bulletEntity);
                Debug.Log($"服务器逻辑[Spawn Bullet] frame {worldServeFrame} connId {connId}:  bulletType:{bulletType.ToString()} bulletId:{bulletId}  MasterWRid:{wRid}  起点：{shootStartPoint} 终点：{targetPos} 飞行方向:{dir}");

                var rqs = worldFacades.Network.BulletReqAndRes;
                connIdList.ForEach((otherConnId) =>
                {
                    rqs.SendRes_BulletSpawn(otherConnId, worldServeFrame, bulletType, bulletId, wRid, dir);
                });
            }
        }

        void Tick_BulletLife()
        {
            worldFacades.ClientWorldFacades.Domain.BulletDomain.Tick_BulletLife(NetworkConfig.FIXED_DELTA_TIME);
        }

        #endregion

        void Tick_BulletHitRole()
        {
            var bulletRepo = worldFacades.ClientWorldFacades.Repo.BulletEntityRepo;
            bulletRepo.Foreach((bullet) =>
            {
                int nextFrameIndex = worldServeFrame + 1;
                if (bullet.TryDequeue(out var wrole))
                {
                    worldServeFrame = nextFrameIndex;
                    var rqs = worldFacades.Network.BulletReqAndRes;
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendRes_BulletHitRole(connId, worldServeFrame, bullet.BulletId, wrole.WRid);
                    });

                    // Server Logic
                    wrole.HealthComponent.HurtByBullet(bullet);
                    wrole.MoveComponent.HitByBullet(bullet);
                    if (wrole.HealthComponent.IsDead)
                    {
                        wrole.TearDown();
                        wrole.Reborn();
                    }
                }
            });
        }

        void Tick_RoleMovement()
        {
            var domain = worldFacades.ClientWorldFacades.Domain.WorldRoleSpawnDomain;
            domain.Tick_RoleMovement(fixedDeltaTime);    //客户端的统一物理模拟时间
        }

        void Tick_BulletMovement()
        {
            var domain = worldFacades.ClientWorldFacades.Domain.BulletDomain;
            domain.Tick_BulletMovement();
        }

        // == Network
        // Role
        void OnWoldRoleMove(int connId, FrameOptReqMsg msg)
        {
            if (!wRoleOptQueueDic.TryGetValue(worldServeFrame + 1, out var optQueue))
            {
                optQueue = new Queue<FrameReqOptMsgStruct>();
                wRoleOptQueueDic[worldServeFrame + 1] = optQueue;
            }

            optQueue.Enqueue(new FrameReqOptMsgStruct { connId = connId, msg = msg });
        }

        void OnWoldRoleJump(int connId, FrameJumpReqMsg msg)
        {
            jumpOptDic.TryAdd(worldServeFrame + 1, new FrameReqJumpMsgStruct { connId = connId, msg = msg });
        }

        void OnWoldRoleSpawn(int connId, FrameWRoleSpawnReqMsg msg)
        {
            wRoleSpawnDic.TryAdd(worldServeFrame + 1, new FrameReqWRoleSpawnMsgStruct { connId = connId, msg = msg });
            // TODO:连接服和世界服分离
            connIdList.Add(connId);
            // 创建场景
            sceneSpawnTrigger = true;
        }

        // Bullet
        void OnBulletSpawn(int connId, FrameBulletSpawnReqMsg msg)
        {
            bulletSpawnDic.TryAdd(worldServeFrame + 1, new FrameReqBulletSpawnMsgStruct { connId = connId, msg = msg });
        }

        async void SpawWorldChooseScene()
        {
            // Load Scene And Spawn Field
            var domain = worldFacades.ClientWorldFacades.Domain;
            var fieldEntity = await domain.WorldSpawnDomain.SpawnWorldChooseScene();
            fieldEntity.SetFieldId(1);
            var fieldEntityRepo = worldFacades.ClientWorldFacades.Repo.FiledEntityRepo;
            fieldEntityRepo.Add(fieldEntity);
            fieldEntityRepo.SetPhysicsScene(fieldEntity.gameObject.scene.GetPhysicsScene());
            isSceneSpawn = true;
        }

    }

}