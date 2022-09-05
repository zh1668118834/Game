using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Game.Infrastructure.Input;
using Game.Client.Bussiness.EventCenter;
using Game.Client.Bussiness.WorldBussiness.Facades;
using System.Threading.Tasks;

namespace Game.Client.Bussiness.WorldBussiness.Controller.Domain
{

    public class WorldSpawnDomain
    {
        WorldFacades worldFacades;

        public WorldSpawnDomain()
        {
        }

        public void Inject(WorldFacades worldFacades)
        {
            this.worldFacades = worldFacades;
        }

        public async Task<FieldEntity> SpawnWorldChooseScene()
        {
            var result = await Addressables.LoadSceneAsync("WorldChooseScene", LoadSceneMode.Single).Task;
            var scene = result.Scene;
            var sceneObjs = scene.GetRootGameObjects();
            var fieldTrans = sceneObjs[0].transform;
            var fieldEntity = fieldTrans.GetComponent<FieldEntity>();
            var cinemachineExtra = fieldTrans.GetComponentInChildren<CinemachineExtra>();

            var cameraAsset = worldFacades.Assets.CameraAsset;
            cameraAsset.TryGetByName("FirstViewCam", out GameObject firstViewCamPrefab);
            cameraAsset.TryGetByName("ThirdViewCam", out GameObject thirdViewCamPrefab);
            var firstCam = GameObject.Instantiate(firstViewCamPrefab).GetComponent<CinemachineExtra>();
            var thirdCam = GameObject.Instantiate(thirdViewCamPrefab).GetComponent<CinemachineExtra>();
            fieldEntity.CameraComponent.SetFirstViewCam(firstCam);
            fieldEntity.CameraComponent.SetThirdViewCam(thirdCam);

            Debug.Log("生成大世界---------------------------");
            LocalEventCenter.Invoke_SceneLoadedHandler(scene.name);

            return fieldEntity;
        }

    }

}