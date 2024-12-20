using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

namespace Code.GameLogic
{
    public class SceneChanger : NetworkBehaviour
    {
        private static SceneChanger _instance;

        public static SceneChanger Instance
        {
            get
            {
                _instance = FindFirstObjectByType<SceneChanger>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("SceneChanger", typeof(SceneChanger), typeof(NetworkIdentity));
                    _instance = obj.GetComponent<SceneChanger>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        [ClientRpc]
        public void ChangeScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}