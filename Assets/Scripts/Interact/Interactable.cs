using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public abstract class Interactable : MonoBehaviour {

    [Header("Interact Icon")]
    [SerializeField] private InteractIcon interactIcon;

    protected void Start() {

        interactIcon = GetComponentInChildren<InteractIcon>();
        interactIcon.Hide(); // hide the interact icon by default

    }

    public void ShowInteractIcon() => interactIcon.Show();

    public void HideInteractIcon() => interactIcon.Hide();

    public virtual void OnInteract() => interactIcon.OnInteract();

}
