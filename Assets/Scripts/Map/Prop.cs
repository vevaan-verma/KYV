
using UnityEngine;

public class Prop : MonoBehaviour {

    [Header("Variations")]
    [SerializeField] private GameObject[] variations;

    [Header("Properties")]
    [SerializeField] private int width;
    [SerializeField] private int height;
    [Space]
    [SerializeField] private int topMargin;
    [SerializeField] private int bottomMargin;
    [SerializeField] private int leftMargin;
    [SerializeField] private int rightMargin;

    public GameObject GetRandomVariation() => variations[Random.Range(0, variations.Length)];

    public int GetWidth() => width;

    public int GetHeight() => height;

    public int GetTopMargin() => topMargin;

    public int GetBottomMargin() => bottomMargin;

    public int GetLeftMargin() => leftMargin;

    public int GetRightMargin() => rightMargin;

    private void OnDrawGizmos() {

        Matrix4x4 oldMatrix = Gizmos.matrix; // store the current matrix
        Gizmos.matrix = transform.localToWorldMatrix; // set the matrix to the object's matrix

        // draw the object's bounds
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawCube(new Vector3(width / 2f, height / 2f, 0f), new Vector3(width, height, 1f)); // draw the object's bounds (no need to account for object position offset since the matrix is set to the object's matrix / matrix is local now)

        // draw the margins
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawCube(new Vector3(width / 2f, height + topMargin / 2f, 0f), new Vector3(width, topMargin, 1f)); // draw the top margin
        Gizmos.DrawCube(new Vector3(width / 2f, -bottomMargin / 2f, 0f), new Vector3(width, bottomMargin, 1f)); // draw the bottom margin
        Gizmos.DrawCube(new Vector3(-leftMargin / 2f, height / 2f, 0f), new Vector3(leftMargin, height, 1f)); // draw the left margin
        Gizmos.DrawCube(new Vector3(width + rightMargin / 2f, height / 2f, 0f), new Vector3(rightMargin, height, 1f)); // draw the right margin

        Gizmos.matrix = oldMatrix; // restore the matrix

    }
}
