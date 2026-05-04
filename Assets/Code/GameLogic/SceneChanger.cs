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
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SceneChanger>();
                    if (_instance == null)
                    {
                        Debug.LogError("SceneChanger is missing from the scene! Make sure it is added via the Editor for Mirror to work properly.");
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [Server]
        public void ChangeScene(string sceneName)
        {
            NetworkManager.singleton.ServerChangeScene(sceneName);
        }
    }
}