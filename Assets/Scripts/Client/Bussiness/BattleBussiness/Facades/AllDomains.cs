using Game.Client.Bussiness.BattleBussiness.Controller.Domain;

namespace Game.Client.Bussiness.BattleBussiness.Facades
{

    public class AllDomains
    {

        public BattleSceneDomain SceneDomain { get; private set; }

        public BattleRoleDomain RoleDomain { get; private set; }
        public BattleRoleRendererDomain RoleRendererDomain { get; private set; }
        public BattleRoleStateDomain RoleStateDomain { get; private set; }
        public BattleRoleStateRendererDomain RoleStateRendererDomain { get; private set; }

        public BattleInputDomain InputDomain { get; private set; }

        public BulletDomain BulletDomain { get; private set; }

        public ItemDomain ItemDomain { get; private set; }

        public PhysicsDomain PhysicsDomain { get; private set; }

        public WeaponDomain WeaponDomain { get; private set; }

        public BattleHitDomain HitDomain { get; private set; }



        public AllDomains()
        {
            SceneDomain = new BattleSceneDomain();

            RoleDomain = new BattleRoleDomain();
            RoleRendererDomain = new BattleRoleRendererDomain();
            RoleStateDomain = new BattleRoleStateDomain();
            RoleStateRendererDomain = new BattleRoleStateRendererDomain();

            InputDomain = new BattleInputDomain();

            BulletDomain = new BulletDomain();

            ItemDomain = new ItemDomain();

            PhysicsDomain = new PhysicsDomain();

            WeaponDomain = new WeaponDomain();

            HitDomain = new BattleHitDomain();
        }

        // todo: obsolete
        public void Inject(BattleFacades facades)
        {
            SceneDomain.Inject(facades);

            RoleDomain.Inject(facades);
            RoleRendererDomain.Inject(facades);
            RoleStateDomain.Inject(facades);
            RoleStateRendererDomain.Inject(facades);

            InputDomain.Inject(facades);

            BulletDomain.Inject(facades);

            ItemDomain.Inject(facades);

            PhysicsDomain.Inject(facades);

            WeaponDomain.Inject(facades);

            HitDomain.Inject(facades);
        }
    }

}