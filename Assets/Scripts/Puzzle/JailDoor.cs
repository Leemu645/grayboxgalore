using UnityEngine;

/// <summary>
/// Slides the barred door aside once the jail key is used on the lock.
/// </summary>
public class JailDoor : MonoBehaviour
{
    [SerializeField] float slideDistance = 1.55f;
    [SerializeField] float slideSpeed = 1.6f;

    Vector3 closed;
    Vector3 open;
    bool opening;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        closed = transform.localPosition;
        open = closed + new Vector3(slideDistance, 0f, 0f);
    }

    void Update()
    {
        if (!opening)
            return;

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, open, slideSpeed * Time.deltaTime);
        if (transform.localPosition == open)
            opening = false;
    }

    public void Open()
    {
        IsOpen = true;
        opening = true;
    }
}
