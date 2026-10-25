using UnityEngine;
using UnityEngine.Serialization;

public class HitBoxData : MonoBehaviour
{
    public string ownerTag; //攻撃した側のタグ,攻撃主のタグ（例：PlayerA、PlayerB）
    public int AttackId;
    [FormerlySerializedAs("tatuAttackData")] public ActionFrameData actionFrameData;
}
