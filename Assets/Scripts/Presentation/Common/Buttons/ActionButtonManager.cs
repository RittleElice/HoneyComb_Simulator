using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActionButtonManager : MonoBehaviour
{
    [SerializeField] private ActionButtonCatalog catalog;
    public ActionButtonCatalog Catalog { get => catalog; set => catalog = value; }

    public static ActionButtonCatalog CatalogFor(Transform target)
    {
        var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return Resources.Load<ActionButtonCatalog>("ActionButtonCatalog");
        var manager = canvas.rootCanvas.GetComponent<ActionButtonManager>();
        if (manager == null) manager = canvas.rootCanvas.gameObject.AddComponent<ActionButtonManager>();
        if (manager.catalog == null) manager.catalog = Resources.Load<ActionButtonCatalog>("ActionButtonCatalog");
        return manager.catalog;
    }

    public static ActionButtonView Adopt(Button button, ActionButtonId id, string label = null)
    {
        if (button == null) return null;
        var canvas = button.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        var resolved = CatalogFor(button.transform);
        var manager = canvas.rootCanvas.GetComponent<ActionButtonManager>();
        if (resolved == null || resolved.Find(id) == null)
            Debug.LogError("[Buttons] Missing catalog entry: " + id, button);
        var view = button.GetComponent<ActionButtonView>();
        if (view == null) view = button.gameObject.AddComponent<ActionButtonView>();
        view.Bind(manager, id, label);
        return view;
    }

    public static Button Create(Button fallback, Transform parent, ActionButtonId id, string label = null)
    {
        var entry = CatalogFor(parent)?.Find(id);
        var prefab = entry != null && entry.prefab != null ? entry.prefab : fallback;
        if (prefab == null) throw new InvalidOperationException("No button prefab for " + id);
        var button = Instantiate(prefab, parent, false);
        Adopt(button, id, label);
        return button;
    }

    public static void SetVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }
}
