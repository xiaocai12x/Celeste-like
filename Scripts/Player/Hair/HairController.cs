using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HairController : MonoBehaviour
{
    [Header("Anchor Settings")]
    [Tooltip("角色向右时，发根的位置")]
    public Transform hairAnchorRight;
    [Tooltip("角色向左时，发根的位置")]
    public Transform hairAnchorLeft;

    [Header("8x8 Pixel Sprite")]
    public Sprite hairCircleSprite;

    [Header("Nodes Settings")]
    public int segmentCount = 6;
    public float baseRadius = 0.45f;
    public float nodeDistance = 0.16f;

    [Header("Physics Settings")]
    public float followSpeed = 22f;
    public float gravity = 12f;
    public float drag = 4f;

    [Header("Juice (Pro 特性)")]
    [Tooltip("运动时的拉伸强度")]
    public float stretchStrength = 0.35f;
    [Tooltip("呼吸/摆动频率")]
    public float waveFrequency = 6f;
    [Tooltip("呼吸/摆动幅度")]
    public float waveAmplitude = 0.06f;

    [Header("Visual Sync")]
    [Tooltip("拖入 Visuals 子物体，用于同步挤压拉伸")]
    public Transform visualsRoot;

    [Header("Visual Settings (v2.3 Matrix)")]
    public Color colorNormal = new Color32(158, 132, 133, 255);
    public Color colorUsed = new Color32(158, 158, 158, 255);
    public Color colorDouble = new Color32(158, 90, 126, 255);
    public Color colorDash = new Color32(158, 41, 41, 255);
    public Color colorGolden = new Color32(168, 144, 81, 255);
    public Color colorDead = new Color32(133, 133, 133, 255);
    public Color colorRefill = new Color32(126, 173, 189, 255);

    public float colorLerpSpeed = 15f;

    // 内部状态
    private Color currentHairColor;
    private float pulseScale = 1f;
    private float refillFlashWeight = 0f;

    private PlayerController player;
    private SpriteRenderer playerSR;
    private MeshFilter meshFilter;
    private Mesh hairMesh;
    private List<Vector2> nodePositions = new List<Vector2>();
    private int lastFacingDirection;

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        playerSR = player.GetComponentInChildren<SpriteRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        hairMesh = new Mesh();
        hairMesh.name = "CelesteHairMesh_Sync";
        meshFilter.mesh = hairMesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();

        Shader urp2DShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material hairMat = new Material(urp2DShader);
        if (hairCircleSprite != null)
        {
            hairMat.mainTexture = hairCircleSprite.texture;
        }
        mr.material = hairMat;

        mr.sortingLayerName = "Player";
        mr.sortingOrder = -1;

        nodePositions.Clear();
        for (int i = 0; i < segmentCount; i++) nodePositions.Add(transform.position);
        lastFacingDirection = player.FacingDirection;
        currentHairColor = colorNormal;
    }

    public void WarpNodes(Vector3 position)
    {
        for (int i = 0; i < nodePositions.Count; i++)
        {
            nodePositions[i] = position;
        }
        GenerateCelesteStyleMesh();
    }

    private void LateUpdate()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null || !mr.enabled) return;

        UpdatePositions();
        UpdateHairColor();
        GenerateCelesteStyleMesh();
    }

    private void UpdatePositions()
    {
        Transform activeAnchor = player.FacingDirection > 0 ? hairAnchorRight : hairAnchorLeft;
        Vector2 currentAnchorPos = activeAnchor != null ? (Vector2)activeAnchor.position : (Vector2)transform.position;

        float currentFollowSpeed = followSpeed;
        float currentDrag = drag;
        float currentGravity = gravity;

        if (player.IsDead)
        {
            currentFollowSpeed = 5f;
            currentDrag = 1f;
            currentGravity = 25f;
        }

        if (player.FacingDirection != lastFacingDirection)
        {
            for (int i = 1; i < nodePositions.Count; i++)
            {
                float relativeX = nodePositions[i].x - nodePositions[0].x;
                nodePositions[i] = new Vector2(currentAnchorPos.x - relativeX, nodePositions[i].y);
            }
        }
        lastFacingDirection = player.FacingDirection;

        nodePositions[0] = currentAnchorPos;

        for (int i = 1; i < segmentCount; i++)
        {
            Vector2 force = new Vector2(-player.FacingDirection * currentDrag, -currentGravity);
            float wave = Mathf.Sin(Time.time * waveFrequency + i * 0.8f) * waveAmplitude * (i / (float)segmentCount);
            force.y += wave * 50f;

            Vector2 target = nodePositions[i - 1] + force * Time.deltaTime;
            nodePositions[i] = Vector2.Lerp(nodePositions[i], target, Time.deltaTime * currentFollowSpeed);

            float d = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
            if (d > nodeDistance)
                nodePositions[i] = nodePositions[i - 1] + (nodePositions[i] - nodePositions[i - 1]).normalized * nodeDistance;
        }
    }

    private void GenerateCelesteStyleMesh()
    {
        vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear();

        DrawHairPass(0.125f, Color.black);
        DrawHairPass(0f, currentHairColor);

        hairMesh.Clear();
        hairMesh.vertices = vertices.ToArray();
        hairMesh.uv = uvs.ToArray();
        hairMesh.triangles = triangles.ToArray();
        hairMesh.colors = colors.ToArray();
    }

    private void DrawHairPass(float outlineSize, Color passColor)
    {
        float characterScaleX = visualsRoot != null ? visualsRoot.localScale.x : 1f;
        float characterScaleY = visualsRoot != null ? visualsRoot.localScale.y : 1f;

        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(0.65f, i);
            r = Mathf.Max(0.05f, r);

            float squashFactor = (characterScaleX + characterScaleY) * 0.5f;
            float finalRadius = r * squashFactor * pulseScale;

            float angle = 0;
            if (i < segmentCount - 1)
            {
                Vector2 dir = nodePositions[i + 1] - nodePositions[i];
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
            else if (i > 0)
            {
                Vector2 dir = nodePositions[i] - nodePositions[i - 1];
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }

            float velocityMult = 1f;
            if (i > 0)
            {
                float dist = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
                float speed = dist / nodeDistance;
                velocityMult = 1f + (speed - 1f) * stretchStrength;
            }

            AddAdvancedQuad(nodePositions[i], finalRadius + outlineSize, passColor, angle, velocityMult);
        }
    }

    private void AddAdvancedQuad(Vector2 worldPos, float radius, Color color, float angle, float stretch)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int vIndex = vertices.Count;

        float halfSizeX = radius * stretch;
        float halfSizeY = radius / (stretch * 0.7f + 0.3f);

        Vector3 v0 = new Vector3(-halfSizeX, -halfSizeY, 0);
        Vector3 v1 = new Vector3(halfSizeX, -halfSizeY, 0);
        Vector3 v2 = new Vector3(-halfSizeX, halfSizeY, 0);
        Vector3 v3 = new Vector3(halfSizeX, halfSizeY, 0);

        Quaternion rot = Quaternion.Euler(0, 0, angle);
        vertices.Add(localPos + rot * v0);
        vertices.Add(localPos + rot * v1);
        vertices.Add(localPos + rot * v2);
        vertices.Add(localPos + rot * v3);

        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 1); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);

        Color finalColor = color;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            finalColor = color.linear;
        }

        for (int j = 0; j < 4; j++) colors.Add(finalColor);
    }

    private void UpdateHairColor()
    {
        Color targetColor = GetPriorityColor();
        currentHairColor = Color.Lerp(currentHairColor, targetColor, Time.deltaTime * colorLerpSpeed);

        if (refillFlashWeight > 0.01f)
        {
            currentHairColor = Color.Lerp(currentHairColor, colorRefill, refillFlashWeight);
            refillFlashWeight = Mathf.MoveTowards(refillFlashWeight, 0f, Time.deltaTime * 5f);
        }

        if (pulseScale > 1f)
        {
            pulseScale = Mathf.MoveTowards(pulseScale, 1f, Time.deltaTime * 4f);
        }
    }

    private Color GetPriorityColor()
    {
        if (player.IsDead) return colorDead;
        if (player.StateMachine.CurrentState == player.DashState) return colorDash;

        if (player.CurrentStamina < player.playerData.tiredThreshold)
        {
            return (Time.time % 0.2f < 0.1f) ? colorDash : colorUsed;
        }

        return player.DashState.CheckIfCanDash() ? colorNormal : colorUsed;
    }

    public void OnRefill()
    {
        pulseScale = 1.35f;
        refillFlashWeight = 1.0f;
    }

    public float GetPulseScale() => pulseScale;
    public Color CurrentHairColor => currentHairColor;
}