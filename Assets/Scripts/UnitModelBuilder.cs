using UnityEngine;

public static class UnitModelBuilder
{
    private static Shader DefaultShader => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

    public static void BuildUnitModel(GameObject root, BattleManager.UnitType type, Unit.Team team)
    {
        MeshRenderer baseRend = root.GetComponent<MeshRenderer>();
        if (baseRend != null) baseRend.enabled = false;

        GameObject modelRoot = new GameObject("VisualModel");
        modelRoot.transform.SetParent(root.transform, false);

        Color mainColor = (team == Unit.Team.Player) ? new Color(0.2f, 0.55f, 0.95f) : new Color(0.9f, 0.25f, 0.2f);
        Color armorColor = new Color(0.7f, 0.72f, 0.75f);
        Color woodColor = new Color(0.45f, 0.3f, 0.15f);
        Color skinColor = new Color(0.95f, 0.8f, 0.7f);

        switch (type)
        {
            case BattleManager.UnitType.Soldier:
                CreateHumanoidBase(modelRoot.transform, mainColor, armorColor, skinColor);
                GameObject sHilt = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0.45f, 0.7f, 0.3f), new Vector3(0.08f, 0.15f, 0.08f), woodColor);
                CreatePart(PrimitiveType.Cube, sHilt.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 0.8f, 0.04f), armorColor);
                sHilt.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(-0.42f, 0.75f, 0.25f), new Vector3(0.1f, 0.55f, 0.45f), mainColor * 0.8f);
                break;

            case BattleManager.UnitType.Archer:
                CreateHumanoidBase(modelRoot.transform, mainColor, armorColor, skinColor);
                GameObject bow = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(-0.45f, 0.8f, 0.35f), new Vector3(0.08f, 0.9f, 0.08f), woodColor);
                bow.transform.localRotation = Quaternion.Euler(15f, 20f, -10f);
                GameObject quiver = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0.15f, 0.85f, -0.28f), new Vector3(0.18f, 0.55f, 0.18f), woodColor);
                quiver.transform.localRotation = Quaternion.Euler(-25f, 0f, 20f);
                break;

            case BattleManager.UnitType.Tank:
                CreateHumanoidBase(modelRoot.transform, mainColor, armorColor, skinColor);
                CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0.42f, 1.05f, 0f), new Vector3(0.35f, 0.25f, 0.4f), armorColor);
                CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(-0.42f, 1.05f, 0f), new Vector3(0.35f, 0.25f, 0.4f), armorColor);
                GameObject bigShield = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0f, 0.65f, 0.55f), new Vector3(0.85f, 1.15f, 0.14f), armorColor * 0.9f);
                CreatePart(PrimitiveType.Cube, bigShield.transform, new Vector3(0f, 0f, 0.06f), new Vector3(0.35f, 0.35f, 0.08f), mainColor);
                break;

            case BattleManager.UnitType.Wyvern:
                Color dragonCol = (team == Unit.Team.Player) ? new Color(0.15f, 0.45f, 0.85f) : new Color(0.85f, 0.2f, 0.15f);
                GameObject dBody = CreatePart(PrimitiveType.Sphere, modelRoot.transform, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.5f, 1.1f), dragonCol);
                GameObject dHead = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0f, 0.3f, 0.65f), new Vector3(0.35f, 0.35f, 0.55f), dragonCol * 1.1f);
                CreatePart(PrimitiveType.Cube, dHead.transform, new Vector3(0.12f, 0.25f, -0.1f), new Vector3(0.08f, 0.25f, 0.08f), Color.white);
                CreatePart(PrimitiveType.Cube, dHead.transform, new Vector3(-0.12f, 0.25f, -0.1f), new Vector3(0.08f, 0.25f, 0.08f), Color.white);
                GameObject leftWing = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(-0.95f, 0.15f, 0.1f), new Vector3(1.2f, 0.05f, 0.65f), dragonCol * 0.85f);
                leftWing.transform.localRotation = Quaternion.Euler(0f, -10f, 15f);
                GameObject rightWing = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0.95f, 0.15f, 0.1f), new Vector3(1.2f, 0.05f, 0.65f), dragonCol * 0.85f);
                rightWing.transform.localRotation = Quaternion.Euler(0f, 10f, -15f);
                GameObject tail = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0f, -0.05f, -0.75f), new Vector3(0.12f, 0.5f, 0.12f), dragonCol);
                tail.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
                break;

            case BattleManager.UnitType.Priest:
                Color robeCol = new Color(0.95f, 0.95f, 0.95f);
                Color goldCol = new Color(1f, 0.85f, 0.2f);
                CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.5f, 0.55f), robeCol);
                GameObject pHead = CreatePart(PrimitiveType.Sphere, modelRoot.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.42f, 0.42f, 0.42f), skinColor);
                CreatePart(PrimitiveType.Cylinder, pHead.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.4f, 0.03f, 0.4f), goldCol);
                // woodCol -> woodColor に修正
                GameObject staff = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0.42f, 0.85f, 0.25f), new Vector3(0.06f, 0.95f, 0.06f), woodColor);
                CreatePart(PrimitiveType.Sphere, staff.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.22f, 0.22f, 0.22f), new Color(0.3f, 1f, 0.5f));
                break;

            case BattleManager.UnitType.Boss:
                CreateHumanoidBase(modelRoot.transform, Color.black, armorColor, skinColor);
                GameObject bHead = modelRoot.transform.Find("Head")?.gameObject;
                if (bHead != null)
                {
                    CreatePart(PrimitiveType.Cube, bHead.transform, new Vector3(0.25f, 0.25f, 0f), new Vector3(0.15f, 0.4f, 0.15f), Color.red);
                    CreatePart(PrimitiveType.Cube, bHead.transform, new Vector3(-0.25f, 0.25f, 0f), new Vector3(0.15f, 0.4f, 0.15f), Color.red);
                }
                GameObject hammer = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0.7f, 0.8f, 0.4f), new Vector3(0.14f, 1.4f, 0.14f), woodColor);
                hammer.transform.localRotation = Quaternion.Euler(35f, 0f, 15f);
                CreatePart(PrimitiveType.Cube, hammer.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 0.4f, 0.4f), new Color(0.2f, 0.2f, 0.25f));
                break;
        }
    }

    private static void CreateHumanoidBase(Transform root, Color bodyCol, Color helmCol, Color skinCol)
    {
        CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.45f, 0.45f), bodyCol);
        GameObject head = CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 1.35f, 0f), new Vector3(0.45f, 0.45f, 0.45f), skinCol);
        head.name = "Head";
        CreatePart(PrimitiveType.Cube, head.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.48f, 0.25f, 0.48f), helmCol);
    }

    public static void BuildCastleTower(GameObject towerObj, Unit.Team team)
    {
        MeshRenderer baseRend = towerObj.GetComponent<MeshRenderer>();
        if (baseRend != null) baseRend.enabled = false;

        GameObject modelRoot = new GameObject("CastleTowerModel");
        modelRoot.transform.SetParent(towerObj.transform, false);

        Color stoneDark = new Color(0.35f, 0.37f, 0.4f);
        Color stoneLight = new Color(0.55f, 0.57f, 0.6f);
        Color teamFlagColor = (team == Unit.Team.Player) ? new Color(0.2f, 0.6f, 1.0f) : new Color(0.95f, 0.2f, 0.2f);

        GameObject mainTower = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0f, 1.2f, 0f), new Vector3(1.9f, 2.4f, 1.9f), stoneLight);
        GameObject deck = CreatePart(PrimitiveType.Cube, modelRoot.transform, new Vector3(0f, 2.45f, 0f), new Vector3(2.2f, 0.25f, 2.2f), stoneDark);

        float d = 0.95f;
        Vector3[] battlementPos = {
            new Vector3(d, 2.7f, d), new Vector3(-d, 2.7f, d), new Vector3(d, 2.7f, -d), new Vector3(-d, 2.7f, -d),
            new Vector3(0f, 2.7f, d), new Vector3(0f, 2.7f, -d), new Vector3(d, 2.7f, 0f), new Vector3(-d, 2.7f, 0f)
        };

        foreach (var bp in battlementPos)
        {
            CreatePart(PrimitiveType.Cube, modelRoot.transform, bp, new Vector3(0.38f, 0.32f, 0.38f), stoneDark);
        }

        GameObject pole = CreatePart(PrimitiveType.Cylinder, modelRoot.transform, new Vector3(0.7f, 3.2f, 0f), new Vector3(0.05f, 0.5f, 0.05f), Color.gray);
        GameObject banner = CreatePart(PrimitiveType.Cube, pole.transform, new Vector3(0.2f, 0.12f, 0f), new Vector3(0.4f, 0.25f, 0.03f), teamFlagColor);
    }

    private static GameObject CreatePart(PrimitiveType prim, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(prim);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;

        Collider c = part.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);

        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(DefaultShader);
            mat.color = color;
            rend.material = mat;
        }
        return part;
    }
}