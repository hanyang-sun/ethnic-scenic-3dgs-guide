using UnityEngine;

/// <summary>
/// 原型零件 1｜漫游相机   —— 归成员 A
/// 挂到 Main Camera 上。
///
/// 操作：W/S 前后，A/D 左右，按住鼠标右键拖动转视角，Shift 加速。
///
/// 碰撞：如果同一个物体上挂了 CharacterController，脚本会自动用它移动，
///       相机就会被场景里的碰撞体挡住，不会穿过地板和墙。
///
/// 注意：移动方向取自"水平面投影"，所以抬头看天时按 W 不会飞起来。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CameraController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 3.0f;
    public float boostMultiplier = 3.0f;

    [Header("视角")]
    public float lookSensitivity = 2.0f;

    [Header("重力")]
    [Tooltip("勾上之后会被重力拉向地面，需要场景里有地板碰撞体")]
    public bool useGravity = true;
    public float gravity = -12f;

    [Header("调试")]
    [Tooltip("勾上后 Z/X 可以自由升降，用来检查场景。正式走动时关掉")]
    public bool allowVerticalFly = false;

    CharacterController controller;
    float yaw;
    float pitch;
    float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
    }

    void Update()
    {
        // ---------- 视角 ----------
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // ---------- 水平移动 ----------
        // 投影到水平面，避免抬头时按 W 往上飞
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += fwd;
        if (Input.GetKey(KeyCode.S)) dir -= fwd;
        if (Input.GetKey(KeyCode.D)) dir += right;
        if (Input.GetKey(KeyCode.A)) dir -= right;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);
        Vector3 motion = dir.normalized * speed * Time.deltaTime;

        // ---------- 垂直 ----------
        if (allowVerticalFly)
        {
            if (Input.GetKey(KeyCode.X)) motion += Vector3.up * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.Z)) motion += Vector3.down * speed * Time.deltaTime;
            verticalVelocity = 0f;
        }
        else if (useGravity && controller != null)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                // 贴地时给一个很小的向下速度，保证 isGrounded 稳定
                verticalVelocity = -2f;
            }
            verticalVelocity += gravity * Time.deltaTime;
            motion += Vector3.up * verticalVelocity * Time.deltaTime;
        }

        // ---------- 应用 ----------
        if (controller != null && controller.enabled)
        {
            controller.Move(motion);
        }
        else
        {
            transform.position += motion;
        }
    }
}
