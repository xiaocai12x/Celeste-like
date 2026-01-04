using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CrumblePlatform : MonoBehaviour
{
    [SerializeField] private float totalLifetime = 0.7f;
    [SerializeField] private float warningThreshold = 0.5f;
    [SerializeField] private float resetDelay = 2.0f;
    [SerializeField] private AnimationCurve shakeIntensityCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

    [SerializeField] private float landSquashPixels = 1.5f;
    [SerializeField] private float jitterPixels = 1.0f;
    [SerializeField] private float fallSpeed = 5.0f;

    [SerializeField] private List<Transform> tiles;
    [SerializeField] private List<SpriteRenderer> tileRenderers = new List<SpriteRenderer>();
    [SerializeField] private ParticleSystem dustPS, crackPS, breakPS;
    [SerializeField] private float rebuildStagger = 0.06f;

    private Collider2D col;
    private List<Vector3> tilesInitialPos = new List<Vector3>();
    private bool isCrumbled = false;
    private const float PIXEL = 0.125f;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        tilesInitialPos.Clear();
        foreach (var t in tiles)
        {
            tilesInitialPos.Add(t.localPosition);
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && !tileRenderers.Contains(sr)) tileRenderers.Add(sr);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isCrumbled && collision.gameObject.CompareTag("Player"))
        {
            if (collision.contacts[0].normal.y < -0.5f)
                StartCoroutine(CrumbleRoutine());
        }
    }

    private IEnumerator CrumbleRoutine()
    {
        isCrumbled = true;
        float elapsed = 0f;

        if (dustPS) dustPS.Play();
        yield return StartCoroutine(ApplyImpactBounce());

        if (crackPS) crackPS.Play();
        var crackEmission = crackPS.emission;

        while (elapsed < totalLifetime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / totalLifetime;

            if (progress > warningThreshold)
            {
                float wp = (progress - warningThreshold) / (1f - warningThreshold);
                float intensity = shakeIntensityCurve.Evaluate(wp);
                ApplyGrindShake(intensity);
                crackEmission.rateOverTime = Mathf.Lerp(5f, 40f, intensity);
            }
            else
            {
                ApplyGrindShake(progress * 0.2f);
            }
            yield return null;
        }

        col.enabled = false;
        if (crackPS) { crackPS.Stop(); crackPS.Clear(); }
        if (breakPS) breakPS.Play();

        yield return StartCoroutine(VerticalCollapseAnimation());

        yield return new WaitForSeconds(resetDelay);
        yield return StartCoroutine(RebuildSequence());

        col.enabled = true;
        isCrumbled = false;
    }

    private IEnumerator ApplyImpactBounce()
    {
        float t = 0;
        float dur = 0.08f;
        while (t < 1)
        {
            t += Time.deltaTime / dur;
            float weight = Mathf.Sin(t * Mathf.PI);
            float currentSag = Mathf.Round(landSquashPixels * weight) * PIXEL;
            for (int i = 0; i < tiles.Count; i++)
                tiles[i].localPosition = tilesInitialPos[i] + Vector3.down * currentSag;
            yield return null;
        }
        for (int i = 0; i < tiles.Count; i++) tiles[i].localPosition = tilesInitialPos[i];
    }

    private void ApplyGrindShake(float intensity)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            float x = (Random.value > 0.5f ? 1 : -1) * (jitterPixels * PIXEL * intensity);
            float y = (Random.value > 0.8f ? -PIXEL : 0) * intensity;

            tiles[i].localPosition = tilesInitialPos[i] + new Vector3(Mathf.Round(x / PIXEL) * PIXEL, Mathf.Round(y / PIXEL) * PIXEL, 0);
        }
    }

    private IEnumerator VerticalCollapseAnimation()
    {
        float t = 0;
        float dur = 0.25f;

        float[] speeds = new float[tiles.Count];
        for (int i = 0; i < tiles.Count; i++) speeds[i] = Random.Range(0.8f, 1.2f);

        while (t < 1)
        {
            t += Time.deltaTime / dur;
            for (int i = 0; i < tiles.Count; i++)
            {
                float xJitter = (Random.value - 0.5f) * PIXEL * 0.5f;
                float yFall = Time.deltaTime * fallSpeed * speeds[i];

                tiles[i].localPosition += new Vector3(xJitter, -yFall, 0);

                if (i < tileRenderers.Count)
                {
                    Color c = tileRenderers[i].color;
                    c.a = Mathf.Lerp(1, 0, t * 1.5f);
                    tileRenderers[i].color = c;
                }
                tiles[i].localScale = new Vector3(0.9f, 1.1f, 1f) * (1 - t * 0.5f);
            }
            yield return null;
        }

        foreach (var sr in tileRenderers) sr.gameObject.SetActive(false);
    }

    private IEnumerator RebuildSequence()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            tileRenderers[i].gameObject.SetActive(true);
            tileRenderers[i].color = Color.white;
            tiles[i].localScale = Vector3.one;
            StartCoroutine(JuicePop(tiles[i], tilesInitialPos[i]));
            yield return new WaitForSeconds(rebuildStagger);
        }
    }

    private IEnumerator JuicePop(Transform t, Vector3 target)
    {
        Vector3 start = target + Vector3.down * PIXEL * 4;
        float elapsed = 0;
        float dur = 0.15f;
        while (elapsed < 1)
        {
            elapsed += Time.deltaTime / dur;
            float easedT = 1f - Mathf.Pow(1f - elapsed, 3);
            t.localPosition = Vector3.Lerp(start, target, easedT);
            float bounce = Mathf.Sin(elapsed * Mathf.PI);
            t.localScale = new Vector3(1 - bounce * 0.2f, 1 + bounce * 0.4f, 1);
            yield return null;
        }
        t.localPosition = target;
        t.localScale = Vector3.one;
    }
}