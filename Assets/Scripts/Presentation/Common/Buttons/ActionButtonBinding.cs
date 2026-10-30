using UnityEngine;
using UnityEngine.UI;

// Scene-owned buttons keep their original persistent/runtime click listeners.
[DisallowMultipleComponent, RequireComponent(typeof(Button)), DefaultExecutionOrder(-200)]
public sealed class ActionButtonBinding : MonoBehaviour
{
    [SerializeField] private ActionButtonId id;
    public ActionButtonId Id { get => id; set => id = value; }
    private void Awake() => ActionButtonManager.Adopt(GetComponent<Button>(), id);
}
