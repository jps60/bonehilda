using UnityEngine;

namespace Bonehilda;

internal sealed class CandleFlame : MonoBehaviour
{
    private Light? glow;
    private float seed;

    private void Start()
    {
        glow = GetComponentInChildren<Light>();
        seed = Mathf.Repeat(GetInstanceID() * .618f, 80f);
    }

    private void Update()
    {
        if (glow == null) return;
        var time = Time.time;
        var gust = Mathf.PerlinNoise(seed, time * 3.2f) - .5f;
        var flutter = Mathf.PerlinNoise(time * 7.3f, seed + 17f) - .5f;
        transform.localRotation = Quaternion.Euler(12f * flutter, 0, 27f * gust);
        transform.localScale = new Vector3(1f - .20f * flutter, 1f + .36f * flutter, 1f - .20f * flutter);
        glow.intensity = .85f + .43f * flutter + .16f * gust;
    }
}
