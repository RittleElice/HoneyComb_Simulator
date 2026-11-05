using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EquipmentTabs : MonoBehaviour
{
    enum Page { Apiary, Shop, Warehouse }
    static readonly Color Panel = new Color(.075f, .12f, .17f);
    static readonly Color Normal = new Color(.18f, .27f, .33f);
    static readonly Color Selected = new Color(.16f, .45f, .46f);
    RectTransform safe, pageRoot, bar, shelfContent, shelfViewport, inventoryRegion;
    EquipmentSlotGrid inventoryGrid, warehouseGrid, trashGrid;
    RectTransform warehouseTitle, trashTitle, emptyButton;
    TMP_Text status, held;
    InventoryItemPresenter original;
    GameWorld world;
    Page page;
    Vector2 lastSize;

    readonly Button[] tabs = new Button[3];

    void Start()
    {
        world = GameBootstrap.Instance.GameWorld;
        foreach (var view in FindObjectsByType<InventoryItemPresenter>())
            if (view.gameObject.scene.name == "SharedUI") { original = view; break; }
        var canvasObject = new GameObject("Equipment navigation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 720);
        scaler.matchWidthOrHeight = .5f;
        safe = Box(canvas.transform, "SafeArea", 0, 0, 1, 1);
        bar = Box(safe, "Tabs", .02f, .01f, .63f, .095f);
        string[] titles = { "양봉장", "상점", "창고" };
        for (int i = 0; i < tabs.Length; i++)
        {
            Page target = (Page)i;
            tabs[i] = MakeButton(Box(bar,titles[i],i/3f,0,(i+1)/3f,1), titles[i], () => Open(target));
        }
        held = Label(Box(safe,"Held equipment",.02f,.01f,.63f,.095f),"");
        world.Hand.Slot.OnChanged += HandChanged;
        tabs[0].image.color = Selected;
        tabs[0].GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;
        Open(Page.Apiary);
        HandChanged(world.Hand.Slot);
    }

    void OnDestroy()
    {
        if (world != null) world.Hand.Slot.OnChanged -= HandChanged;
        if (original != null && page != Page.Apiary) original.gameObject.SetActive(true);
    }
    void HandChanged(ItemSlot slot)
    {
        var lastHeld = slot.Value;
        bar.gameObject.SetActive(lastHeld == null);
        held.gameObject.SetActive(lastHeld != null && page != Page.Apiary);
        held.text = lastHeld == null ? "" : "손 · " + lastHeld.Name;
    }

    void Open(Page next)
    {
        if (!world.Hand.IsEmpty || GameTimeRuntime.InputBlocked) return;
        if (pageRoot != null) { pageRoot.gameObject.SetActive(false); Destroy(pageRoot.gameObject); }
        page = next;
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].image.color = (int)page == i ? Selected : Normal;
            tabs[i].GetComponentInChildren<TMP_Text>().fontStyle = (int)page == i ? FontStyles.Bold : FontStyles.Normal;
        }
        if (original != null) original.gameObject.SetActive(page == Page.Apiary);
        if (page == Page.Apiary) return;
        pageRoot = Box(safe,"Equipment page",0,0,1,1);
        pageRoot.SetAsFirstSibling();
        pageRoot.gameObject.AddComponent<Image>().color = Panel;
        inventoryRegion = Box(pageRoot,"Inventory region",0,0,1,1);
        Label(Box(inventoryRegion,"Title",0,.83f,1,1),"소지품");
        inventoryGrid = Grid(inventoryRegion,world.Inventory,6,page == Page.Shop,"Inventory");
        shelfViewport = Box(pageRoot,"Shelf viewport",0,0,1,1);
        if (page == Page.Shop)
        {
            int n = 0;
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                string name = GameWorld.ShopName(kind);
                if (name == null) continue;
                float x = n % 2 * .49f, top = 1 - n / 2 * .24f;
                MakeButton(Box(shelfViewport,name,x,top-.22f,x+.48f,top),name,() => {
                    if (world.TryAcquire(kind,out string message)) Save();
                    status.text = message;
                });
                n++;
            }
        }
        else
        {
            shelfContent = Box(shelfViewport,"Shelves",0,0,1,1);
            warehouseTitle = Box(shelfContent,"Warehouse title",0,1,1,1);
            Label(warehouseTitle,"창고");
            warehouseGrid = Grid(shelfContent,world.Warehouse,10,false,"Warehouse");
            trashTitle = Box(shelfContent,"Trash title",0,1,1,1);
            Label(trashTitle,"쓰레기통");
            trashGrid = Grid(shelfContent,world.Trash,6,false,"Trash");
            emptyButton = Box(shelfContent,"Empty trash",0,1,1,1);
            MakeButton(emptyButton,"쓰레기통 비우기",() => {
                if (world.TryEmptyTrash(out string message)) Save();
                status.text = message;
            });
        }
        status = Label(Box(pageRoot,"Status",.02f,.095f,.98f,.15f),"");
        lastSize = Vector2.zero;
        Layout();
    }

    EquipmentSlotGrid Grid(RectTransform parent, InventoryData bag, int columns, bool readOnly, string name)
    {
        var rect = Box(parent,name,0,1,0,1);
        rect.pivot = new Vector2(0,1);
        var grid = rect.gameObject.AddComponent<EquipmentSlotGrid>();
        grid.Columns = columns; grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        var presenter = rect.gameObject.AddComponent<InventoryItemPresenter>();
        presenter.ConfigureGrid(original,rect,bag,readOnly);
        return grid;
    }
    static void Save() => GameBootstrap.Instance?.SaveSystem?.RequestSave();

    void LateUpdate()
    {
        Rect area = Screen.safeArea;
        if (Screen.width > 0 && Screen.height > 0) {
            safe.anchorMin = new Vector2(area.xMin / Screen.width,area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width,area.yMax / Screen.height);
        }
        if (page != Page.Apiary && safe.rect.size != lastSize) Layout();
    }

    void Layout()
    {
        lastSize = safe.rect.size;
        bool wide = lastSize.x > lastSize.y;
        Anchors(inventoryRegion, .025f, wide ? .18f : .68f, wide ? .35f : .975f, .98f);
        Anchors(shelfViewport,wide ? .38f : .025f,.16f,.975f,wide ? .96f : .67f);
        float invHeight = inventoryRegion.rect.height;
        FitGrid(inventoryGrid,world.Inventory.Capacity,inventoryRegion.rect.width,invHeight*.78f,invHeight*.18f);
        if (page != Page.Warehouse) return;
        float width = shelfViewport.rect.width;
        float height = shelfViewport.rect.height;
        float heading = Mathf.Min(32,height*.07f);
        float action = Mathf.Min(52,height*.1f);
        float forSlots = Mathf.Max(1,height - heading*2 - action - 8);
        float y = 0;
        Header(warehouseTitle,y,heading); y += heading;
        y += FitGrid(warehouseGrid,world.Warehouse.Capacity,width,forSlots*10/12,y) + 4;
        Header(trashTitle,y,heading); y += heading;
        y += FitGrid(trashGrid,world.Trash.Capacity,width,forSlots*2/12,y) + 4;
        Header(emptyButton,y,action);
    }

    static void Header(RectTransform rect,float top,float height)
    {
        rect.pivot = new Vector2(.5f,1);
        rect.anchoredPosition = new Vector2(0,-top);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
    }

    static float FitGrid(EquipmentSlotGrid grid,int count,float width,float height,float top)
    {
        var style = ActionButtonManager.CatalogFor(grid.transform).Find(ActionButtonId.InventorySlot);
        grid.cellSize = style.size; grid.spacing = style.gridSpacing;
        int rows = Mathf.CeilToInt(count/(float)grid.Columns);
        float w = grid.Columns * style.size.x + (grid.Columns-1)*grid.spacing.x;
        float h = rows * style.size.y + (rows-1)*grid.spacing.y;
        float scale = Mathf.Max(.001f,Mathf.Min(width/w,height/h,1));
        var rect = (RectTransform)grid.transform;
        rect.sizeDelta = new Vector2(w,h);
        rect.localScale = new Vector3(scale,scale,1);
        rect.anchoredPosition = new Vector2((width-w*scale)*.5f,-top);
        return h*scale;
    }

    static RectTransform Box(Transform parent,string name,float x,float y,float right,float top)
    {
        var r = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        r.SetParent(parent,false); Anchors(r,x,y,right,top);
        return r;
    }
    static void Anchors(RectTransform r,float x,float y,float right,float top)
    {
        r.anchorMin = new Vector2(x,y); r.anchorMax = new Vector2(right,top);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
    static TMP_Text Label(RectTransform parent,string caption)
    {
        var t = GameSaveUI.CreateText(parent,"Label",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        t.text = caption; t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 10; t.fontSizeMax = 24;
        return t;
    }
    static Button MakeButton(RectTransform r,string title,Action action)
    {
        r.offsetMin += new Vector2(2,2); r.offsetMax -= new Vector2(2,2);
        var bg = r.gameObject.AddComponent<Image>(); bg.color = Normal;
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
        button.onClick.AddListener(() => { if (!GameTimeRuntime.InputBlocked) action(); });
        Label(r,title);
        return button;
    }
}
