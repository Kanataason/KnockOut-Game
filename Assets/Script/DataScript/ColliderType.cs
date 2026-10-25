
using UnityEngine;
using UnityEngine.UIElements;

public enum ColliderTypes
{
    HitBox,
    HurtBox,
    ThrowBox,
    ThrowHurtBox,
    SubHurtBox
}
public class ColliderType: MonoBehaviour
{
    public ColliderTypes Types;
    [HideInInspector]public Collider MyCollider;

    private BoxCollider box;
    private void Awake()
    {
        MyCollider = GetComponent<Collider>();
        //box = GetComponent<BoxCollider>();
    }
    //private void OnDrawGizmos()
    //{
    //    if (box == null) return;

    //    Gizmos.color = Color.red;

    //    Gizmos.matrix = box.transform.localToWorldMatrix;
    //    Gizmos.DrawWireCube(box.center, box.size);
    //}
}
