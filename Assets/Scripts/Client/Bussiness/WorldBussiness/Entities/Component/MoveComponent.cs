using UnityEngine;
using Game.Generic;
using Game.Client.Bussiness.WorldBussiness.Shot;

namespace Game.Client.Bussiness.WorldBussiness
{

    public class MoveComponent
    {

        // TODO: INJECT
        float speed;
        public void SetSpeed(float speed) => this.speed = speed;

        float maximumSpeed = float.MaxValue;
        public void SetMaximumSpeed(float speed) => this.maximumSpeed = speed;

        float jumpSpeed;
        public void SetJumpVelocity(float jumpSpeed) => this.jumpSpeed = jumpSpeed;

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

        public bool isPersistentMove;

        // == 移动速度 1
        Vector3 moveVelocity;
        public Vector3 MoveVelocity => moveVelocity;
        public void SetMoveVelocity(Vector3 moveVelocity) => this.moveVelocity = moveVelocity;
        public void ActivateMoveVelocity(Vector3 dir)
        {
            dir.Normalize();
            dir = dir.FixDecimal(2);
            this.moveVelocity = dir * speed;
        }

        // == 跳跃速度 2
        float jumpVelocity;
        public float JumpVelocity => jumpVelocity;

        // == 重力速度 3
        float _gravityVelocity;
        public float GravityVelocity => _gravityVelocity;
        public void SetGravityVelocity(float gravityVelocity) => this._gravityVelocity = gravityVelocity;

        // == 额外速度 4
        Vector3 extraVelocity;
        public Vector3 ExtraVelocity => extraVelocity;
        public void SetExtraVelocity(Vector3 extraVelocity) => this.extraVelocity = extraVelocity;
        public void AddExtraVelocity(Vector3 addVelocity) => this.extraVelocity += addVelocity.FixDecimal(4);

        float _gravity;
        public void SetGravity(float _gravity) => this._gravity = _gravity;

        public bool IsGrouded { get; private set; }
        public bool IsHitWall { get; private set; }

        public Vector3 CurPos => rb.position;
        public void SetCurPos(Vector3 curPos) => rb.position = curPos;

        public Vector3 EulerAngel => rb.rotation.eulerAngles;

        public MoveComponent(Rigidbody rb, float speed = 0, float jumpVelocity = 0)
        {
            this.rb = rb;
            this.speed = speed;
            this.jumpSpeed = jumpVelocity;
            rb.useGravity = false;  //关闭自动重力
            _gravity = 10;
        }

        public MoveComponentShot ToShot()
        {
            var shot = new MoveComponentShot { CurPos = CurPos, Velocity = Velocity }; ;
            Debug.Log($"MoveComponentShot : CurPos {CurPos}");
            return shot;
        }

        public void Sync(MoveComponentShot moveComponentShot)
        {
            SetCurPos(moveComponentShot.CurPos);
            SetVelocity(moveComponentShot.Velocity);
        }

        public void SetJumpVelocity()
        {
            Debug.Log("SetJumpVelocity");
            if (!IsGrouded) LeaveGround();

            var v = rb.velocity;
            v.y = 0;
            rb.velocity = v;

            jumpVelocity = jumpSpeed;

            _gravityVelocity = 0;
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
            vel.y = rb.velocity.y + jumpVelocity + _gravityVelocity * fixedDeltaTime;   //Y轴
            vel += extraVelocity;//XYZ轴
            rb.velocity = vel;

            //限制'最大速度'
            if (rb.velocity.magnitude > maximumSpeed) rb.velocity = rb.velocity.normalized * 30f;

            // 重置 ‘一次性速度’
            moveVelocity = Vector3.zero;
            jumpVelocity = 0;
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
                }
                else
                {
                    if (Mathf.Abs(extraVelocity.x) <= 0.1f) extraVelocity.x = 0f;
                    if (Mathf.Abs(extraVelocity.z) <= 0.1f) extraVelocity.z = 0f;
                }
                // Debug.Log($"cosValue:{cosValue}摩擦力过后frictionReduce:{frictionReduce} reduceVelocity:{reduceVelocity} extraVelocity: {extraVelocity}");
            }

        }

        public void Tick_GravityVelocity(float fixedDeltaTime)
        {
            //模拟重力
            if (!IsGrouded)
            {
                _gravityVelocity -= (fixedDeltaTime * _gravity);

                if (extraVelocity.y > 0)
                {
                    extraVelocity.y -= (fixedDeltaTime * _gravity);
                    if (extraVelocity.y < 0) extraVelocity.y = 0;
                }

                if (moveVelocity.y > 0)
                {
                    moveVelocity.y -= (fixedDeltaTime * _gravity);
                    if (moveVelocity.y < 0) moveVelocity.y = 0;
                }
            }
        }

        public void HitSomething(Vector3 hitDir)
        {
            var log = $"碰撞某物，碰撞方向:{hitDir}";
            Debug.Log($"<color=#191970>{log}</color>");
            //  消除反方向V
            EraseVelocity(hitDir);
        }

        public void EraseVelocity(Vector3 dir)
        {
            var a = extraVelocity.normalized;
            var b = dir.normalized;
            var cosValue = Vector3.Dot(a, b);
            var reduceVelocity = extraVelocity * cosValue;
            extraVelocity -= reduceVelocity;
            DebugExtensions.LogWithColor($"碰撞消除速度[ExtraVelocity]:{extraVelocity}", "#191970");

            var gravityVelocity = new Vector3(0, _gravityVelocity, 0);
            a = gravityVelocity.normalized;
            b = dir.normalized;
            cosValue = Vector3.Dot(a, b);
            reduceVelocity = 2f * gravityVelocity * cosValue;
            gravityVelocity -= reduceVelocity;
            _gravityVelocity = gravityVelocity.y;

            DebugExtensions.LogWithColor($"碰撞消除速度[GravityVelocity]:{_gravityVelocity}", "#191970");
        }

        public void LeaveSomthing(Vector3 leaveDir)
        {
            Debug.Log($"离开某物，方向:{leaveDir}");
        }

        public void LeaveGround()
        {
            Debug.Log("离开地面");
            IsGrouded = false;
        }

        public void EnterGound()
        {
            Debug.Log("接触Field");
            IsGrouded = true;

            //重力速度 归零
            _gravityVelocity = 0;
            //额外速度Y轴 归零
            extraVelocity.y = 0;
            //移动速度Y轴 归零
            moveVelocity.y = 0;
            //RigidBody速度 归零
            var v = rb.velocity;
            v.y = 0;
            rb.velocity = v;
        }

        public void LeaveWall()
        {
            Debug.Log("离开墙体");
            IsHitWall = false;
        }

        public void EnterWall()
        {

            Debug.Log($"{rb.gameObject.name} 接触墙体");
            IsHitWall = true;

            // TODO: 惯性指定方向清零
            // if (Velocity != Vector3.zero)
            // {
            // }

            // 水平速度归零
            extraVelocity.x = 0;
            extraVelocity.z = 0;
            extraVelocity = Vector3.zero;
        }

        public void FaceTo(Vector3 forward)
        {
            rb.rotation = Quaternion.LookRotation(forward);
        }

        public void HitByBullet(BulletEntity bulletEntity)
        {
            var velocity = bulletEntity.MoveComponent.Velocity / 10f;
            Debug.Log($"HitByBullet velocity add:  {velocity}");
            rb.velocity += (velocity);
        }

        public void SetEulerAngle(Vector3 eulerAngle)
        {
            rb.rotation = Quaternion.Euler(eulerAngle);
        }

        public void AddEulerAngleY(float eulerAngleY)
        {
            var euler = rb.rotation.eulerAngles;
            euler.y += eulerAngleY;    //左右看
            rb.rotation = Quaternion.Euler(euler);
        }

        public void Reset()
        {
            rb.position = new Vector3(0, 10f, 0);
            rb.velocity = Vector3.zero;
            rb.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        public string ToInfo()
        {
            return $"位置: {CurPos} 移动速度: {MoveVelocity} 跳跃速度: {JumpVelocity} 重力速度: {GravityVelocity} 额外速度: {ExtraVelocity} ";
        }

    }

}