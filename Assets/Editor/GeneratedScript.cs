using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

/// <summary>
/// タワー（プレイヤー拠点・敵拠点）をシーン上に自動配置・初期化するエディタ拡張ツール。
/// </summary>
public static class TowerSetupTool
{
    private const string MenuPath = "Tools/BraveLegion/Setup Towers";
    private const string PlayerTowerName = "Tower_Player";
    private const string EnemyTowerName = "Tower_Enemy";
    private const string PlayerMatPath = "Assets/Materials/Mat_Player.mat";
    private const string EnemyMatPath = "Assets/Materials/Mat_Enemy.mat";

    [MenuItem(MenuPath)]
    public static void SetupTowers()
    {
        // 1. 既存タワーの重複クリーンアップ（存在すれば削除）
        RemoveExistingTower(PlayerTowerName);
        RemoveExistingTower(EnemyTowerName);

        // 2. 味方タワーの生成とセットアップ
        CreateTower(
            towerName: PlayerTowerName,
            position: new Vector3(0f, 1.5f, -12f),
            scale: new Vector3(2f, 3f, 2f),
            materialPath: PlayerMatPath,
            teamValue: 0 // 通常 Unit.Team.Player = 0
        );

        // 3. 敵タワーの生成とセットアップ
        CreateTower(
            towerName: EnemyTowerName,
            position: new Vector3(0f, 1.5f, 12f),
            scale: new Vector3(2f, 3f, 2f),
            materialPath: EnemyMatPath,
            teamValue: 1 // 通常 Unit.Team.Enemy = 1
        );

        // シーンに変更があったことをマーク
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 完了通知ダイアログ
        EditorUtility.DisplayDialog(
            "セットアップ完了",
            "タワーの自動配置が完了しました！",
            "OK"
        );
    }

    /// <summary>
    /// シーン内の既存同名オブジェクトを削除して重複を防止します。
    /// </summary>
    private static void RemoveExistingTower(string objectName)
    {
        GameObject existingObj = GameObject.Find(objectName);
        if (existingObj != null)
        {
            Undo.DestroyObjectImmediate(existingObj);
        }
    }

    /// <summary>
    /// 指定パラメータでタワーオブジェクトを生成・初期化します。
    /// </summary>
    private static void CreateTower(string towerName, Vector3 position, Vector3 scale, string materialPath, int teamValue)
    {
        // 基本形状(Cube)の生成
        GameObject towerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        towerObj.name = towerName;
        towerObj.transform.position = position;
        towerObj.transform.localScale = scale;

        Undo.RegisterCreatedObjectUndo(towerObj, $"Create {towerName}");

        // マテリアルの適用
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat != null)
        {
            MeshRenderer renderer = towerObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }
        else
        {
            Debug.LogWarning($"[TowerSetupTool] マテリアルが見つかりませんでした: {materialPath}");
        }

        // NavMeshObstacle コンポーネントの設定
        NavMeshObstacle obstacle = towerObj.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = towerObj.AddComponent<NavMeshObstacle>();
        }
        obstacle.carving = true;

        // Unit コンポーネントのアタッチと値の設定
        SetupUnitComponent(towerObj, teamValue, isTower: true, maxHp: 500f);
    }

    /// <summary>
    /// SerializedObject およびリフレクションを活用して Unit コンポーネントの変数を確実に設定します。
    /// </summary>
    private static void SetupUnitComponent(GameObject target, int teamValue, bool isTower, float maxHp)
    {
        // "Unit" 型が存在するか検索して動的にアタッチ・設定
        System.Type unitType = System.Type.GetType("Unit, Assembly-CSharp") ?? System.Type.GetType("Unit");

        if (unitType == null)
        {
            Debug.LogWarning("[TowerSetupTool] 'Unit' スクリプトがプロジェクト内に見つかりません。コンポーネントのアタッチをスキップしました。");
            return;
        }

        Component unitComponent = target.GetComponent(unitType);
        if (unitComponent == null)
        {
            unitComponent = target.AddComponent(unitType);
        }

        // SerializedObject でシリアライズされたフィールドを安全に更新
        SerializedObject serializedObj = new SerializedObject(unitComponent);
        serializedObj.Update();

        // team の設定（enum / int）
        SerializedProperty teamProp = serializedObj.FindProperty("team");
        if (teamProp != null)
        {
            teamProp.enumValueIndex = teamValue;
        }

        // isTower の設定
        SerializedProperty isTowerProp = serializedObj.FindProperty("isTower");
        if (isTowerProp != null)
        {
            isTowerProp.boolValue = isTower;
        }

        // maxHp の設定
        SerializedProperty maxHpProp = serializedObj.FindProperty("maxHp");
        if (maxHpProp != null)
        {
            maxHpProp.floatValue = maxHp;
        }

        // 現在HPプロパティ(currentHp)が存在する場合の同期
        SerializedProperty currentHpProp = serializedObj.FindProperty("currentHp");
        if (currentHpProp != null)
        {
            currentHpProp.floatValue = maxHp;
        }

        serializedObj.ApplyModifiedProperties();

        // プライベートフィールド等で SerializedProperty に現れないケースへのリフレクション対応
        System.Reflection.FieldInfo teamField = unitType.GetField("team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (teamField != null && teamField.FieldType.IsEnum)
        {
            object enumVal = System.Enum.ToObject(teamField.FieldType, teamValue);
            teamField.SetValue(unitComponent, enumVal);
        }

        System.Reflection.FieldInfo isTowerField = unitType.GetField("isTower", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (isTowerField != null)
        {
            isTowerField.SetValue(unitComponent, isTower);
        }

        System.Reflection.FieldInfo maxHpField = unitType.GetField("maxHp", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (maxHpField != null)
        {
            maxHpField.SetValue(unitComponent, maxHp);
        }
    }
}