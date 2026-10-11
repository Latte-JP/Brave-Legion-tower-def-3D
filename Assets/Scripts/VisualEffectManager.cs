using UnityEngine;

public class VisualEffectManager : MonoBehaviour
{
    public static VisualEffectManager Instance { get; private set; }

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
    }

    /// <summary>
    /// メテオ着弾時の大爆発（火花スパーク＋黒煙スモーク＋閃光）
    /// </summary>
    public void PlayMeteorExplosion(Vector3 position)
    {
        GameObject fxObj = new GameObject("VFX_MeteorExplosion");
        fxObj.transform.position = position;

        // 1. 火花スパーク
        ParticleSystem sparks = fxObj.AddComponent<ParticleSystem>();
        var main = sparks.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 12f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.45f, 0.1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = sparks.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });
        emission.rateOverTime = 0;

        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 1.0f;

        // 2. 立ち上る黒煙
        GameObject smokeObj = new GameObject("Smoke");
        smokeObj.transform.SetParent(fxObj.transform, false);
        ParticleSystem smoke = smokeObj.AddComponent<ParticleSystem>();
        var sMain = smoke.main;
        sMain.startLifetime = 1.2f;
        sMain.startSpeed = 4f;
        sMain.startSize = 1.8f;
        sMain.startColor = new Color(0.18f, 0.18f, 0.2f, 0.7f);

        var sEmission = smoke.emission;
        sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.05f, 25) });
        sEmission.rateOverTime = 0;

        var sShape = smoke.shape;
        sShape.shapeType = ParticleSystemShapeType.Cone;
        sShape.angle = 20f;
        sShape.radius = 0.8f;

        // 閃光ライト（一瞬だけ明るく照らす）
        GameObject lightObj = new GameObject("ExplosionLight");
        lightObj.transform.SetParent(fxObj.transform, false);
        Light expLight = lightObj.AddComponent<Light>();
        expLight.type = LightType.Point;
        expLight.color = new Color(1f, 0.5f, 0.1f);
        expLight.range = 14f;
        expLight.intensity = 4.5f;

        Destroy(fxObj, 1.6f);
    }

    /// <summary>
    /// 着弾時の小スパーク（タワー砲弾・矢）
    /// </summary>
    public void PlayHitSpark(Vector3 position, Color sparkColor)
    {
        GameObject sparkObj = new GameObject("VFX_HitSpark");
        sparkObj.transform.position = position;

        ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.35f;
        main.startSpeed = 6f;
        main.startSize = 0.2f;
        main.startColor = sparkColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        Destroy(sparkObj, 0.5f);
    }
    /// <summary>
    /// プリーストの回復光エフェクト
    /// </summary>
    public void PlayHealEffect(Vector3 position)
    {
        GameObject healObj = new GameObject("VFX_Heal");
        healObj.transform.position = position + Vector3.up * 0.5f;

        ParticleSystem ps = healObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 2.5f;
        main.startSize = 0.25f;
        main.startColor = new Color(0.2f, 1f, 0.4f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;

        Destroy(healObj, 0.8f);
    }
}