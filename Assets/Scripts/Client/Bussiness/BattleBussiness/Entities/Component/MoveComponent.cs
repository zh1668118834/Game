using UnityEngine;
using Game.Generic;
using Game.Client.Bussiness.BattleBussiness.Shot;
using System;

namespace Game.Client.Bussiness.BattleBussiness
{

    [Serializable]
    public class MoveComponent
    {

        #region [静态变量]

        [SerializeField]
        [Header("移动速度")]
        float moveSpeed;

        [SerializeField]
        [Header("持续移动")]
        bool isPersistentMove;
        public void SetPersistentMove(bool flag) => isPersistentMove = flag;

        [SerializeField]
        [Header("地球引力加速度")]
        float gravity;

        [SerializeField]
        [Header("前滚翻速度")]
        float rollSpeed;
        public float RollVelocity => rollSpeed;

        #endregion

        float maximumSpeed = float.MaxValue;
        public void SetMaximumSpeed(float speed) => this.maximumSpeed = speed;

        // TODO: 滑铲摩擦力
        float frictionReduce = 10f;
        public void SetFriction(float friction) => this.frictionReduce = friction;

        // Rigidbody
        Rigidbody rb;

        public void SetForward(Vector3 forward)
        {
            this.rb.rotation = Quaternion.LookRotation(forward);
        }

        public Vector3 Velocity => rb.velocity;
        public void SetVelocity(Vector3 velocity) => rb.velocity = velocity;

        #region [动态速度]

        // == 移动速度 
        Vector3 moveVelocity;
        public Vector3 MoveVelocity => moveVelocity;
        public void SetMoveVelocity(Vector3 moveVelocity) => this.moveVelocity = moveVelocity;
        public void ActivateMoveVelocity(Vector3 dir)
        {
            dir.Normalize();
            dir = dir.FixDecimal(2);
            this.moveVelocity = dir * moveSpeed;
            // Debug.Log($"移动:{moveVelocity}");
        }

        // == 重力速度 
        float gravityVelocity;
        public float GravityVelocity => gravityVelocity;
        public void SetGravityVelocity(float gravityVelocity) => this.gravityVelocity = gravityVelocity;

        // == 额外速度 
        Vector3 extraVelocity;
        public Vector3 ExtraVelocity => extraVelocity;
        public void SetExtraVelocity(Vector3 extraVelocity) => this.extraVelocity = extraVelocity;
        public void AddExtraVelocity(Vector3 addVelocity) => this.extraVelocity += addVelocity.FixDecimal(4);

        #endregion

        // == 碰撞状态
        public bool IsGrouded { get; private set; }
        public bool IsHitWall { get; private set; }

        public Vector3 Position => rb.position;
        public void SetCurPos(Vector3 curPos) => rb.position = curPos;

        // == Rotation
        public Quaternion Rotation => rb.rotation;
        public Vector3 EulerAngle => rb.rotation.eulerAngles;
        public Vector3 OldEulerAngle { get; private set; }
        public void FaceTo(Vector3 forward)
        {
            var newRotation = Quaternion.LookRotation(forward);
            rb.rotation = newRotation;
        }
        public void SetEulerAngle(Vector3 eulerAngle) => rb.rotation = Quaternion.Euler(eulerAngle);
        public void AddEulerAngleY(float eulerAngleY)
        {
            var euler = rb.rotation.eulerAngles;
            euler.y += eulerAngleY;    //左右看
            rb.rotation = Quaternion.Euler(euler);
        }
        public void SetEulerAngleY(Vector3 eulerAngle)
        {
            var euler = rb.rotation.eulerAngles;
            euler.y = eulerAngle.y;
            rb.rotation = Quaternion.Euler(euler);
        }
        public void FlushEulerAngle() => OldEulerAngle = EulerAngle;
        public bool IsEulerAngleNeedFlush()
        {
            if (Mathf.Abs(OldEulerAngle.x - EulerAngle.x) > 10) return true;
            if (Mathf.Abs(OldEulerAngle.y - EulerAngle.y) > 10) return true;
            if (Mathf.Abs(OldEulerAngle.z - EulerAngle.z) > 10) return true;
            return false;
        }

        public void Inject(Rigidbody rb)
        {
            if (rb == null) return;
            this.rb = rb;
            rb.useGravity = false;  //关闭地球引力
        }

        public bool TryRollForward(Vector3 dir)
        {
            if (!IsGrouded) return false;

            dir.Normalize();
            var addVelocity = dir * rollSpeed;
            // addVelocity.y = 2f;
            extraVelocity += addVelocity;
            Debug.Log($"前滚翻 dir {dir} rollSpeed {rollSpeed} addVelocity:{addVelocity}");
            return true;
        }

        public void Tick_Rigidbody(float fixedDeltaTime)
        {
            if (fixedDeltaTime == 0) return;

            Vector3 vel = Vector3.zero;
            if (isPersistentMove)
            {
                //比如子弹
                rb.velocity = moveVelocity;
                return;
            }

            vel = moveVelocity;//XZ轴
            vel.y = rb.velocity.y + gravityVelocity * fixedDeltaTime;   //Y轴
            vel += extraVelocity;//XYZ轴
            rb.velocity = vel;

            //限制'最大速度'
            if (rb.velocity.magnitude > maximumSpeed) rb.velocity = rb.velocity.normalized * 30f;

            // 重置 ‘一次性速度’
            moveVelocity = Vector3.zero;
        }

        public void Tick_Friction(float fixedDeltaTime)
        {
            //模拟摩擦力
            if (IsGrouded && (Mathf.Abs(extraVelocity.x) > 0.1f || Mathf.Abs(extraVelocity.z) > 0.1f))
            {
                var reduceVelocity = extraVelocity.normalized;
                reduceVelocity.y = 0;
                extraVelocity -= (frictionReduce * reduceVelocity * fixedDeltaTime);
                var cosValue = Vector3.Dot(reduceVelocity.normalized, extraVelocity.normalized);
                if (cosValue <= 0)
                {
                    extraVelocity.z = 0f;
                    extraVelocity.x = 0f;
                    Debug.Log($"摩擦力作用结束");
                }
                else
                {
                    if (Mathf.Abs(extraVelocity.x) <= 0.1f) extraVelocity.x = 0f;
                    if (Mathf.Abs(extraVelocity.z) <= 0.1f) extraVelocity.z = 0f;
                }
            }

        }

        public void Tick_GravityVelocity(float fixedDeltaTime)
        {
            //模拟重力
            if (!IsGrouded)
            {
                if (extraVelocity.y > 0)
                {
                    extraVelocity.y -= (fixedDeltaTime * gravity);
                    if (extraVelocity.y < 0) extraVelocity.y = 0;
                }
                else
                {
                    gravityVelocity -= (fixedDeltaTime * gravity);
                }

                if (moveVelocity.y > 0)
                {
                    moveVelocity.y -= (fixedDeltaTime * gravity);
                    if (moveVelocity.y < 0) moveVelocity.y = 0;
                }
            }
        }

        public void Reset()
        {
            LeaveGround();
            gravityVelocity = 0;
            rollSpeed = 0;
            extraVelocity = Vector3.zero;
            moveVelocity = Vector3.zero;
            rb.velocity = Vector3.zero;

            rb.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        public string ToInfo()
        {
            return $"位置: {Position} 移动速度: {MoveVelocity} 跳跃速度: {RollVelocity} 重力速度: {GravityVelocity} 额外速度: {ExtraVelocity} ";
        }

        #region [ Physics Collision]

        public void MoveHitErase(Vector3 hitDir)
        {
            var a = MoveVelocity.normalized;
            var b = hitDir.normalized;
            var cosValue = Vector3.Dot(a, b);
            var reduceVelocity = MoveVelocity * cosValue;
            moveVelocity -= reduceVelocity;
            if (cosValue != 0)
            {
                // DebugExtensions.LogWithColor($"贴墙移动，消除垂直墙面速度 {reduceVelocity}  moveVelocity{moveVelocity} cosValue:{cosValue}", "#48D1CC");
            }
        }

        public void HitSomething(Vector3 hitDir)
        {
            // DebugExtensions.LogWithColor($"碰撞某物，碰撞方向:{hitDir}", "#48D1CC");
            //  消除反方向Velocity
            EraseVelocity(hitDir);
        }

        public void LeaveSomthing(Vector3 leaveDir)
        {
            // DebugExtensions.LogWithColor($"离开某物，方向:{leaveDir}", "#48D1CC");
        }

        public void EraseVelocity(Vector3 dir)
        {
            var a = rb.velocity.normalized;
            var b = dir.normalized;
            var cosValue = Vector3.Dot(a, b);
            var reduceVelocity = rb.velocity * cosValue;
            rb.velocity -= Vector3.zero;
            // DebugExtensions.LogWithColor($"碰撞消除'Rigidbody速度':{reduceVelocity}---->新'Rigidbody速度':{rb.velocity}", "#48D1CC");

            a = extraVelocity.normalized;
            b = dir.normalized;
            cosValue = Vector3.Dot(a, b);
            reduceVelocity = extraVelocity * cosValue;
            extraVelocity -= reduceVelocity;
            // DebugExtensions.LogWithColor($"碰撞消除'额外速度':{reduceVelocity}---->新'额外速度':{extraVelocity}", "#48D1CC");

            var gravityVelocity = new Vector3(0, this.gravityVelocity, 0);
            a = gravityVelocity.normalized;
            b = dir.normalized;
            cosValue = Vector3.Dot(a, b);
            reduceVelocity = gravityVelocity * cosValue;
            gravityVelocity -= reduceVelocity;
            this.gravityVelocity = gravityVelocity.y;
            // DebugExtensions.LogWithColor($"碰撞消除'重力速度':{reduceVelocity}---->新'重力速度':{_gravityVelocity}", "#48D1CC");
        }

        public void JumpboardSpeedUp()
        {
            DebugExtensions.LogWithColor($"跳板起飞！！！！！", "#48D1CC");
            extraVelocity.y += 2f;
            var addVelocity = rb.velocity;
            addVelocity.y = 0;
            addVelocity = addVelocity * 5f;
            extraVelocity += addVelocity * 5f;
            DebugExtensions.LogWithColor($"跳板起飞  addVelocity:{addVelocity} extraVelocity:{extraVelocity}", "#48D1CC");
        }

        public void EnterGound()
        {
            if (IsGrouded) return;

            DebugExtensions.LogWithColor($"{rb.gameObject.name}接触地面------------------------", "#48D1CC");
            IsGrouded = true;
        }

        public void LeaveGround()
        {
            if (!IsGrouded) return;

            DebugExtensions.LogWithColor($"{rb.gameObject.name}离开地面-----------------------", "#48D1CC");
            IsGrouded = false;
        }

        public void EnterWall()
        {
            if (IsHitWall) return;

            DebugExtensions.LogWithColor($"{rb.gameObject.name}接触墙体---------------------------", "#48D1CC");
            IsHitWall = true;
        }

        public void LeaveWall()
        {
            if (!IsHitWall) return;

            DebugExtensions.LogWithColor($"{rb.gameObject.name}离开墙体-----------------------------", "#48D1CC");
            IsHitWall = false;
        }

        public void HitByBullet(BulletEntity bulletEntity)
        {
            if (bulletEntity == null)
            {
                Debug.LogWarning("HitByBullet Null");
                return;
            }


            var velocity = bulletEntity.MoveComponent.Velocity / 10f;
            Debug.Log($"被子弹击中 extraVelocity 增加:  {velocity}");
            extraVelocity += velocity;
        }

        #endregion
    }

}