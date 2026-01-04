using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    public Transform visualsRoot;

    private PlayerController player;
    private Vector3 defaultScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private bool _lockDeformation = false;

    public float jumpStretchX = 0.75f;
    public float jumpStretchY = 1.25f;
    public float landSquashX = 1.35f;
    public float landSquashY = 0.65f;
    public float recoverSpeed = 20f;

    public float dashStretchLimitX = 0.4f;
    public float dashStretchLimitY = 1.6f;
    public float dashStopSquashX = 1.25f;
    public float dashStopSquashY = 0.8f;

    public float crushWide = 2.5f;
    public float crushThin = 0.1f;
    public float crushSpeed = 25f;

    private float lastLandTime;

    private void Start()
    {
        player = GetComponent<PlayerController>();
        if (visualsRoot == null)
        {
            if (transform.childCount > 0) visualsRoot = transform.GetChild(0);
            else Debug.LogError("【PlayerAnimator】找不到 Visuals 子物体！请手动赋值。");
        }
    }

    private void Update()
    {
        if (visualsRoot == null) return;

        if (_lockDeformation)
        {
            visualsRoot.localScale = Vector3.Lerp(visualsRoot.localScale, targetScale, Time.deltaTime * crushSpeed);
            return;
        }

        float currentRecoverSpeed = recoverSpeed;
        if (player.StateMachine != null && player.StateMachine.CurrentState == player.CutsceneState)
        {
            currentRecoverSpeed = 2f;
        }

        visualsRoot.localScale = Vector3.Lerp(visualsRoot.localScale, targetScale, Time.deltaTime * currentRecoverSpeed);

        if (Vector3.Distance(visualsRoot.localScale, targetScale) < 0.05f)
        {
            targetScale = defaultScale;
        }

        if (Vector3.Distance(visualsRoot.localScale, defaultScale) < 0.01f)
        {
            visualsRoot.localScale = defaultScale;
            targetScale = defaultScale;
        }
    }

    public void PlayJump()
    {
        visualsRoot.localScale = new Vector3(jumpStretchX, jumpStretchY, 1);
        targetScale = defaultScale;
        SpawnFootDust();
    }

    public void PlayLand()
    {
        if (Time.time < lastLandTime + 0.15f) return;
        lastLandTime = Time.time;

        visualsRoot.localScale = new Vector3(landSquashX, landSquashY, 1);
        targetScale = defaultScale;
        SpawnFootDust();
    }

    public void PlayDashStart(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            Vector3 stretch = new Vector3(dashStretchLimitY, dashStretchLimitX, 1);
            visualsRoot.localScale = stretch;
            targetScale = stretch;
        }
        else
        {
            Vector3 stretch = new Vector3(dashStretchLimitX, dashStretchLimitY, 1);
            visualsRoot.localScale = stretch;
            targetScale = stretch;
        }
    }

    public void PlayDashStop()
    {
        visualsRoot.localScale = new Vector3(dashStopSquashX, dashStopSquashY, 1);
        targetScale = defaultScale;
    }

    public void PlayWallJump()
    {
        visualsRoot.localScale = new Vector3(jumpStretchX, jumpStretchY, 1);
        targetScale = defaultScale;

        if (FXManager.Instance != null && FXManager.Instance.jumpDust != null)
        {
            FXManager.Instance.PlayVFX(FXManager.Instance.jumpDust, transform.position);
        }
    }

    public void PlayCrushDeath(Vector2 crushDir)
    {
        _lockDeformation = true;
        if (Mathf.Abs(crushDir.x) > Mathf.Abs(crushDir.y))
        {
            targetScale = new Vector3(crushThin, crushWide, 1f);
        }
        else
        {
            targetScale = new Vector3(crushWide, crushThin, 1f);
        }
    }

    public void ResetVisuals()
    {
        _lockDeformation = false;
        visualsRoot.localScale = Vector3.zero;
        targetScale = defaultScale;
    }

    private void SpawnFootDust()
    {
        if (FXManager.Instance != null && FXManager.Instance.jumpDust != null)
        {
            Vector3 spawnPos = player.transform.position;
            spawnPos.y -= 0.5f;
            FXManager.Instance.PlayVFX(FXManager.Instance.jumpDust, spawnPos);
        }
    }

    public void SetSquash(float x, float y)
    {
        if (visualsRoot == null) return;
        visualsRoot.localScale = new Vector3(x, y, 1f);
        targetScale = defaultScale;
    }
}