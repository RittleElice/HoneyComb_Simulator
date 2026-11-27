using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PollenTextureBlend
{
    [InspectorName("일반 (Normal)")] Normal = 0,
    [InspectorName("곱하기 (Multiply)")] Multiply = 1,
    [InspectorName("스크린 (Screen)")] Screen = 2,
    [InspectorName("오버레이 (Overlay)")] Overlay = 3
}

[System.Serializable]
public sealed class FrameCellArtwork
{
    public Sprite wallTwoArm;
    public Sprite wallThreeArm;
    public Sprite completeCell;
    public Sprite rapeSeedNectar;
    public Sprite cherryBlossomNectar;
    public Sprite blackLocustNectar;
    public Sprite chestnutNectar;
    public Sprite wildflowerNectar;
    public Sprite cappedPupa;
    public Sprite pollen;
    public Texture2D pollenTexture;
    [Min(.01f), Tooltip("화분 내부 질감의 크기 배율. 1은 기존 크기이며, 2는 무늬가 두 배 커집니다. 화분 이미지 크기는 바뀌지 않습니다.")]
    public float pollenTextureScale = 1f;
    [Range(0f, 1f), Tooltip("질감의 불투명도. 0은 질감 없음, 1은 완전히 적용. 화분 이미지 자체의 불투명도는 유지됩니다.")]
    public float pollenTextureOpacity = 1f;
    [Tooltip("화분 이미지와 질감을 합성하는 방식입니다.")]
    public PollenTextureBlend pollenTextureBlend = PollenTextureBlend.Multiply;
    public Color pollenColorA = Color.white;
    public Color pollenColorB = new Color(1f, .7f, .3f, 1f);
    [HideInInspector] public Shader pollenShader;
    public Sprite egg;
    public Sprite larva;

    public Sprite Nectar(int kind)
    {
        switch (kind)
        {
            case 0: return rapeSeedNectar;
            case 1: return cherryBlossomNectar;
            case 2: return blackLocustNectar;
            case 3: return chestnutNectar;
            case 4: return wildflowerNectar;
            default: return null;
        }
    }
}

// One child per texture/range, never one object per cell.
public sealed class FrameCellSpriteLayer : MaskableGraphic
{
    internal FrameCellMesh Owner;
    internal Texture Texture;
    public override Texture mainTexture => Texture != null ? Texture : Texture2D.whiteTexture;
    protected override void OnPopulateMesh(VertexHelper vh) => Owner.PopulateTexture(vh, Texture);
}

// One batched graphic per range. Shared junctions have exactly one owner, including
// junctions crossing a range boundary. The source art uses a hex radius of 40 px.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class FrameCellMesh : MaskableGraphic
{
    public sealed class Junction
    {
        public Vector2 Position;
        public readonly List<int> Cells = new List<int>(3);
        public readonly List<int> Corners = new List<int>(3);
        public int Owner => Cells[0];
    }

    public static List<Junction> BuildJunctions(int columns, int rows, float radius, Vector2 origin)
    {
        var result = new List<Junction>();
        var lookup = new Dictionary<Vector2Int, Junction>();
        int[] dx = { 1, 0, -1, -1, 0, 1 };
        int[] dy = { 1, 2, 1, -1, -2, -1 };
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        for (int corner = 0; corner < 6; corner++)
        {
            var key = new Vector2Int(2 * col + (row & 1) + 1 + dx[corner], 3 * row + 2 + dy[corner]);
            if (!lookup.TryGetValue(key, out var node))
            {
                node = new Junction { Position = origin + new Vector2(key.x * Mathf.Sqrt(3f) * radius / 2, key.y * radius / 2) };
                lookup.Add(key, node); result.Add(node);
            }
            node.Cells.Add(row * columns + col); node.Corners.Add(corner);
        }
        return result;
    }

    private struct State
    {
        public byte Construction;
        public bool Complete;
        public float Nectar;
        public int NectarKind;
        public float NectarAlpha;
        public int Occupant;
        public float PollenScale;
        public uint PollenSeed;
        public float BroodProgress;
        public uint BroodSeed;
        public bool Same(State other) => BroodProgress == other.BroodProgress && BroodSeed == other.BroodSeed && PollenSeed == other.PollenSeed && Construction == other.Construction && Complete == other.Complete && Nectar == other.Nectar && NectarKind == other.NectarKind && NectarAlpha == other.NectarAlpha && Occupant == other.Occupant && PollenScale == other.PollenScale;
    }
    private FrameSurface surface;
    private BeeCycleRuntime broodRuntime;
    private int columns, firstIndex, lastIndex;
    private float radius, alphaMin, alphaMax = 1f, nectarMinimum = .3f;
    private float representativePercent = 50f;
    private float dryAlpha = 1f, wetAlpha = .3f;
    private float pollenMinimum = .3f;
    private float nectarMaximum = 1f, pollenMaximum = 1f;
    private float larvaMinimum=.3f, larvaMaximum=1f;
    private Color pupaStart=Color.white,pupaEnd=Color.white;
    private float completeOpacity=1f,eggAngleMin=0f,eggAngleMax=360f;
    private Color completeTint=Color.white;
#if UNITY_EDITOR
    private IReadOnlyDictionary<long,(CycleStage stage,float progress)> editorBrood;
    private ulong editorAppearanceSalt;
    public void SetEditorPreview(FrameSurface value,IReadOnlyDictionary<long,(CycleStage stage,float progress)> brood,ulong appearanceSalt=0)
    { editorBrood=brood;editorAppearanceSalt=appearanceSalt;SetSurface(value); }
#endif
    private bool ReadBrood(long id,out CycleStage stage,out float progress)
    {
#if UNITY_EDITOR
        if(editorBrood!=null&&editorBrood.TryGetValue(id,out var sample)){stage=sample.stage;progress=sample.progress;return true;}
#endif
        stage=default;progress=0;
        if(broodRuntime==null||!broodRuntime.TryGetObservationDevelopment(id,out stage,out double value))return false;
        progress=(float)value;return true;
    }
    private Vector2 origin;
    private FrameCellArtwork artwork;
    private Texture primaryTexture;
    private Material pollenStyleMaterial;
    private readonly List<FrameCellSpriteLayer> layers = new List<FrameCellSpriteLayer>();
    private sealed class SpriteGeometry
    {
        public Vector2[] Vertices, UV;
        public ushort[] Triangles;
    }
    private readonly Dictionary<Sprite, SpriteGeometry> geometry = new Dictionary<Sprite, SpriteGeometry>();
    private readonly List<Junction> nodes = new List<Junction>();
    private readonly List<int> dependencies = new List<int>();
    private State[] states;
    public int RefreshCount { get; private set; }
    public override Texture mainTexture => primaryTexture != null ? primaryTexture : Texture2D.whiteTexture;

    // Retains the existing entry point for callers and editor geometry tests.
    public void Configure(int columns, int rows, int firstIndex, int lastIndex,
        float radius, float lineWidth, Vector2 origin, Color outlineColor)
    {
        ConfigureArt(columns, firstIndex, lastIndex, radius, origin,
            BuildJunctions(columns, rows, radius, origin), 0, 1, .3f);
    }

    public void ConfigureArt(int columns, int firstIndex, int lastIndex, float radius,
        Vector2 origin, List<Junction> topology, float minimumAlpha, float maximumAlpha, float minimumNectar,
        FrameCellArtwork images = null, float representativeThresholdPercent = 50f,
        float dryNectarAlpha = 1f, float wetNectarAlpha = .3f, float minimumPollenScale = .3f, float maximumNectarScale = 1f, float maximumPollenScale = 1f, float minimumLarvaScale = .3f, float maximumLarvaScale = 1f, Color? pupaStartColor = null, Color? pupaEndColor = null, float completeCellOpacity = 1f, float minimumEggAngle = 0f, float maximumEggAngle = 360f, Color? completeCellColor = null)
    {
        this.columns = columns; this.firstIndex = firstIndex; this.lastIndex = lastIndex;
        this.radius = radius; this.origin = origin;
        alphaMin = Mathf.Clamp01(minimumAlpha); alphaMax = Mathf.Clamp(maximumAlpha, alphaMin, 1);
        nectarMinimum = Mathf.Clamp01(minimumNectar);
        representativePercent = Mathf.Clamp(representativeThresholdPercent, 50, 100);
        dryAlpha = Mathf.Clamp01(dryNectarAlpha); wetAlpha = Mathf.Clamp01(wetNectarAlpha);
        pollenMinimum = Mathf.Clamp01(minimumPollenScale);
        nectarMaximum = Mathf.Max(.01f,maximumNectarScale);
        pollenMaximum = Mathf.Max(.01f,maximumPollenScale);
        larvaMinimum=Mathf.Clamp01(minimumLarvaScale);larvaMaximum=Mathf.Max(.01f,maximumLarvaScale);
        pupaStart=pupaStartColor??Color.white;pupaEnd=pupaEndColor??Color.white;
        completeOpacity=Mathf.Clamp01(completeCellOpacity);completeTint=completeCellColor??Color.white;
        eggAngleMin=Mathf.Min(minimumEggAngle,maximumEggAngle);eggAngleMax=Mathf.Max(minimumEggAngle,maximumEggAngle);
        artwork = images ?? new FrameCellArtwork();
        BuildLayers();
        raycastTarget = false; color = Color.white;
        nodes.Clear(); dependencies.Clear();
        var needed = new HashSet<int>();
        for (int i = firstIndex; i < lastIndex; i++) needed.Add(i);
        foreach (var node in topology)
        {
            if (node.Owner < firstIndex || node.Owner >= lastIndex) continue;
            nodes.Add(node);
            foreach (int index in node.Cells) needed.Add(index);
        }
        dependencies.AddRange(needed); dependencies.Sort();
        states = new State[dependencies.Count];
        surface = null;
        SetMaterialDirty(); SetVerticesDirty();
    }

    private State Read(int index)
    {
        var cell = surface.GetCell(index % columns, index / columns);
        bool nectar = cell.Content == CellContentKind.Nectar || cell.Content == CellContentKind.Honey;
        float fill = nectar ? (float)surface.GetCapacityFillRatio(index % columns, index / columns) : 0;
        // A nonempty quantitative nectar cell may consist of sugar alone or water alone.
        bool present = nectar && (surface.HasQuantifiedContent(index % columns, index / columns)
            ? surface.GetTotalSugarMassMg(index % columns, index / columns) > 0 || surface.GetWaterMassMg(index % columns, index / columns) > 0
            : cell.FillRatio > 0);
        return new State { Construction = cell.Construction, Complete = cell.IsComplete,
            Occupant = OccupantAt(index, cell),
            BroodProgress = BroodProgressAt(index,cell),
            BroodSeed = cell.Content == CellContentKind.Brood ? ObservationBroodSeed(surface.GetBeeId(index%columns,index/columns)) : 0,
            PollenSeed = surface.GetPollenAppearanceSeed(index % columns,index / columns),
            PollenScale = cell.Content == CellContentKind.Pollen ? pollenMaximum * Mathf.Lerp(pollenMinimum, 1,
                Mathf.Clamp01((float)surface.GetCapacityFillRatio(index % columns, index / columns))) : 0,
            NectarKind = present ? RepresentativeNectar(surface, index % columns, index / columns, representativePercent) : 4,
            NectarAlpha = NectarOpacity(surface.GetTotalSugarMassMg(index % columns, index / columns),
                surface.GetWaterMassMg(index % columns, index / columns), dryAlpha, wetAlpha),
            Nectar = present ? nectarMaximum * Mathf.Lerp(nectarMinimum, 1, Mathf.Clamp01(fill)) : 0 };
    }

    private float BroodProgressAt(int index,CellData cell)
    {
        if(cell.Content!=CellContentKind.Brood)return 0;
        return ReadBrood(surface.GetBeeId(index%columns,index/columns),out _,out float progress)?progress:0;
    }
    private uint ObservationBroodSeed(long beeId)
    {
#if UNITY_EDITOR
        if(editorBrood!=null)beeId=unchecked((long)((ulong)beeId^editorAppearanceSalt));
#endif
        return BroodAppearanceSeed(beeId);
    }
    private static uint BroodAppearanceSeed(long beeId)
    {
        // Stable hash of the full saved identity; no simulation RNG or extra save state.
        unchecked { ulong x=(ulong)beeId+0x9e3779b97f4a7c15UL;x=(x^(x>>30))*0xbf58476d1ce4e5b9UL;x=(x^(x>>27))*0x94d049bb133111ebUL;x^=x>>31;uint seed=(uint)(x^(x>>32));return seed==0?1:seed; }
    }

    private int OccupantAt(int index, CellData cell)
    {
        int x = index % columns, y = index / columns;
        if (cell.Content == CellContentKind.Pollen)
            return surface.GetPollenMassMg(x, y) > 0 || cell.FillRatio > 0 ? 1 : 0;
        if (cell.Content != CellContentKind.Brood ||
            !ReadBrood(surface.GetBeeId(x, y), out var stage,out _)) return 0;
        switch (stage)
        {
            case CycleStage.Egg: return 2;
            case CycleStage.Larva: return 3;
            case CycleStage.Pupa: return 4;
            default: return 0;
        }
    }

    public static float NectarOpacity(double sugar, double water, float dry, float wet)
        => Mathf.Lerp(dry, wet, sugar + water > 0 ? Mathf.Clamp01((float)(water / (sugar + water))) : 0);

    public static int RepresentativeNectar(FrameSurface surface, int x, int y, float thresholdPercent)
    {
        double total = surface.GetTotalSugarMassMg(x, y);
        if (total <= 0) return 4;
        double threshold = total * (Mathf.Clamp(thresholdPercent, 50, 100) / 100.0);
        // Stable forage IDs: 0 rape, 1 cherry, 2 black locust, 3 chestnut,
        // 4..7 wildflowers. Water is deliberately absent from this comparison.
        for (int kind = 0; kind < 4; kind++)
            if (surface.GetSugarMassMg(x, y, kind) > threshold) return kind;
        double wildflower = surface.GetSugarMassMg(x, y, NectarSourceIdentity.Wildflower);
        if (wildflower > threshold) return 4;
        return 4; // No strict majority, unknown source, or mixed nectar.
    }

    public void SetSurface(FrameSurface value, BeeCycleRuntime runtime = null)
    {
        bool changed = surface != value;
        surface = value;
        broodRuntime = runtime;
        if (states == null) return;
        for (int i = 0; i < dependencies.Count; i++)
        {
            var next = surface == null ? default : Read(dependencies[i]);
            if (!states[i].Same(next)) changed = true;
            states[i] = next;
        }
        if (changed)
        {
            RefreshCount++; SetVerticesDirty();
            foreach (var layer in layers) layer.SetVerticesDirty();
        }
    }

    private State Cached(int index) => states[dependencies.BinarySearch(index)];
    public static float WallAlpha(byte maximumConstruction, float minimum, float maximum)
        => Mathf.Lerp(minimum, maximum, maximumConstruction / 100f);

    protected override void OnPopulateMesh(VertexHelper vh)
        => PopulateTexture(vh, primaryTexture);

    internal void PopulateTexture(VertexHelper vh, Texture texture)
    {
        vh.Clear();
        if (surface == null || texture == null || radius <= 0) return;
        float scale = radius / 40f;
        for (int index = firstIndex; index < lastIndex; index++)
        {
            int x = index % columns, y = index / columns;
            var center = origin + new Vector2(Mathf.Sqrt(3f) * radius * (x + .5f + (y & 1) * .5f), radius + y * 1.5f * radius);
            var cell = Cached(index);
            if (cell.Complete) DrawSprite(vh, texture, artwork.completeCell, center, new Vector2(60, 69), new Vector2(.5f, .5f), scale, 0, completeOpacity, true, 0, completeTint);
            if (cell.Nectar > 0) DrawSprite(vh, texture, artwork.Nectar(cell.NectarKind), center, new Vector2(57, 63), new Vector2(.5f, .5f), scale * cell.Nectar, 0, cell.NectarAlpha, true);
            Sprite occupant = cell.Occupant == 1 ? artwork.pollen : cell.Occupant == 2 ? artwork.egg : cell.Occupant == 3 ? artwork.larva : cell.Occupant == 4 ? artwork.cappedPupa : null;
            // Supplied square artwork shares a canvas: preserve its aspect and
            // internal whitespace (the egg is intentionally much smaller than the cap).
            if (occupant != null) DrawSprite(vh, texture, occupant, center, cell.Occupant == 1 ? new Vector2(57, 63) : new Vector2(96, 96), new Vector2(.5f, .5f),
                scale * (cell.Occupant == 1 ? cell.PollenScale : cell.Occupant == 3 ? larvaMaximum*Mathf.Lerp(larvaMinimum,1,cell.BroodProgress) : 1),
                cell.Occupant == 2 ? Mathf.Lerp(eggAngleMin,eggAngleMax,PollenRandom(cell.BroodSeed,6)) : cell.Occupant == 3 ? PollenRandom(cell.BroodSeed,5)*360f : 0, 1, true, cell.Occupant == 1 ? cell.PollenSeed : 0,
                cell.Occupant == 4 ? Color.Lerp(pupaStart,pupaEnd,cell.BroodProgress) : (Color?)null);
        }
        foreach (var node in nodes)
        {
            int active = 0, corner = 0; byte maximum = 0;
            for (int i = 0; i < node.Cells.Count; i++)
            {
                byte construction = Cached(node.Cells[i]).Construction;
                if (construction > 0) { active++; corner = node.Corners[i]; }
                if (construction > maximum) maximum = construction;
            }
            if (active == 0) continue;
            float angle = 60 * corner - 60;
            if (active == 1)
                DrawSprite(vh, texture, artwork.wallTwoArm, node.Position, new Vector2(40, 18), new Vector2(.5f, 4f / 18), scale, angle, WallAlpha(maximum, alphaMin, alphaMax));
            else
                DrawSprite(vh, texture, artwork.wallThreeArm, node.Position, new Vector2(40, 34), new Vector2(.5f, 20f / 34), scale, angle, WallAlpha(maximum, alphaMin, alphaMax));
        }
    }

    private void BuildLayers()
    {
        foreach (var layer in layers)
        {
            layer.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(layer.gameObject); else DestroyImmediate(layer.gameObject);
        }
        layers.Clear(); geometry.Clear(); primaryTexture = null;
        ReleasePollenMaterial();
        if (artwork.pollenShader != null && artwork.pollen != null)
        {
            pollenStyleMaterial = new Material(artwork.pollenShader) { hideFlags = HideFlags.HideAndDontSave };
            pollenStyleMaterial.SetTexture("_DetailTex", artwork.pollenTexture != null ? artwork.pollenTexture : Texture2D.whiteTexture);
            pollenStyleMaterial.SetFloat("_DetailOpacity", artwork.pollenTexture != null ? Mathf.Clamp01(artwork.pollenTextureOpacity) : 0f);
            pollenStyleMaterial.SetFloat("_DetailBlend", (float)artwork.pollenTextureBlend);
            if(canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
        material = null;
        var textures = new HashSet<Texture>();
        var sprites = new[] { artwork.completeCell, artwork.rapeSeedNectar, artwork.cherryBlossomNectar,
            artwork.blackLocustNectar, artwork.chestnutNectar, artwork.wildflowerNectar,
            artwork.pollen, artwork.egg, artwork.larva, artwork.cappedPupa, artwork.wallTwoArm, artwork.wallThreeArm };
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue;
            if (!geometry.ContainsKey(sprite)) geometry.Add(sprite, new SpriteGeometry
                { Vertices = sprite.vertices, UV = sprite.uv, Triangles = sprite.triangles });
            if (!textures.Add(sprite.texture)) continue;
            if (primaryTexture == null) { primaryTexture = sprite.texture; if(artwork.pollen!=null && sprite.texture==artwork.pollen.texture)material=pollenStyleMaterial; continue; }
            var child = new GameObject("CellSprite_" + sprite.name, typeof(RectTransform), typeof(CanvasRenderer));
            child.transform.SetParent(transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layer = child.AddComponent<FrameCellSpriteLayer>();
            layer.Owner = this; layer.Texture = sprite.texture; layer.raycastTarget = false;
            if(artwork.pollen!=null && sprite.texture==artwork.pollen.texture)layer.material=pollenStyleMaterial;
            layers.Add(layer);
        }
    }

    // Use actual Sprite geometry/UVs, including sliced and tightly packed sprites.
    // Reference dimensions preserve the SVG layout regardless of image resolution.
    private void DrawSprite(VertexHelper vh, Texture texture, Sprite sprite, Vector2 anchor,
        Vector2 size, Vector2 topLeftPivot, float scale, float angle, float alpha, bool preserveOrientation = false, uint pollenSeed = 0, Color? tintOverride = null)
    {
        if (sprite == null || sprite.texture != texture) return;
        var source = geometry[sprite];
        int start = vh.currentVertCount;
        float radians = angle * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        var matrix = transform.localToWorldMatrix;
        bool unmirror = preserveOrientation && matrix.m00 * matrix.m11 - matrix.m01 * matrix.m10 < 0;
        var tint = tintOverride ?? (pollenSeed == 0 ? Color.white : Color.Lerp(artwork.pollenColorA, artwork.pollenColorB, PollenRandom(pollenSeed,1)));
        float rotation=PollenRandom(pollenSeed,2)*Mathf.PI*2;
        var offset=new Vector2(PollenRandom(pollenSeed,3),PollenRandom(pollenSeed,4));
        for (int i = 0; i < source.Vertices.Length; i++)
        {
            Vector2 pixel = source.Vertices[i] * sprite.pixelsPerUnit + sprite.pivot;
            Vector2 p = new Vector2((pixel.x / sprite.rect.width - topLeftPivot.x) * size.x,
                (pixel.y / sprite.rect.height - (1 - topLeftPivot.y)) * size.y) * scale;
            // Keep source-art handedness on both faces and with the cell-grid mirror.
            // Only reflect about the cell center; cell positions and wall topology stay intact.
            p = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            if (unmirror) p.x = -p.x;
            var vertex = UIVertex.simpleVert;
            vertex.position = anchor + p;
            vertex.color = new Color(tint.r, tint.g, tint.b, alpha);
            var uv=new Vector2(pixel.x/sprite.rect.width-.5f,pixel.y/sprite.rect.height-.5f) / Mathf.Max(.01f,artwork.pollenTextureScale);
            vertex.uv1 = new Vector4(uv.x*Mathf.Cos(rotation)-uv.y*Mathf.Sin(rotation)+offset.x,uv.x*Mathf.Sin(rotation)+uv.y*Mathf.Cos(rotation)+offset.y,pollenSeed==0?0:1,0);
            vertex.uv0 = source.UV[i];
            vh.AddVert(vertex);
        }
        for (int i = 0; i < source.Triangles.Length; i += 3)
            vh.AddTriangle(start + source.Triangles[i], start + source.Triangles[i + 1], start + source.Triangles[i + 2]);
    }
    private static float PollenRandom(uint seed,uint stream)
    {
        unchecked { uint x=seed+stream*0x9e3779b9u; x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return (x>>8)*(1f/16777216f); }
    }
    private void ReleasePollenMaterial()
    {
        if(pollenStyleMaterial==null)return;
        if(Application.isPlaying)Destroy(pollenStyleMaterial);else DestroyImmediate(pollenStyleMaterial);
        pollenStyleMaterial=null;
    }
    protected override void OnDestroy(){ReleasePollenMaterial();base.OnDestroy();}

}
