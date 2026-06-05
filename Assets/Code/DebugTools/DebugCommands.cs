using System;
using System.Collections.Generic;
using System.IO;
using Code.DebugTools.CommandsInterface;
using Code.GameLogic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class DebugCommands : MonoBehaviour
{
    public static DebugCommands Instance;
    public GameObject debugObject;
    [SerializeField] private GameObject _debugPanel;
    [SerializeField] private TMP_InputField inputField;
    private bool debugMode = false;
    private Dictionary<string, IChatCommand> _chatCommands = new Dictionary<string, IChatCommand>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
        _debugPanel.SetActive(false);
    }

    private void Start()
    {
        GameManager.Instance.playerInput.Player.DebugCommand.performed += OpenDebugPanel;
    }

    public void OpenDebugPanel(InputAction.CallbackContext context)
    {
        debugMode = !debugMode;
        _debugPanel.SetActive(debugMode);
        FindAnyObjectByType<EventSystem>().SetSelectedGameObject(inputField.gameObject);
    }

    public void Log(string message)
    {
        if (message.Length < 1) return;

        FindAnyObjectByType<EventSystem>().SetSelectedGameObject(inputField.gameObject);
        var debugText = Instantiate(debugObject, transform.GetChild(0).transform.GetChild(0));
        debugText.GetComponentInChildren<TMP_Text>().text = $"[{DateTime.Now:HH:mm:ss}] {OutputMessage(message)}";
        inputField.text = "";
        Destroy(debugText, 60);
    }

    private string OutputMessage(string message)
    {
        if (message[0] == '/')
        {
            var path = Application.dataPath + "/Commands.txt";
            if (File.Exists(path))
            {
                string[] lineas = File.ReadAllLines(path);

                foreach (string linea in lineas)
                {
                    _chatCommands.TryGetValue(linea, out IChatCommand command);
                    //command.Execute();
                }
            }
            else
            {
            }
        }

        return message;
    }
}
