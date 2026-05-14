using UnityEngine;
using UnityEngine.AI;

public class RootMotionSync : MonoBehaviour
{
    private Animator anim;
    private NavMeshAgent agent;
    private AttackBase attackBase;

    void Awake()
    {
        anim       = GetComponent<Animator>();
        agent      = GetComponent<NavMeshAgent>();
        attackBase = GetComponent<AttackBase>();
    }

    void Start()
    {
        if (agent == null) return;

        // updatePosition은 true 유지 — NavMeshAgent가 이동 담당
        agent.updatePosition = true;
        agent.updateRotation = false; // 회전만 Root Motion으로 제어
    }

    void OnAnimatorMove()
    {
        if (anim == null || agent == null) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // 회전만 Root Motion에서 가져옴
        // 위치는 NavMeshAgent가 알아서 제어
        transform.rotation = anim.rootRotation;
    }
}