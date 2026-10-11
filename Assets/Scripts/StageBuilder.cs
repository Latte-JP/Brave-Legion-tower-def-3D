using UnityEngine;
using Unity.AI.Navigation; // 1. 追加：NavMeshSurface用名前空間

public class StageBuilder : MonoBehaviour
{
    private void Awake()
    {
        BuildEnvironment();
        OptimizeLighting();
    }

    private void BuildEnvironment()
    {
        GameObject oldEnv = GameObject.Find("Diorama_Environment");
        if (oldEnv != null) DestroyImmediate(oldEnv);

        GameObject envRoot = new GameObject("Diorama_Environment");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. ベース地面（芝生エリア：Z長さを 32 -> 56 に拡張）
        GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grass.name = "Ground_Grass";
        grass.transform.SetParent(envRoot.transform);
        grass.transform.position = new Vector3(0f, -0.5f, 0f);
        grass.transform.localScale = new Vector3(30f, 1f, 56f);
        SetMaterial(grass, shader, new Color(0.28f, 0.52f, 0.22f));

        // 2. 中央進軍レーン（Z長さを 26 -> 50 に拡張）
        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Main_Road";
        path.transform.SetParent(envRoot.transform);
        path.transform.position = new Vector3(0f, 0.02f, 0f);
        path.transform.localScale = new Vector3(6.0f, 0.06f, 50f);
        SetMaterial(path, shader, new Color(0.68f, 0.62f, 0.52f));

        // レーンの縁石ライン（左右）
        CreateCurbs(envRoot.transform, shader);

        // 3. タワー台座（タワー間隔2倍：Z = -20 と Z = +20）
        CreateTowerBase(envRoot.transform, shader, new Vector3(0f, 0.15f, -20f), new Color(0.35f, 0.45f, 0.6f)); // 味方台座
        CreateTowerBase(envRoot.transform, shader, new Vector3(0f, 0.15f, 20f), new Color(0.6f, 0.35f, 0.35f));   // 敵台座

        // 4. 木々の配置
        CreateTrees(envRoot.transform, shader);

        // 5. 岩・小石の配置
        CreateRocks(envRoot.transform, shader);

        // 6. 木の柵（フェンス）
        CreateFences(envRoot.transform, shader);

        // ==============================================================
        // 2. 追加：ステージ全体を対象に NavMesh をコードから自動ベイク
        // ==============================================================
        NavMeshSurface surface = envRoot.AddComponent<NavMeshSurface>();
        // 木や岩・柵などは RemoveCollider 済みなので、歩行面である地面とレーンだけが綺麗にベイクされます
        surface.collectObjects = CollectObjects.Children; // envRoot配下のオブジェクトを収集
        surface.BuildNavMesh(); // 即座にベイク実行
    }

    private void CreateCurbs(Transform parent, Shader shader)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Road_Curb";
            curb.transform.SetParent(parent);
            curb.transform.position = new Vector3(side * 3.1f, 0.08f, 0f);
            curb.transform.localScale = new Vector3(0.25f, 0.12f, 50f);
            SetMaterial(curb, shader, new Color(0.45f, 0.42f, 0.38f));
        }
    }

    private void CreateTowerBase(Transform parent, Shader shader, Vector3 pos, Color color)
    {
        GameObject step1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step1.transform.SetParent(parent);
        step1.transform.position = pos;
        step1.transform.localScale = new Vector3(4.6f, 0.3f, 4.6f);
        SetMaterial(step1, shader, new Color(0.35f, 0.35f, 0.38f));

        GameObject step2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step2.transform.SetParent(parent);
        step2.transform.position = pos + Vector3.up * 0.2f;
        step2.transform.localScale = new Vector3(3.6f, 0.25f, 3.6f);
        SetMaterial(step2, shader, color);
    }

    private void CreateTrees(Transform parent, Shader shader)
    {
        float[] xCoords = { -7.0f, -9.5f, -6.5f, -10.0f, -7.5f, -9.0f, 7.0f, 9.5f, 6.8f, 10.0f, 7.5f, 9.2f };
        float[] zCoords = { -16f, -10f, -3f, 5f, 12f, 18f, -17f, -8f, 0f, 7f, 14f, 19f };

        for (int i = 0; i < xCoords.Length; i++)
        {
            Vector3 pos = new Vector3(xCoords[i] + Random.Range(-0.5f, 0.5f), 0f, zCoords[i] + Random.Range(-0.8f, 0.8f));
            CreateSingleTree(parent, shader, pos);
        }
    }

    private void CreateSingleTree(Transform parent, Shader shader, Vector3 basePos)
    {
        GameObject tree = new GameObject("Diorama_Tree");
        tree.transform.SetParent(parent);
        tree.transform.position = basePos;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        trunk.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
        SetMaterial(trunk, shader, new Color(0.38f, 0.24f, 0.14f));
        RemoveCollider(trunk);

        Color leafColor = (Random.value > 0.5f) ? new Color(0.2f, 0.58f, 0.26f) : new Color(0.32f, 0.65f, 0.25f);

        GameObject leafBottom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leafBottom.transform.SetParent(tree.transform);
        leafBottom.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        leafBottom.transform.localScale = new Vector3(1.6f, 1.4f, 1.6f);
        SetMaterial(leafBottom, shader, leafColor);
        RemoveCollider(leafBottom);

        GameObject leafTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leafTop.transform.SetParent(tree.transform);
        leafTop.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        leafTop.transform.localScale = new Vector3(1.2f, 1.1f, 1.2f);
        SetMaterial(leafTop, shader, leafColor * 1.1f);
        RemoveCollider(leafTop);
    }

    private void CreateRocks(Transform parent, Shader shader)
    {
        Vector3[] rockPositions = {
            new Vector3(-5.0f, 0.2f, -14f),
            new Vector3(-5.5f, 0.15f, -6f),
            new Vector3(-4.8f, 0.2f, 8f),
            new Vector3(-5.2f, 0.25f, 16f),
            new Vector3(5.2f, 0.25f, -12f),
            new Vector3(5.0f, 0.18f, -2f),
            new Vector3(5.4f, 0.2f, 11f),
            new Vector3(4.8f, 0.3f, 17f)
        };

        foreach (var pos in rockPositions)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Rock";
            rock.transform.SetParent(parent);
            rock.transform.position = pos;
            rock.transform.rotation = Quaternion.Euler(Random.Range(10f, 40f), Random.Range(0f, 360f), Random.Range(10f, 40f));
            rock.transform.localScale = Vector3.one * Random.Range(0.7f, 1.2f);
            SetMaterial(rock, shader, new Color(0.5f, 0.52f, 0.55f));
            RemoveCollider(rock);
        }
    }

    private void CreateFences(Transform parent, Shader shader)
    {
        float[] fenceZ = { -16f, -12f, -8f, -4f, 0f, 4f, 8f, 12f, 16f };

        foreach (float z in fenceZ)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Fence_Post";
                post.transform.SetParent(parent);
                post.transform.position = new Vector3(side * 3.6f, 0.35f, z);
                post.transform.localScale = new Vector3(0.18f, 0.7f, 0.18f);
                SetMaterial(post, shader, new Color(0.48f, 0.32f, 0.18f));
                RemoveCollider(post);

                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Fence_Rail";
                rail.transform.SetParent(parent);
                rail.transform.position = new Vector3(side * 3.6f, 0.5f, z);
                rail.transform.localScale = new Vector3(0.12f, 0.14f, 2.8f);
                SetMaterial(rail, shader, new Color(0.52f, 0.36f, 0.22f));
                RemoveCollider(rail);
            }
        }
    }

    private void OptimizeLighting()
    {
        Light dirLight = FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = new Color(1.0f, 0.95f, 0.86f);
            dirLight.intensity = 1.15f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.55f;
        }

        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);
    }

    private void SetMaterial(GameObject obj, Shader shader, Color color)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            rend.material = mat;
        }
    }

    private void RemoveCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }
}