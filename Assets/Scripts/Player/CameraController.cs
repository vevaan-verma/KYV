using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour {

    [Header("References")]
    private Transform target;
    private Coroutine shakeCoroutine;

    [Header("Settings")]
    [SerializeField] private float followLerpSpeed;
    private float zOffset;

    public void Initialize(Transform target) {

        this.target = target;
        zOffset = transform.position.z - target.position.z;

    }

    private void LateUpdate() => transform.position = Vector3.Lerp(transform.position, target.position + new Vector3(0f, 0f, zOffset), followLerpSpeed * Time.deltaTime);

    public void ShakeCamera(float duration, float magnitude) {

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(HandleCameraShake(duration, magnitude));

    }

    private IEnumerator HandleCameraShake(float duration, float magnitude) {

        Vector3 originalPosition = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration) {

            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = new Vector3(originalPosition.x + x, originalPosition.y + y, originalPosition.z);
            elapsed += Time.unscaledDeltaTime; // use unscaled delta time so the shake doesn't get stuck when the game is paused
            yield return null;

        }

        transform.localPosition = originalPosition;

    }
}
