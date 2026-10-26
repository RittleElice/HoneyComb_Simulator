using UnityEngine;

public class ApiaryCameraState
{
    public bool HasSavedState { get; private set; }

    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }

    public float FieldOfView { get; private set; }
    public float OrthographicSize { get; private set; }

    public void Save(Camera camera)
    {
        if (camera == null)
            return;

        Position = camera.transform.position;
        Rotation = camera.transform.rotation;

        FieldOfView = camera.fieldOfView;
        OrthographicSize = camera.orthographicSize;

        HasSavedState = true;
    }

    public void Restore(Camera camera)
    {
        if (camera == null || !HasSavedState)
            return;

        camera.transform.SetPositionAndRotation(
            Position, Rotation);

        camera.fieldOfView = FieldOfView;
        camera.orthographicSize = OrthographicSize;
    }

    public void Clear()
    {
        HasSavedState = false;
    }
}