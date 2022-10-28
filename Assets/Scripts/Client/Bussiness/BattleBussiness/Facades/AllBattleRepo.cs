using Game.Client.Bussiness.BattleBussiness.Repo;
using Game.Client.Bussiness.Repo;

namespace Game.Client.Bussiness.BattleBussiness.Facades
{

    public class AllBattleRepo
    {

        public FiledEntityRepo FiledRepo { get; private set; }
        public BattleRoleRepo RoleRepo { get; private set; }
        public WeaponRepo WeaponRepo { get; private set; }
        public WeaponItemRepo WeaponItemRepo { get; private set; }
        public BattleArmorRepo ArmorRepo { get; private set; }
        public BulletRepo BulletRepo { get; private set; }
        public BulletItemRepo BulletItemRepo { get; private set; }
        public BattleArmorItemRepo ArmorItemRepo { get; private set; }

        public AllBattleRepo()
        {
            FiledRepo = new FiledEntityRepo();
            RoleRepo = new BattleRoleRepo();
            WeaponRepo = new WeaponRepo();
            WeaponItemRepo = new WeaponItemRepo();
            ArmorRepo = new BattleArmorRepo();
            ArmorItemRepo = new BattleArmorItemRepo();
            BulletRepo = new BulletRepo();
            BulletItemRepo = new BulletItemRepo();
        }

    }

}