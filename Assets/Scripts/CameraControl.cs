using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public Transform player;
    public Rigidbody2D playerRB;
    public float cameraSpeed = 5f;
    public float DeadZoneX = 2f;
    public float DeadZoneY = 0.5f;
    private float newX;
    private float newY;

    void Start()
    {
        //transform.position = player.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (Mathf.Abs(transform.position.x - player.position.x) > DeadZoneX)
        {
            newX = transform.position.x + cameraSpeed * Time.deltaTime * (player.position.x - transform.position.x);
        }
        // else
        // {
        //     newX = transform.position.x + player.position.x / 2;
        // }
        if (Mathf.Abs(transform.position.y - player.position.y) > DeadZoneY)
        {
            newY = transform.position.y + cameraSpeed * Time.deltaTime * (player.position.y - transform.position.y);
        }
        // else
        // {
        //     newY = transform.position.y + player.position.y / 2;
        // }

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}
