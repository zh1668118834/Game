using UnityEngine;

namespace Game.Client.Bussiness
{

    public class FieldEntity : MonoBehaviour
    {
        byte fieldId;
        public byte FieldId => fieldId;
        public void SetFieldId(byte id) => fieldId = id;

        public CinemachineComponent CameraComponent { get; private set; }

        void Awake()
        {
            CameraComponent = new CinemachineComponent();
        }


    }


}