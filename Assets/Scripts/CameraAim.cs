using Unity.Cinemachine;
using UnityEngine;

// Смещение камеры в сторону прицела. Сам добавляется на CinemachineCamera при первом вызове.
public class CameraAim : CinemachineExtension
{
    public float smooth = 8f;

    static CameraAim instance;

    Vector2 targetOffset;
    Vector2 offset;

    public static void SetOffset(Vector2 value)
    {
        if (instance == null)
        {
            if (value == Vector2.zero)
                return;

            CinemachineCamera cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera == null)
                return;

            instance = cinemachineCamera.GetComponent<CameraAim>();
            if (instance == null)
                instance = cinemachineCamera.gameObject.AddComponent<CameraAim>();
        }

        instance.targetOffset = value;
    }

    void Update()
    {
        offset = Vector2.Lerp(offset, targetOffset, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body || offset.sqrMagnitude < 0.0001f)
            return;

        state.PositionCorrection += (Vector3)offset;
    }
}
