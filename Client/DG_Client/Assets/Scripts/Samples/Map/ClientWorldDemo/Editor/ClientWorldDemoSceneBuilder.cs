using DG.Map;
using Fantasy;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DG.EditorTools
{
    public static class ClientWorldDemoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ClientWorldRunnerDemo.unity";

        [MenuItem("DG/ClientWorld/Create Client ClientMapWorld Runner Demo Scene")]
        public static void CreateScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ClientMapWorld = new GameObject("ClientWorld");
            ClientMapWorld.AddComponent<WorldBootstrap>();
            ClientMapWorld.AddComponent<ClientWorldRunner>();
            ClientMapWorld.AddComponent<ClientMoveNetworkSubmitter>();
            ClientMapWorld.AddComponent<ClientWorldDemo>();
            ClientMapWorld.AddComponent<ClientWorldVisuals>();
            ClientMapWorld.AddComponent<ClientWorldDebugEditor>();

            GameObject fantasyRuntimeObject = new GameObject("FantasyRuntime");
            FantasyRuntime fantasyRuntime = fantasyRuntimeObject.AddComponent<FantasyRuntime>();
            fantasyRuntime.remoteIP = "127.0.0.1";
            fantasyRuntime.remotePort = 20000;
            fantasyRuntime.protocol = FantasyRuntime.NetworkProtocolType.KCP;
            fantasyRuntime.enableHeartbeat = true;

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 1f);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(1.5f, 1.5f, -10f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Selection.activeGameObject = ClientMapWorld;
        }
    }
}


