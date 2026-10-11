using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

public class GeminiAssistantWindow : EditorWindow
{
    private string apiKey = "";
    private string userPrompt = "";
    private Vector2 scrollPos;
    private bool isRequesting = false;

    private UnityWebRequest currentRequest;
    private string lastPrompt = "";
    private int retryCount = 0;
    private const int MaxRetries = 2;

    [System.Serializable]
    private class ChatMessage
    {
        public string role;
        public string text;
        public string extractedCode;
        public string suggestedFileName;
    }

    private List<ChatMessage> chatLog = new List<ChatMessage>();

    [MenuItem("Window/Gemini Assistant")]
    public static void ShowWindow()
    {
        GetWindow<GeminiAssistantWindow>("Gemini Assistant");
    }

    private void OnEnable()
    {
        apiKey = EditorPrefs.GetString("GEMINI_API_KEY", "");
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        if (currentRequest != null)
        {
            currentRequest.Abort();
            currentRequest.Dispose();
            currentRequest = null;
        }
    }

    private void OnEditorUpdate()
    {
        if (isRequesting && currentRequest != null)
        {
            if (currentRequest.isDone)
            {
                HandleResponse();
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Gemini Unity Assistant", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        apiKey = EditorGUILayout.PasswordField("Gemini API Key:", apiKey);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString("GEMINI_API_KEY", apiKey);
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            EditorGUILayout.HelpBox("Google AI Studioで発行したAPIキーを入力してください。", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(5);

        // 履歴クリアボタン
        if (chatLog.Count > 0)
        {
            if (GUILayout.Button("チャット履歴をクリア", GUILayout.Width(150)))
            {
                chatLog.Clear();
            }
            EditorGUILayout.Space(4);
        }
        //ステージ生成ボタン
        if (GUILayout.Button("ステージを自動生成・配置 (Plane & Camera)", GUILayout.Height(25)))
        {
            SetupPrototypeStage();
        }
        //ユニット自動作成ボタン
        if (GUILayout.Button("テスト用ユニットを配置 (味方青 & 敵赤)", GUILayout.Height(25)))
        {
            SpawnTestUnits();
        }
        if (GUILayout.Button("出撃システム自動セットアップ (Prefabs & BattleManager)", GUILayout.Height(25)))
        {
            SetupBattleSystem();
        }
        EditorGUILayout.Space(5);
        // チャット履歴表示エリア
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        foreach (var msg in chatLog)
        {
            if (msg.role == "user")
            {
                GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("あなた:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(msg.text, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
            else
            {
                GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Gemini:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(msg.text, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(msg.extractedCode))
                {
                    EditorGUILayout.Space(4);
                    GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                    if (GUILayout.Button($"C#スクリプトとして保存 ({msg.suggestedFileName})"))
                    {
                        SaveScript(msg.suggestedFileName, msg.extractedCode);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(6);
        }
        EditorGUILayout.EndScrollView();

        // プロンプト入力エリア
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        userPrompt = EditorGUILayout.TextArea(userPrompt, GUILayout.Height(60));
        
        GUI.enabled = !isRequesting && !string.IsNullOrWhiteSpace(userPrompt);
        if (GUILayout.Button(isRequesting ? "思考中..." : "送信", GUILayout.Width(80), GUILayout.Height(60)))
        {
            StartNewRequest();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void StartNewRequest()
    {
        lastPrompt = userPrompt;
        userPrompt = "";
        retryCount = 0;
        chatLog.Add(new ChatMessage { role = "user", text = lastPrompt });
        Repaint();

        SendRequestInternal();
    }

    private void SendRequestInternal()
    {
        isRequesting = true;

        string systemInstruction = 
            "あなたはUnity C#のシニアエンジニアです。タワーディフェンスのプロトタイプ作成を支援してください。" +
            "スクリプトを作成・提案する際は、必ず ```csharp ... ``` の形式で出力してください。" +
            "コードにはクラス名を明確に含めてください。";

        string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + 
            EscapeJson(systemInstruction + "\n\nユーザーの要望: " + lastPrompt) + 
            "\"}]}]}";

        // モデルエンドポイント
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent?key={apiKey}";

        currentRequest = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        currentRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        currentRequest.downloadHandler = new DownloadHandlerBuffer();
        currentRequest.SetRequestHeader("Content-Type", "application/json");
        currentRequest.timeout = 90;

        currentRequest.SendWebRequest();
    }

    private void HandleResponse()
    {
        // 503混雑エラー時は自動リトライ
        if (currentRequest.responseCode == 503 && retryCount < MaxRetries)
        {
            retryCount++;
            currentRequest.Dispose();
            currentRequest = null;
            
            // 1.5秒後に再送
            EditorApplication.delayCall += () =>
            {
                if (this != null) SendRequestInternal();
            };
            return;
        }

        retryCount = 0;
        isRequesting = false;

        if (currentRequest.result != UnityWebRequest.Result.Success)
        {
            chatLog.Add(new ChatMessage { 
                role = "gemini", 
                text = $"エラーが発生しました: HTTP/{currentRequest.responseCode} {currentRequest.error}\n{currentRequest.downloadHandler?.text}" 
            });
        }
        else
        {
            string responseJson = currentRequest.downloadHandler.text;
            string replyText = ParseGeminiResponse(responseJson);

            string code = ExtractCSharpCode(replyText);
            string className = ExtractClassName(code);

            chatLog.Add(new ChatMessage {
                role = "gemini",
                text = replyText,
                extractedCode = code,
                suggestedFileName = string.IsNullOrEmpty(className) ? "GeneratedScript.cs" : $"{className}.cs"
            });
        }

        currentRequest.Dispose();
        currentRequest = null;
        Repaint();
    }

    private string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }

    private string ParseGeminiResponse(string json)
    {
        Match match = Regex.Match(json, "\"text\"\\s*:\\s*\"(.*?)(?<!\\\\)\"", RegexOptions.Singleline);
        if (match.Success)
        {
            string raw = match.Groups[1].Value;
            return Regex.Unescape(raw);
        }
        return "応答テキストの解析に失敗しました。";
    }

    private string ExtractCSharpCode(string text)
    {
        Match match = Regex.Match(text, @"```(?:csharp|cs|c#)?\s*\n?([\s\S]*?)```", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private string ExtractClassName(string code)
    {
        Match match = Regex.Match(code, @"public\s+class\s+([A-Za-z0-9_]+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    private void SaveScript(string fileName, string code)
    {
        string dirPath = Application.dataPath + "/Scripts";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        string fullPath = Path.Combine(dirPath, fileName);
        File.WriteAllText(fullPath, code, Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("保存完了", $"{fileName} を Assets/Scripts に保存しました！", "OK");
    }
    // --- シーン自動セットアップ用機能 ---
    [MenuItem("Tools/BraveLegion/Setup Prototype Stage")]
    public static void SetupPrototypeStage()
    {
        // 1. 地面 (Plane) の作成
        GameObject plane = GameObject.Find("Ground");
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
        }
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(3f, 1f, 6f); // 縦長のレーン

        // 2. カメラのアングル調整（斜め上45度クォータービュー）
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0f, 15f, -12f);
            mainCam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        }

        // 3. NavMeshSurface のアタッチとベイク（AI Navigation パッケージ導入時）
        // 型をリフレクションまたは動的取得して安全に追加
        var navSurfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (navSurfaceType != null)
        {
            var surface = plane.GetComponent(navSurfaceType);
            if (surface == null)
            {
                surface = plane.AddComponent(navSurfaceType);
            }
            // Bake() メソッドを呼び出し
            var bakeMethod = navSurfaceType.GetMethod("BuildNavMesh");
            if (bakeMethod != null)
            {
                bakeMethod.Invoke(surface, null);
                Debug.Log("NavMeshのベイクが完了しました！");
            }
        }
        else
        {
            Debug.LogWarning("AI Navigation パッケージが見つかりませんでした。Package Managerからインストールしてください。");
        }

        Selection.activeGameObject = plane;
        EditorUtility.DisplayDialog("セットアップ完了", "プロトタイプ用ステージ（Ground・カメラアングル・NavMesh）を配置しました！", "OK");
    }
    // --- テスト用ユニット自動作成機能 ---
    [MenuItem("Tools/BraveLegion/Spawn Test Units")]
    public static void SpawnTestUnits()
    {
        // 1. マテリアル用のフォルダ確認
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // 青・赤のマテリアルを作成または取得
        Material matBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
        if (matBlue == null)
        {
            matBlue = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            matBlue.color = Color.cyan;
            AssetDatabase.CreateAsset(matBlue, "Assets/Materials/Mat_Player.mat");
        }

        Material matRed = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Enemy.mat");
        if (matRed == null)
        {
            matRed = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            matRed.color = Color.red;
            AssetDatabase.CreateAsset(matRed, "Assets/Materials/Mat_Enemy.mat");
        }

        // 2. 味方ユニットの作成（手前に配置）
        GameObject playerUnit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerUnit.name = "Unit_Player";
        playerUnit.transform.position = new Vector3(0f, 1f, -10f);
        playerUnit.GetComponent<MeshRenderer>().sharedMaterial = matBlue;

        // Unitコンポーネントを追加
        var playerComp = playerUnit.AddComponent<Unit>();
        // TeamをPlayerに（デフォルト）

        // 3. 敵ユニットの作成（奥に配置）
        GameObject enemyUnit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyUnit.name = "Unit_Enemy";
        enemyUnit.transform.position = new Vector3(0f, 1f, 10f);
        enemyUnit.GetComponent<MeshRenderer>().sharedMaterial = matRed;

        var enemyComp = enemyUnit.AddComponent<Unit>();
        // リフレクションでTeamをEnemyに切り替え
        var field = typeof(Unit).GetField("team", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(enemyComp, Unit.Team.Enemy);
        }

        EditorUtility.DisplayDialog("完了", "味方（青）と敵（赤）ユニットをシーンに配置しました！\n上部の再生ボタン（▶）を押して戦闘を確認してください。", "OK");
    }
    // --- プレハブ化＆バトルマネージャー自動セットアップ機能 ---
    [MenuItem("Tools/BraveLegion/Setup Battle System")]
    public static void SetupBattleSystem()
    {
        // 1. Prefabs フォルダの作成確認
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // マテリアル取得
        Material matBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
        Material matRed = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Enemy.mat");

        // 2. 味方ユニットプレハブの生成
        string playerPrefabPath = "Assets/Prefabs/Unit_Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab == null)
        {
            GameObject pObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pObj.name = "Unit_Player";
            if (matBlue != null) pObj.GetComponent<MeshRenderer>().sharedMaterial = matBlue;
            pObj.AddComponent<Unit>();
            playerPrefab = PrefabUtility.SaveAsPrefabAsset(pObj, playerPrefabPath);
            DestroyImmediate(pObj);
        }

        // 3. 敵ユニットプレハブの生成
        string enemyPrefabPath = "Assets/Prefabs/Unit_Enemy.prefab";
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);
        if (enemyPrefab == null)
        {
            GameObject eObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            eObj.name = "Unit_Enemy";
            if (matRed != null) eObj.GetComponent<MeshRenderer>().sharedMaterial = matRed;
            var unitComp = eObj.AddComponent<Unit>();

            // TeamをEnemyに設定
            var field = typeof(Unit).GetField("team", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(unitComp, Unit.Team.Enemy);

            enemyPrefab = PrefabUtility.SaveAsPrefabAsset(eObj, enemyPrefabPath);
            DestroyImmediate(eObj);
        }

        // 4. 既存のテスト用シーン上カプセルがあれば削除
        GameObject oldPlayer = GameObject.Find("Unit_Player");
        if (oldPlayer != null && !PrefabUtility.IsPartOfPrefabAsset(oldPlayer)) DestroyImmediate(oldPlayer);
        GameObject oldEnemy = GameObject.Find("Unit_Enemy");
        if (oldEnemy != null && !PrefabUtility.IsPartOfPrefabAsset(oldEnemy)) DestroyImmediate(oldEnemy);

        // 5. 出撃ポイント（Transform）の作成
        GameObject pSpawn = GameObject.Find("PlayerSpawnPoint");
        if (pSpawn == null)
        {
            pSpawn = new GameObject("PlayerSpawnPoint");
        }
        pSpawn.transform.position = new Vector3(0f, 1f, -12f);

        GameObject eSpawn = GameObject.Find("EnemySpawnPoint");
        if (eSpawn == null)
        {
            eSpawn = new GameObject("EnemySpawnPoint");
        }
        eSpawn.transform.position = new Vector3(0f, 1f, 12f);

        // 6. BattleManager オブジェクトの作成とセットアップ
        GameObject bmObj = GameObject.Find("BattleManager");
        if (bmObj == null)
        {
            bmObj = new GameObject("BattleManager");
        }

        // Assembly-CSharp から BattleManager 型を検索
        Type bmType = Type.GetType("BattleManager, Assembly-CSharp");
        if (bmType == null)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                bmType = assembly.GetType("BattleManager");
                if (bmType != null) break;
            }
        }

        if (bmType != null)
        {
            var bmComp = bmObj.GetComponent(bmType);
            if (bmComp == null) bmComp = bmObj.AddComponent(bmType);

            // フィールド割り当て（大文字小文字対応）
            AssignField(bmComp, new[] { "playerPrefab", "playerUnitPrefab" }, playerPrefab);
            AssignField(bmComp, new[] { "enemyPrefab", "enemyUnitPrefab" }, enemyPrefab);
            AssignField(bmComp, new[] { "playerSpawnPoint", "playerSpawnTransform" }, pSpawn.transform);
            AssignField(bmComp, new[] { "enemySpawnPoint", "enemySpawnTransform" }, eSpawn.transform);
            
            EditorUtility.SetDirty(bmComp);
        }
        else
        {
            EditorUtility.DisplayDialog("注意", "先にGemini Assistantから『BattleManager.cs』を作成・保存してください。", "OK");
            return;
        }

        EditorUtility.DisplayDialog("セットアップ完了", "・味方/敵のプレハブ化 (Assets/Prefabs/)\n・出撃ポイントの作成\n・BattleManagerへの自動割り当て\nがすべて完了しました！\n\n再生ボタン（▶）を押してプレイできます。", "OK");
    }

    private static void AssignField(object target, string[] candidateNames, object value)
    {
        var type = target.GetType();
        foreach (var name in candidateNames)
        {
            var field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
        }
    }
}