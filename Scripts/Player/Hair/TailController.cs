using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TailController : MonoBehaviour
{
    public Transform tailAnchorRight;
    public Transform tailAnchorLeft;

    public Sprite circleSprite;
    public int segmentCount = 8;
    public float baseRadius = 0.25f;
    public float nodeDistance = 0.15f;
    [Range(0, 1)]
    public float tapering = 0.95f;

    public float gravity = 5f;
    public float drag = 4f;
    public float followSpeed = 15f;

    public HairController hairMaster;
    public Color fallbackColor = Color.white;

    public bool useDarkenEffect = true;
    [Range(0f, 1f)]
    public float darkenMultiplier = 0.85f;

    public float idleLift = 15f;
    public float idleSpread = 5f;
    public float stiffness = 0.5f;

    private PlayerController player;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh tailMesh;
    private List<Vector2> nodePositions = new List<Vector2>();
    private int lastFacingDirection;

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        tailMesh = new Mesh();
        tailMesh.name = "CatTailMesh";
        meshFilter.mesh = tailMesh;

        Shader urp2DShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material mat = new Material(urp2DShader);

        if (circleSprite != null)
        {
            mat.mainTexture = circleSprite.texture;
        }
        meshRenderer.material = mat;

        meshRenderer.sortingLayerName = "Player";
        meshRenderer.sortingOrder = -2;

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        nodePositions.Clear();
        for (int i = 0; i < segmentCount; i++) nodePositions.Add(transform.position);
    }

    private void Start()
    {
        if (player != null) lastFacingDirection = player.FacingDirection;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (meshRenderer == null || !meshRenderer.enabled)
        {
            if (tailMesh != null) tailMesh.Clear();
            return;
        }

        UpdatePositions();
        RenderTailMesh();
    }

    private void UpdatePositions()
    {
        Transform activeAnchor = player.FacingDirection > 0 ? tailAnchorRight : tailAnchorLeft;
        Vector2 currentAnchorPos = activeAnchor != null ? (Vector2)activeAnchor.position : (Vector2)transform.position;

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
            Vector2 force;
            bool isMoving = Mathf.Abs(player.RB.linearVelocity.x) > 0.1f || Mathf.Abs(player.RB.linearVelocity.y) > 0.1f;

            if (isMoving)
            {
                force = new Vector2(-player.FacingDirection * drag, -gravity);
            }
            else
            {
                force = new Vector2(-player.FacingDirection * idleSpread, idleLift * (1.2f - (i * 0.1f)));
                force.y += Mathf.Sin(Time.time * 2f + i) * 1.5f;
                force.x += Mathf.Cos(Time.time * 1.5f + i) * 1f;
            }

            Vector2 target = nodePositions[i - 1] + force * Time.deltaTime;
            nodePositions[i] = Vector2.Lerp(nodePositions[i], target, Time.deltaTime * followSpeed);

            float d = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
            if (d > nodeDistance)
                nodePositions[i] = nodePositions[i - 1] + (nodePositions[i] - nodePositions[i - 1]).normalized * nodeDistance;
        }
    }

    private void RenderTailMesh()
    {
        vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear();

        Color rawSyncColor = (hairMaster != null) ? hairMaster.CurrentHairColor : fallbackColor;

        Color processedColor = rawSyncColor;
        if (useDarkenEffect)
        {
            processedColor.r *= darkenMultiplier;
            processedColor.g *= darkenMultiplier;
            processedColor.b *= darkenMultiplier;
        }

        Color finalColorForMesh = processedColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            finalColorForMesh = processedColor.linear;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(tapering, i);
            AddQuad(nodePositions[i], r + 0.125f, Color.black);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(tapering, i);
            AddQuad(nodePositions[i], r, finalColorForMesh);
        }

        tailMesh.Clear();
        tailMesh.vertices = vertices.ToArray();
        tailMesh.uv = uvs.ToArray();
        tailMesh.triangles = triangles.ToArray();
        tailMesh.colors = colors.ToArray();
    }

    private void AddQuad(Vector2 worldPos, float radius, Color color)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int vIndex = vertices.Count;

        vertices.Add(localPos + new Vector3(-radius, -radius, 0));
        vertices.Add(localPos + new Vector3(radius, -radius, 0));
        vertices.Add(localPos + new Vector3(-radius, radius, 0));
        vertices.Add(localPos + new Vector3(radius, radius, 0));

        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 1); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);

        for (int j = 0; j < 4; j++) colors.Add(color);
    }
}