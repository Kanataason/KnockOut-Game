using UnityEngine;
using UnityEngine.Serialization;

public class AnimationStateMachine : StateMachineBehaviour
{
    [SerializeField] private string TriggerName;
    [FormerlySerializedAs("IntergerName")] [SerializeField] private string IntegerName;
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //var at = animator.GetComponent<TatsuAnimationController>();
        //if(at != null)
        //{
        //    at.AttackOff();
        //}
        //animator.ResetTrigger(TriggerName) ;

    }
}
