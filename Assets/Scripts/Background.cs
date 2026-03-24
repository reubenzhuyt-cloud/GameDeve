using UnityEngine;

public class Background : MonoBehaviour
{
    public Transform mainCamera;
    private Vector3 mainCameraPosition;
    private Vector3 backgroundPosition;

    void Awake()
    {

    }
    void Start()
    {
        mainCameraPosition = mainCamera.position;
        backgroundPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 newPosition = backgroundPosition + (mainCamera.position - mainCameraPosition) * 0.95f;
        transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);
    }
}
