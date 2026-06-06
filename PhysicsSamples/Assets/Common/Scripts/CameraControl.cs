using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float lookSpeedH = 2f;
    public float lookSpeedV = 2f;
    public float zoomSpeed = 2f;
    public float dragSpeed = 5f;

    private float yaw;
    private float pitch;


    private void Start()
    {
        // x - 右螺距
        // y 向上偏航
        // z - 前滚翻
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        if (!enabled) return;

        if (Input.touchCount > 0)
        {
            float touchToMouseScale = 0.25f;
            // 第一次接触时环顾四周
            Touch t0 = Input.GetTouch(0);
            yaw += lookSpeedH * touchToMouseScale * t0.deltaPosition.x;
            pitch -= lookSpeedV * touchToMouseScale * t0.deltaPosition.y;
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);

            // 如果有额外的接触，也向前飞
            if (Input.touchCount > 1)
            {
                Touch t1 = Input.GetTouch(1);
                Vector3 offset = new Vector3(t1.deltaPosition.x, 0, t1.deltaPosition.y);
                transform.Translate(offset * Time.deltaTime * touchToMouseScale, Space.Self);
            }
        }
        else
        {
            //用鼠标右键环顾四周
            if (Input.GetMouseButton(1))
            {
                yaw += lookSpeedH * Input.GetAxis("Mouse X");
                pitch -= lookSpeedV * Input.GetAxis("Mouse Y");

                transform.eulerAngles = new Vector3(pitch, yaw, 0f);

                Vector3 offset = Vector3.zero;
                float offsetDelta = Time.deltaTime * dragSpeed;
                if (Input.GetKey(KeyCode.LeftShift)) offsetDelta *= 5.0f;
                if (Input.GetKey(KeyCode.S)) offset.z -= offsetDelta;
                if (Input.GetKey(KeyCode.W)) offset.z += offsetDelta;
                if (Input.GetKey(KeyCode.A)) offset.x -= offsetDelta;
                if (Input.GetKey(KeyCode.D)) offset.x += offsetDelta;
                if (Input.GetKey(KeyCode.Q)) offset.y -= offsetDelta;
                if (Input.GetKey(KeyCode.E)) offset.y += offsetDelta;

                transform.Translate(offset, Space.Self);
            }

            //使用鼠标中键拖动相机
            if (Input.GetMouseButton(2))
            {
                transform.Translate(-Input.GetAxisRaw("Mouse X") * Time.deltaTime * dragSpeed, -Input.GetAxisRaw("Mouse Y") * Time.deltaTime * dragSpeed, 0);
            }

            //使用鼠标滚轮放大和缩小
            transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);
        }
    }
}
