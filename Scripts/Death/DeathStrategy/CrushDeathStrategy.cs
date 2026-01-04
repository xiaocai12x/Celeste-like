using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Death Strategy/Crush (Procedural Fixed)")]
public class CrushDeathStrategy : DeathStrategy
{
    [Header("Iris Transition")]
    public Material irisMaterial;
    public float shrinkDuration = 0.3f;
    public float expandDuration = 0.4f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float centerOffsetY = 0.5f;

    [Header("Crush Settings")]
    public float hitStopDuration = 0.1f;
    public float crushAnimDuration = 0.1f;
    public AnimationCurve crushCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public float waitBeforeHide = 0.2f;

    private readonly Vector3 squashedScale = new Vector3(1.8f, 0.1f, 1f);
    private Material runtimeMaterial;

    public override IEnumerator ExecuteDeath(PlayerController player, Vector3 crushDir)
    {
        AudioSource audio = player.GetComponent<AudioSource>();
        var effect = player.GetComponent<PlayerDeathEffect>();

        if (player.RB != null)
        {
            player.RB.linearVelocity = Vector2.zero;
            player.RB.simulated = false;
        }

        if (hitSFX != null && audio != null) audio.PlayOneShot(hitSFX);
        yield return new WaitForSecondsRealtime(hitStopDuration);
        if (deathSFX != null && audio != null) audio.PlayOneShot(deathSFX);

        Vector3 startScale = player.transform.localScale;
        Vector3 startPos = player.transform.position;
        float timer = 0f;

        while (timer < crushAnimDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / crushAnimDuration);
            float curveT = crushCurve.Evaluate(t);
            Vector3 currentScale = Vector3.Lerp(startScale, squashedScale, curveT);
            player.transform.localScale = currentScale;
            float heightDiff = startScale.y - currentScale.y;
            player.transform.position = startPos - new Vector3(0, heightDiff * 0.5f, 0);
            yield return null;
        }

        player.transform.localScale = squashedScale;
        float finalHeightDiff = startScale.y - squashedScale.y;
        player.transform.position = startPos - new Vector3(0, finalHeightDiff * 0.5f, 0);
        yield return new WaitForSecondsRealtime(waitBeforeHide);

        if (effect != null)
        {
            Color explodeColor = Color.white;
            var hair = player.GetComponentInChildren<HairController>();
            if (hair != null) explodeColor = hair.CurrentHairColor;
            else if (player.SR != null) explodeColor = player.SR.color;
            effect.PlayExplode(player.transform.position, explodeColor);
        }

        player.SetVisualState(false);
        player.transform.localScale = Vector3.one;
        player.transform.rotation = Quaternion.identity;
    }

    public override IEnumerator TransitionIn(GameManager gm)
    {
        if (irisMaterial == null) yield break;
        if (runtimeMaterial == null) runtimeMaterial = new Material(irisMaterial);
        gm.transitionImage.material = runtimeMaterial;
        gm.transitionImage.enabled = true;

        float timer = 0f;
        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / shrinkDuration);
            if (gm.player != null) UpdateShaderCenter(gm.player.transform.position);
            float r = Mathf.Lerp(2.5f, 0f, transitionCurve.Evaluate(t));
            runtimeMaterial.SetFloat("_Radius", r);
            yield return null;
        }
        runtimeMaterial.SetFloat("_Radius", 0f);
    }

    public override IEnumerator ExecuteRespawn(PlayerController player, Vector2 spawnPos)
    {
        player.transform.position = spawnPos;
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        if (player.RB != null)
        {
            player.RB.linearVelocity = Vector2.zero;
            player.RB.simulated = false;
        }

        player.SetVisualState(false);
        yield return null;
    }

    public override IEnumerator TransitionOut(GameManager gm)
    {
        float timer = 0f;
        while (timer < expandDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / expandDuration);
            if (gm.player != null) UpdateShaderCenter(gm.player.transform.position);
            float r = Mathf.Lerp(0f, 2.5f, transitionCurve.Evaluate(t));
            runtimeMaterial.SetFloat("_Radius", r);
            yield return null;
        }

        if (gm.player != null)
        {
            if (respawnSFX != null && gm.audioSource != null)
                gm.audioSource.PlayOneShot(respawnSFX);

            var effect = gm.player.GetComponent<PlayerDeathEffect>();
            if (effect != null)
            {
                Color normalColor = new Color(0.62f, 0.52f, 0.52f, 1f);
                bool done = false;
                yield return gm.player.StartCoroutine(effect.PlayReformRoutine(
                    gm.player.transform.position,
                    normalColor,
                    () => done = true
                ));
                while (!done) yield return null;
            }

            gm.player.SetVisualState(true);
            gm.player.ReviveInternal();
        }

        if (gm.transitionImage != null) gm.transitionImage.enabled = false;
    }

    private void UpdateShaderCenter(Vector3 worldPos)
    {
        if (Camera.main == null || runtimeMaterial == null) return;
        Vector3 targetPos = worldPos;
        targetPos.y += centerOffsetY;
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(targetPos);
        runtimeMaterial.SetVector("_Center", new Vector2(viewportPos.x, viewportPos.y));
    }
}