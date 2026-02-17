using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Fusion;
using CornHole;

namespace CornHole.Editor
{
    /// <summary>
    /// Editor script to automate GameScene setup.
    /// Creates prefabs, scene objects, and UI canvas with all references wired up.
    /// Run via menu: Corn Hole > Setup Game Scene.
    /// </summary>
    public static class SceneSetup
    {
        private const string PrefabsPath = "Assets/Prefabs";

        // ──────────────────────────────────────────────
        //  Menu Items
        // ──────────────────────────────────────────────

        [MenuItem("Corn Hole/Setup Game Scene (Full)")]
        public static void SetupFull()
        {
            if (!EditorUtility.DisplayDialog("Setup Game Scene",
                "This will create prefabs and populate the active scene with:\n" +
                "- Ground plane\n- GameManager + ObjectSpawner\n- Camera (CameraFollow)\n" +
                "- Full UI Canvas (5 panels, 25 references)\n\n" +
                "Existing objects with the same names will be skipped.\nContinue?",
                "Setup", "Cancel"))
                return;

            CreatePrefabsFolder();

            var holePlayerPrefab = CreateHolePlayerPrefab();
            var consumablePrefab = CreateConsumablePrefab();
            var matchTimerPrefab = CreateMatchTimerPrefab();

            SetupGroundPlane();
            SetupGameManager(holePlayerPrefab, matchTimerPrefab);
            SetupObjectSpawner(consumablePrefab);
            SetupCamera();
            SetupUICanvas();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== Corn Hole scene setup complete! ===");
            Debug.Log("Manual steps remaining:");
            Debug.Log("  1. Verify prefab references on NetworkManager (playerPrefab, matchTimerPrefab)");
            Debug.Log("  2. Verify consumable prefab on ObjectSpawner");
            Debug.Log("  3. Configure Photon App ID: Fusion > Realtime Settings");
            Debug.Log("  4. File > Build Settings > Add Open Scenes (GameScene at index 0)");
        }

        [MenuItem("Corn Hole/Setup Minimum (Ground + Menu)")]
        public static void SetupMinimum()
        {
            SetupGroundPlane();
            SetupUICanvas();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("Minimum scene setup complete — ground plane and UI canvas created.");
        }

        // ──────────────────────────────────────────────
        //  Step 1: Prefabs Folder
        // ──────────────────────────────────────────────

        private static void CreatePrefabsFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
                Debug.Log("Created Assets/Prefabs folder.");
            }
        }

        // ──────────────────────────────────────────────
        //  Step 2: HolePlayer Prefab
        // ──────────────────────────────────────────────

        private static GameObject CreateHolePlayerPrefab()
        {
            string path = $"{PrefabsPath}/HolePlayer.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log("HolePlayer prefab already exists — skipping.");
                return existing;
            }

            // Root object
            var root = new GameObject("HolePlayer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            var collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1f;

            var holePlayer = root.AddComponent<HolePlayer>();

            // Visual child — cylinder scaled flat to look like a hole
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(2f, 0.1f, 2f);

            // Remove the default collider from the cylinder primitive
            var cylCollider = visual.GetComponent<CapsuleCollider>();
            if (cylCollider != null) Object.DestroyImmediate(cylCollider);

            // Create a dark material for the hole
            var material = CreateHoleMaterial();
            if (material != null)
            {
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            // Wire references via SerializedObject (fields are private [SerializeField])
            var so = new SerializedObject(holePlayer);
            so.FindProperty("holeVisual").objectReferenceValue = visual.transform;
            so.FindProperty("consumeCollider").objectReferenceValue = collider;
            so.ApplyModifiedProperties();

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"Created HolePlayer prefab at {path}");
            return prefab;
        }

        private static Material CreateHoleMaterial()
        {
            string matPath = $"{PrefabsPath}/HoleMaterial.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback to standard shader
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // ──────────────────────────────────────────────
        //  Step 3: Consumable Prefab
        // ──────────────────────────────────────────────

        private static GameObject CreateConsumablePrefab()
        {
            string path = $"{PrefabsPath}/Consumable_Cube.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log("Consumable_Cube prefab already exists — skipping.");
                return existing;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Consumable_Cube";
            cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            cube.AddComponent<NetworkObject>();

            var rb = cube.GetComponent<Rigidbody>();
            if (rb == null) rb = cube.AddComponent<Rigidbody>();
            rb.useGravity = true;

            var consumable = cube.AddComponent<ConsumableObject>();

            // Wire references
            var so = new SerializedObject(consumable);
            so.FindProperty("objectSize").floatValue = 0.5f;
            so.FindProperty("pointValue").intValue = 10;
            so.FindProperty("sizeValue").floatValue = 0.05f;
            so.FindProperty("rb").objectReferenceValue = rb;
            so.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
            Object.DestroyImmediate(cube);

            Debug.Log($"Created Consumable_Cube prefab at {path}");
            return prefab;
        }

        // ──────────────────────────────────────────────
        //  Step 4: MatchTimer Prefab
        // ──────────────────────────────────────────────

        private static GameObject CreateMatchTimerPrefab()
        {
            string path = $"{PrefabsPath}/MatchTimer.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log("MatchTimer prefab already exists — skipping.");
                return existing;
            }

            var go = new GameObject("MatchTimer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchTimer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"Created MatchTimer prefab at {path}");
            return prefab;
        }

        // ──────────────────────────────────────────────
        //  Step 5a: Ground Plane
        // ──────────────────────────────────────────────

        private static void SetupGroundPlane()
        {
            if (GameObject.Find("Ground") != null)
            {
                Debug.Log("Ground already exists — skipping.");
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
            Debug.Log("Created ground plane.");
        }

        // ──────────────────────────────────────────────
        //  Step 5b: GameManager (NetworkManager)
        // ──────────────────────────────────────────────

        private static void SetupGameManager(GameObject holePlayerPrefab, GameObject matchTimerPrefab)
        {
            if (GameObject.Find("GameManager") != null)
            {
                Debug.Log("GameManager already exists — skipping.");
                return;
            }

            var go = new GameObject("GameManager");
            var nm = go.AddComponent<NetworkManager>();

            // Try to wire prefab references via SerializedObject
            TrySetNetworkPrefabRef(nm, "playerPrefab", holePlayerPrefab);
            TrySetNetworkPrefabRef(nm, "matchTimerPrefab", matchTimerPrefab);

            Undo.RegisterCreatedObjectUndo(go, "Create GameManager");
            Debug.Log("Created GameManager with NetworkManager.");
        }

        // ──────────────────────────────────────────────
        //  Step 5b (cont.): ObjectSpawner (separate scene object)
        // ──────────────────────────────────────────────

        private static void SetupObjectSpawner(GameObject consumablePrefab)
        {
            if (GameObject.Find("ObjectSpawner") != null)
            {
                Debug.Log("ObjectSpawner already exists — skipping.");
                return;
            }

            // ObjectSpawner is a NetworkBehaviour, so it needs its own NetworkObject.
            // Kept separate from GameManager because NetworkManager uses DontDestroyOnLoad.
            var go = new GameObject("ObjectSpawner");
            go.AddComponent<NetworkObject>();
            var spawner = go.AddComponent<ObjectSpawner>();

            // Try to wire the consumable prefab array
            TrySetNetworkPrefabRefArray(spawner, "consumablePrefabs", new[] { consumablePrefab });

            Undo.RegisterCreatedObjectUndo(go, "Create ObjectSpawner");
            Debug.Log("Created ObjectSpawner with NetworkObject (separate from GameManager).");
        }

        // ──────────────────────────────────────────────
        //  Step 5c: Camera
        // ──────────────────────────────────────────────

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("No Main Camera found in scene.");
                return;
            }

            if (cam.GetComponent<CameraFollow>() != null)
            {
                Debug.Log("CameraFollow already on Main Camera — skipping.");
                return;
            }

            cam.gameObject.AddComponent<CameraFollow>();
            cam.transform.position = new Vector3(0f, 15f, -10f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            Undo.RegisterCompleteObjectUndo(cam.gameObject, "Setup Camera");
            Debug.Log("Added CameraFollow to Main Camera.");
        }

        // ──────────────────────────────────────────────
        //  Step 6: UI Canvas
        // ──────────────────────────────────────────────

        private static void SetupUICanvas()
        {
            // Check for existing canvas with GameUI
            var existingUI = Object.FindAnyObjectByType<GameUI>();
            if (existingUI != null)
            {
                Debug.Log("GameUI already exists in scene — skipping UI setup.");
                return;
            }

            // 6a: Canvas + EventSystem
            var canvasGO = CreateCanvas();
            var gameUI = canvasGO.AddComponent<GameUI>();

            // Ensure EventSystem exists
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            // 6c: Menu Panel
            var menuPanel = CreatePanel("MenuPanel", canvasGO.transform, active: true);
            AddLayoutGroup(menuPanel, spacing: 20f, childAlignment: TextAnchor.MiddleCenter);
            var hostButton = CreateButton("HostButton", "Host Game", menuPanel.transform);
            var joinButton = CreateButton("JoinButton", "Join Game", menuPanel.transform);

            // 6d: Join Panel
            var joinPanel = CreatePanel("JoinPanel", canvasGO.transform, active: false);
            AddLayoutGroup(joinPanel, spacing: 15f, childAlignment: TextAnchor.MiddleCenter);
            var joinCodeInput = CreateInputField("JoinCodeInput", "Enter Join Code", joinPanel.transform);
            var submitJoinButton = CreateButton("SubmitJoinButton", "Join", joinPanel.transform);
            var backButton = CreateButton("BackButton", "Back", joinPanel.transform);
            var joinErrorText = CreateText("JoinErrorText", "", joinPanel.transform, Color.red);

            // 6e: Lobby Panel
            var lobbyPanel = CreatePanel("LobbyPanel", canvasGO.transform, active: false);
            AddLayoutGroup(lobbyPanel, spacing: 15f, childAlignment: TextAnchor.MiddleCenter);
            var joinCodeDisplay = CreateText("JoinCodeDisplay", "Join Code: ---", lobbyPanel.transform, Color.white, fontSize: 36);
            var playerListText = CreateText("PlayerListText", "", lobbyPanel.transform, Color.white, fontSize: 24);
            var readyButton = CreateButton("ReadyButton", "Ready", lobbyPanel.transform);
            var readyButtonLabel = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            var startMatchButton = CreateButton("StartMatchButton", "Start Match", lobbyPanel.transform);
            var countdownText = CreateText("CountdownText", "", lobbyPanel.transform, Color.white, fontSize: 72);
            countdownText.alignment = TextAlignmentOptions.Center;

            // 6f: Game Panel (HUD)
            var gamePanel = CreatePanel("GamePanel", canvasGO.transform, active: false, transparent: true);
            var scoreText = CreateText("ScoreText", "Score: 0", gamePanel.transform, Color.white, fontSize: 28);
            AnchorTopLeft(scoreText.rectTransform, new Vector2(10, -10));
            var sizeText = CreateText("SizeText", "Size: 1.0", gamePanel.transform, Color.white, fontSize: 28);
            AnchorTopLeft(sizeText.rectTransform, new Vector2(10, -50));
            var timerText = CreateText("TimerText", "02:00", gamePanel.transform, Color.white, fontSize: 36);
            AnchorTopCentre(timerText.rectTransform, new Vector2(0, -10));

            // 6g: End Panel
            var endPanel = CreatePanel("EndPanel", canvasGO.transform, active: false);
            AddLayoutGroup(endPanel, spacing: 20f, childAlignment: TextAnchor.MiddleCenter);
            var finalScoreText = CreateText("FinalScoreText", "Final Score: 0", endPanel.transform, Color.white, fontSize: 36);
            var finalSizeText = CreateText("FinalSizeText", "Final Size: 1.0", endPanel.transform, Color.white, fontSize: 28);
            var returnButton = CreateButton("ReturnButton", "Return to Menu", endPanel.transform);

            // Wire all 25 references into GameUI
            WireGameUI(gameUI,
                menuPanel: menuPanel,
                hostButton: hostButton.GetComponent<Button>(),
                joinButton: joinButton.GetComponent<Button>(),
                joinPanel: joinPanel,
                joinCodeInput: joinCodeInput,
                submitJoinButton: submitJoinButton.GetComponent<Button>(),
                backToMenuButton: backButton.GetComponent<Button>(),
                joinErrorText: joinErrorText,
                lobbyPanel: lobbyPanel,
                joinCodeDisplay: joinCodeDisplay,
                playerListText: playerListText,
                readyButton: readyButton.GetComponent<Button>(),
                readyButtonLabel: readyButtonLabel,
                startMatchButton: startMatchButton.GetComponent<Button>(),
                countdownText: countdownText,
                gamePanel: gamePanel,
                scoreText: scoreText,
                sizeText: sizeText,
                timerText: timerText,
                endPanel: endPanel,
                finalScoreText: finalScoreText,
                finalSizeText: finalSizeText,
                returnToMenuButton: returnButton.GetComponent<Button>());

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create UI Canvas");
            Debug.Log("Created UI Canvas with all 5 panels and 25 references wired.");
        }

        // ──────────────────────────────────────────────
        //  UI Helpers
        // ──────────────────────────────────────────────

        private static GameObject CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreatePanel(string name, Transform parent, bool active, bool transparent = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            StretchFill(rt);

            var image = go.GetComponent<Image>();
            image.color = transparent
                ? new Color(0f, 0f, 0f, 0f)
                : new Color(0f, 0f, 0f, 0.7f);

            go.SetActive(active);
            return go;
        }

        private static void AddLayoutGroup(GameObject panel, float spacing, TextAnchor childAlignment)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = childAlignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(40, 40, 40, 40);
        }

        private static GameObject CreateButton(string name, string text, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 60);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            // Text child
            var textGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(go.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            StretchFill(textRT);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontSize = 24;

            return go;
        }

        private static TMP_InputField CreateInputField(string name, string placeholder, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 50);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // Text Area
            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var areaRT = textArea.GetComponent<RectTransform>();
            StretchFill(areaRT);
            areaRT.offsetMin = new Vector2(10, 6);
            areaRT.offsetMax = new Vector2(-10, -7);

            // Placeholder
            var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer));
            placeholderGO.transform.SetParent(textArea.transform, false);
            var phRT = placeholderGO.GetComponent<RectTransform>();
            StretchFill(phRT);
            var phText = placeholderGO.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            phText.fontSize = 20;

            // Text
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(textArea.transform, false);
            var tRT = textGO.GetComponent<RectTransform>();
            StretchFill(tRT);
            var inputText = textGO.AddComponent<TextMeshProUGUI>();
            inputText.color = Color.white;
            inputText.fontSize = 20;

            // TMP_InputField
            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = areaRT;
            inputField.textComponent = inputText;
            inputField.placeholder = phText;
            inputField.characterLimit = 6;

            return inputField;
        }

        private static TextMeshProUGUI CreateText(string name, string text, Transform parent,
            Color colour, float fontSize = 24)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 40);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = colour;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;

            return tmp;
        }

        // ──────────────────────────────────────────────
        //  RectTransform Helpers
        // ──────────────────────────────────────────────

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTopLeft(RectTransform rt, Vector2 position)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
        }

        private static void AnchorTopCentre(RectTransform rt, Vector2 position)
        {
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = position;
        }

        // ──────────────────────────────────────────────
        //  Wire GameUI references
        // ──────────────────────────────────────────────

        private static void WireGameUI(GameUI gameUI,
            GameObject menuPanel, Button hostButton, Button joinButton,
            GameObject joinPanel, TMP_InputField joinCodeInput,
            Button submitJoinButton, Button backToMenuButton, TextMeshProUGUI joinErrorText,
            GameObject lobbyPanel, TextMeshProUGUI joinCodeDisplay,
            TextMeshProUGUI playerListText, Button readyButton,
            TextMeshProUGUI readyButtonLabel, Button startMatchButton,
            TextMeshProUGUI countdownText,
            GameObject gamePanel, TextMeshProUGUI scoreText,
            TextMeshProUGUI sizeText, TextMeshProUGUI timerText,
            GameObject endPanel, TextMeshProUGUI finalScoreText,
            TextMeshProUGUI finalSizeText, Button returnToMenuButton)
        {
            var so = new SerializedObject(gameUI);

            // Menu Panel
            so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
            so.FindProperty("hostButton").objectReferenceValue = hostButton;
            so.FindProperty("joinButton").objectReferenceValue = joinButton;

            // Join Panel
            so.FindProperty("joinPanel").objectReferenceValue = joinPanel;
            so.FindProperty("joinCodeInput").objectReferenceValue = joinCodeInput;
            so.FindProperty("submitJoinButton").objectReferenceValue = submitJoinButton;
            so.FindProperty("backToMenuButton").objectReferenceValue = backToMenuButton;
            so.FindProperty("joinErrorText").objectReferenceValue = joinErrorText;

            // Lobby Panel
            so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanel;
            so.FindProperty("joinCodeDisplay").objectReferenceValue = joinCodeDisplay;
            so.FindProperty("playerListText").objectReferenceValue = playerListText;
            so.FindProperty("readyButton").objectReferenceValue = readyButton;
            so.FindProperty("readyButtonLabel").objectReferenceValue = readyButtonLabel;
            so.FindProperty("startMatchButton").objectReferenceValue = startMatchButton;
            so.FindProperty("countdownText").objectReferenceValue = countdownText;

            // Game Panel (HUD)
            so.FindProperty("gamePanel").objectReferenceValue = gamePanel;
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("sizeText").objectReferenceValue = sizeText;
            so.FindProperty("timerText").objectReferenceValue = timerText;

            // End Screen
            so.FindProperty("endPanel").objectReferenceValue = endPanel;
            so.FindProperty("finalScoreText").objectReferenceValue = finalScoreText;
            so.FindProperty("finalSizeText").objectReferenceValue = finalSizeText;
            so.FindProperty("returnToMenuButton").objectReferenceValue = returnToMenuButton;

            so.ApplyModifiedProperties();
        }

        // ──────────────────────────────────────────────
        //  NetworkPrefabRef Helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Attempts to set a NetworkPrefabRef field via SerializedProperty.
        /// NetworkPrefabRef serialisation varies by Fusion version, so this
        /// explores the property tree and tries to find an assignable object reference.
        /// </summary>
        private static void TrySetNetworkPrefabRef(Component component, string fieldName, GameObject prefab)
        {
            if (prefab == null) return;

            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"Could not find property '{fieldName}' on {component.GetType().Name}.");
                return;
            }

            // Explore children of the NetworkPrefabRef property
            bool assigned = false;
            var iterator = prop.Copy();
            int depth = iterator.depth;
            bool enterChildren = true;

            while (iterator.Next(enterChildren))
            {
                enterChildren = false;

                if (iterator.depth <= depth)
                    break;

                // Look for an Object reference field (some Fusion versions store the prefab directly)
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    iterator.objectReferenceValue = prefab;
                    assigned = true;
                    Debug.Log($"Set {fieldName} → {prefab.name} via {iterator.propertyPath}");
                    break;
                }
            }

            if (!assigned)
            {
                Debug.LogWarning(
                    $"Could not auto-assign '{fieldName}' on {component.GetType().Name}. " +
                    $"Please drag '{prefab.name}' from Assets/Prefabs/ into the field manually.");
            }

            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Attempts to set a NetworkPrefabRef[] array field.
        /// </summary>
        private static void TrySetNetworkPrefabRefArray(Component component, string fieldName, GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            var so = new SerializedObject(component);
            var arrayProp = so.FindProperty(fieldName);
            if (arrayProp == null || !arrayProp.isArray)
            {
                Debug.LogWarning($"Could not find array property '{fieldName}' on {component.GetType().Name}.");
                return;
            }

            arrayProp.arraySize = prefabs.Length;

            bool anyAssigned = false;
            for (int i = 0; i < prefabs.Length; i++)
            {
                var elementProp = arrayProp.GetArrayElementAtIndex(i);
                var iterator = elementProp.Copy();
                int depth = iterator.depth;
                bool enterChildren = true;

                while (iterator.Next(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.depth <= depth) break;

                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        iterator.objectReferenceValue = prefabs[i];
                        anyAssigned = true;
                        Debug.Log($"Set {fieldName}[{i}] → {prefabs[i].name}");
                        break;
                    }
                }
            }

            if (!anyAssigned)
            {
                Debug.LogWarning(
                    $"Could not auto-assign '{fieldName}' on {component.GetType().Name}. " +
                    "Please assign consumable prefabs manually in the Inspector.");
            }

            so.ApplyModifiedProperties();
        }
    }
}
