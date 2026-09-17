// Dan/noc: desna palica GORE/DOLE pomera doba dana (horizontala ostaje snap-turn).
using UnityEngine;
using UnityEngine.InputSystem;

public class DayNight : MonoBehaviour
{
    public Light sun;
    [Range(0, 1)] public float time01 = 0.4f;   // 0 = zora, 0.5 = podne, 1 = noc
    InputAction stick;
    RoutePicker routePicker;

    void Start()
    {
        routePicker = GetComponent<RoutePicker>();
        stick = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/thumbstick");
        stick.Enable();
        Apply();
    }

    void Update()
    {
        if (routePicker != null && routePicker.RightStickReserved) return;
        float v = stick.ReadValue<Vector2>().y;
        if (Mathf.Abs(v) > 0.5f)
        {
            time01 = Mathf.Clamp01(time01 + v * Time.deltaTime * 0.15f);
            Apply();
        }
    }

    void Apply()
    {
        if (sun == null) return;
        // 0 -> 10deg (zora), 0.5 -> 80deg (podne), 1 -> -30deg (noc, ispod horizonta)
        float angle = Mathf.Lerp(8f, 80f, Mathf.Sin(time01 * Mathf.PI));
        if (time01 > 0.92f) angle = Mathf.Lerp(8f, -30f, (time01 - 0.92f) / 0.08f);
        sun.transform.rotation = Quaternion.Euler(angle, -35f, 0);

        float dayFactor = Mathf.Clamp01(Mathf.Sin(time01 * Mathf.PI) * 1.2f);
        sun.intensity = Mathf.Lerp(0.02f, 1.15f, dayFactor);
        sun.color = Color.Lerp(new Color(1f, 0.55f, 0.3f), Color.white,
                               Mathf.Clamp01(dayFactor * 1.6f - 0.2f));
        RenderSettings.ambientLight = Color.Lerp(
            new Color(0.1f, 0.12f, 0.2f), new Color(0.6f, 0.65f, 0.72f), dayFactor);
        var cam = Camera.main;
        if (cam != null)
            cam.backgroundColor = Color.Lerp(
                new Color(0.008f, 0.01f, 0.02f), new Color(0.03f, 0.04f, 0.055f), dayFactor);
    }
}
