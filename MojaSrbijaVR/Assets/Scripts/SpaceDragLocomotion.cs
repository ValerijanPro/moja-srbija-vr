// "Uhvati vazduh i povuci se": grip u prazno vuce svet (pomeras se rukama),
// obe ruke = jos i okretanje. Radi paralelno sa palicama.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SpaceDragLocomotion : MonoBehaviour
{
    public Transform rig;              // XR Origin (XR Rig)
    InputAction gripL, gripR;
    Transform handL, handR;
    NearFarInteractor intL, intR;
    bool dragL, dragR;
    Vector3 prevL, prevR;              // pozicije u LOKALU riga

    void Start()
    {
        gripL = new InputAction(type: InputActionType.Value, binding: "<XRController>{LeftHand}/grip");
        gripR = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/grip");
        gripL.Enable(); gripR.Enable();
    }

    void FindHands()
    {
        if (handL == null)
        {
            var go = GameObject.Find("Left Controller");
            if (go != null) { handL = go.transform; intL = go.GetComponentInChildren<NearFarInteractor>(); }
        }
        if (handR == null)
        {
            var go = GameObject.Find("Right Controller");
            if (go != null) { handR = go.transform; intR = go.GetComponentInChildren<NearFarInteractor>(); }
        }
    }

    Vector3 LocalPos(Transform hand) => rig.InverseTransformPoint(hand.position);

    void Update()
    {
        if (rig == null) return;
        FindHands();
        if (handL == null || handR == null) return;

        bool wantL = gripL.ReadValue<float>() > 0.7f;
        bool wantR = gripR.ReadValue<float>() > 0.7f;
        // ne otimaj se sa hvatanjem makete
        if (wantL && !dragL && intL != null && intL.hasSelection) wantL = false;
        if (wantR && !dragR && intR != null && intR.hasSelection) wantR = false;

        if (wantL && !dragL) { dragL = true; prevL = LocalPos(handL); }
        if (wantR && !dragR) { dragR = true; prevR = LocalPos(handR); }
        if (!wantL) dragL = false;
        if (!wantR) dragR = false;

        if (dragL && dragR)
        {
            var curL = LocalPos(handL); var curR = LocalPos(handR);
            var prevMid = (prevL + prevR) * 0.5f;
            var curMid = (curL + curR) * 0.5f;
            var delta = curMid - prevMid;
            rig.position -= rig.TransformVector(new Vector3(delta.x, 0, delta.z));

            // rotacija oko glave: ugao izmedju vektora ruku
            var pv = prevR - prevL; var cv = curR - curL;
            float aPrev = Mathf.Atan2(pv.x, pv.z), aCur = Mathf.Atan2(cv.x, cv.z);
            float dYaw = Mathf.DeltaAngle(aCur * Mathf.Rad2Deg, aPrev * Mathf.Rad2Deg);
            var head = Camera.main != null ? Camera.main.transform.position : rig.position;
            rig.RotateAround(head, Vector3.up, dYaw);

            prevL = LocalPos(handL); prevR = LocalPos(handR);
        }
        else if (dragL || dragR)
        {
            var hand = dragL ? handL : handR;
            var prev = dragL ? prevL : prevR;
            var cur = LocalPos(hand);
            var delta = cur - prev;
            rig.position -= rig.TransformVector(new Vector3(delta.x, 0, delta.z));
            if (dragL) prevL = LocalPos(handL); else prevR = LocalPos(handR);
        }
    }
}
