using UnityEngine;

namespace Bonehilda;

internal sealed class FeatherDust
{
    private static Material? dustMaterial;
    private readonly ParticleSystem particles;
    private long bout;
    private int lastTurn;

    private FeatherDust(Transform maid, Material material)
    {
        particles = Visuals.Child(maid, "BH_FeatherDust").gameObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 18;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.30f, .52f);
        main.startSize = new ParticleSystem.MinMaxCurve(.11f, .18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.startColor = new Color(.79f, .73f, .65f, .78f);
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(.30f, 0), new GradientAlphaKey(1, .16f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        particles.Play();
    }

    internal static FeatherDust? Create(Transform maid)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
        if (dustMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                Plugin.Instance.LogWarning("Dust puffs are unavailable because Unity could not load Sprites/Default.");
                return null;
            }
            dustMaterial = new Material(shader) {
                name = "BH feather dust", mainTexture = ModelArt.Texture("feather-dust", false)
            };
        }
        return new FeatherDust(maid, dustMaterial);
    }

    internal void Sweep(long start, float cycles, Vector3 brush, Vector3 surface, Vector3 sideways)
    {
        var turn = Mathf.FloorToInt(2f * cycles + .5f);
        if (start != bout)
        {
            bout = start;
            lastTurn = turn;
            return;
        }
        if (turn == lastTurn) return;
        lastTurn = turn;
        if (turn % 3 != 1 || Mathf.Abs(brush.y - surface.y) > .14f
            || Vector3.ProjectOnPlane(brush - surface, Vector3.up).sqrMagnitude > .25f) return;
        var origin = new Vector3(brush.x, surface.y + .055f, brush.z);
        for (var i = 0; i < 5; i++)
        {
            var drift = sideways * ((turn % 2 == 1 ? 1 : -1) * (.23f + .065f * i));
            var puff = new ParticleSystem.EmitParams {
                position = origin + sideways * ((i - 2) * .025f),
                velocity = drift + Vector3.up * (.20f + .055f * (i % 3))
            };
            particles.Emit(puff, 1);
        }
    }
}
