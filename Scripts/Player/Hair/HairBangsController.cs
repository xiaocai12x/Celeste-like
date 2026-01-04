using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HairBangsController : MonoBehaviour
{
    public Transform hairAnchorRight;
    public Transform hairAnchorLeft;

    public Vector2 baseOffset = Vector2.zero;

    public Sprite defaultBangs;
    public BangsMapping[] spriteMappings;

    [System.Serializable]
    public class BangsMapping
    {
        public string animationKeyword;
        public Sprite bangsSprite;
    }

    public HairController hairMaster;

    private PlayerController player;
    private SpriteRenderer playerSR;
    private SpriteRenderer bangsSR;
    private Material bangsMat;

    private string lastSpriteName;

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        playerSR = player.GetComponentInChildren<SpriteRenderer>();
        bangsSR = GetComponent<SpriteRenderer>();

        Shader hsvShader = Shader.Find("Custom/Bangs_Luminance_Sync");
        if (hsvShader != null)
        {
            bangsMat = new Material(hsvShader);
            bangsSR.material = bangsMat;
        }

        bangsSR.sortingLayerName = "Player";
        bangsSR.sortingOrder = 10;
    }

    private void LateUpdate()
    {
        if (player == null || !bangsSR.enabled) return;

        Transform activeAnchor = player.FacingDirection > 0 ? hairAnchorRight : hairAnchorLeft;
        if (activeAnchor != null)
        {
            transform.position = (Vector2)activeAnchor.position + baseOffset;
        }

        bangsSR.flipX = playerSR.flipX;

        UpdateBangsSpriteWithCache();

        if (hairMaster != null && bangsMat != null)
        {
            bangsMat.SetColor("_BaseColor", hairMaster.CurrentHairColor);
        }
    }

    private void UpdateBangsSpriteWithCache()
    {
        if (playerSR.sprite == null) return;

        string currentSpriteName = playerSR.sprite.name;

        if (currentSpriteName == lastSpriteName) return;

        lastSpriteName = currentSpriteName;
        string nameLower = currentSpriteName.ToLower();

        bool matched = false;
        foreach (var mapping in spriteMappings)
        {
            if (nameLower.Contains(mapping.animationKeyword.ToLower()))
            {
                ApplyNewSprite(mapping.bangsSprite);
                matched = true;
                break;
            }
        }

        if (!matched && defaultBangs != null)
        {
            ApplyNewSprite(defaultBangs);
        }
    }

    private void ApplyNewSprite(Sprite newSprite)
    {
        if (bangsSR.sprite == newSprite) return;

        bangsSR.sprite = newSprite;

        if (bangsMat != null && newSprite != null)
        {
            bangsMat.SetTexture("_MainTex", newSprite.texture);
        }
    }
}