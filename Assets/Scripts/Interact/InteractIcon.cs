using System.Collections;
using UnityEngine;

public class InteractIcon : MonoBehaviour {

    [Header("References")]
    private SpriteRenderer spriteRenderer;
    private Coroutine fadeCoroutine;
    private Coroutine popCoroutine;

    [Header("Settings")]
    [SerializeField] private float fadeDuration;
    [SerializeField] private float popDuration;
    [SerializeField] private float popScale;
    private Vector3 initialScale;
    private float initialOpacity;

    private void Awake() {

        spriteRenderer = GetComponent<SpriteRenderer>();
        initialOpacity = spriteRenderer.color.a;
        initialScale = transform.localScale;

    }

    public void OnInteract() {

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(Pop());

    }

    public void Show() {

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(Fade(initialOpacity));

    }

    public void Hide() {

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(Fade(0f));

    }

    private IEnumerator Fade(float targetOpacity) {

        float currentTime = 0f;
        Color initialColor = spriteRenderer.color;

        while (currentTime < fadeDuration) {

            spriteRenderer.color = Color.Lerp(initialColor, new Color(initialColor.r, initialColor.g, initialColor.b, targetOpacity), currentTime / fadeDuration);
            currentTime += Time.deltaTime;
            yield return null;

        }

        spriteRenderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, targetOpacity);
        fadeCoroutine = null;

    }

    private IEnumerator Pop() {

        float currentTime = 0f;
        transform.localScale = initialScale; // reset scale

        while (currentTime < popDuration / 2f) {

            transform.localScale = Vector3.Lerp(initialScale, initialScale * popScale, currentTime / (popDuration / 2f));
            currentTime += Time.deltaTime;
            yield return null;

        }

        currentTime = 0f;

        while (currentTime < popDuration / 2f) {

            transform.localScale = Vector3.Lerp(initialScale * popScale, initialScale, currentTime / (popDuration / 2f));
            currentTime += Time.deltaTime;
            yield return null;

        }

        transform.localScale = initialScale;
        popCoroutine = null;

    }
}
