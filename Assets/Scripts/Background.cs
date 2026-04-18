using UnityEngine;

/// <summary>
/// 背景严格跟随相机（同帧 LateUpdate 对齐位置）；可选额外偏移、平滑与漂移。
/// </summary>
[DefaultExecutionOrder(100)]
public class Background : MonoBehaviour
{
    [SerializeField] private Transform mainCamera;

    [Tooltip("在跟随目标上额外叠加的偏移（世界坐标）")]
    [SerializeField] private Vector3 extraWorldOffset;

    [Tooltip("开启后与相机之间会有轻微正弦漂移（关闭则为严格贴合）")]
    [SerializeField] private bool enableDrift;

    [Tooltip("小幅漂移振幅（世界单位），仅当 enableDrift 为真时生效")]
    [SerializeField] private Vector2 driftAmplitude = new Vector2(0.08f, 0.05f);

    [Tooltip("小幅漂移频率")]
    [SerializeField] private float driftFrequency = 0.35f;

    [Tooltip("0：每帧与目标重合；>0：SmoothDamp 滞后（非严格跟随）")]
    [SerializeField] private float smoothTime;

    private float fixedZ;
    private Vector3 cameraToBackgroundOffset;
    private bool hasCapturedOffset;
    private Vector3 smoothVelocity;

    private void Awake()
    {
        fixedZ = transform.position.z;
    }

    private void LateUpdate()
    {
        if (mainCamera == null && Camera.main != null)
            mainCamera = Camera.main.transform;

        if (mainCamera == null)
            return;

        if (!hasCapturedOffset)
        {
            cameraToBackgroundOffset = transform.position - mainCamera.position;
            hasCapturedOffset = true;
        }

        Vector3 drift = Vector3.zero;
        if (enableDrift && (driftAmplitude.x != 0f || driftAmplitude.y != 0f))
        {
            drift = new Vector3(
                Mathf.Sin(Time.time * driftFrequency) * driftAmplitude.x,
                Mathf.Cos(Time.time * driftFrequency * 0.83f) * driftAmplitude.y,
                0f);
        }

        Vector3 target = mainCamera.position + cameraToBackgroundOffset + extraWorldOffset + drift;
        target.z = fixedZ;

        if (smoothTime <= 0.0001f)
            transform.position = target;
        else
            transform.position = Vector3.SmoothDamp(transform.position, target, ref smoothVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }
}
