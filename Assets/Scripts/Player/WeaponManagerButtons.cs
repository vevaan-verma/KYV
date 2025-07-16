#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponManager))]
public class WeaponManagerButtons : Editor {

    public override void OnInspectorGUI() {

        DrawDefaultInspector();

        WeaponManager trackWeapon = (WeaponManager)target;

        if (GUILayout.Button("Stop Tracking Weapon Speed"))
            trackWeapon.ShouldTrackSpeed(false);

        if (GUILayout.Button("Start Tracking Weapon Speed"))
            trackWeapon.ShouldTrackSpeed(true);

        if (GUILayout.Button("Set Origin to Player"))
            trackWeapon.SetOrigin(FindAnyObjectByType<PlayerController>().gameObject);

        if (GUILayout.Button("Set Origin to Main Camera"))
            trackWeapon.SetOrigin(FindAnyObjectByType<CameraController>().gameObject);

    }
}
#endif
