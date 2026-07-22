using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,
    Dead
}

public class Enemy_Movement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform player;
    private int facingDirection=1;
    private EnemyState currentState;
    
    
    public float atkDelay=1f;
    private float atkTimer;
    public float attackRange=1f;
    public Animator animator;
    public float speed=1f;
 
    // Start is called before the first frame update
    void Start()
    {
        rb=GetComponent<Rigidbody2D>();
        animator=GetComponent<Animator>();
        SetEnemyState(EnemyState.Idle);
        atkTimer=atkDelay;
    }

    // Update is called once per frame
    void Update()
    {
        atkTimer+=Time.deltaTime;
        if (currentState == EnemyState.Chasing)
        {
            ChasePlayer();
        }
        else if (currentState == EnemyState.Attacking)
        {
            rb.velocity = Vector2.zero;
           
        }
    }

    private void ChasePlayer()
    {
        if (Vector2.Distance(transform.position, player.position) <= attackRange && atkTimer>=atkDelay)
        {
            Debug.Log("Attack");    
            atkTimer=0f;
            SetEnemyState(EnemyState.Attacking);
        }
        else if (player.position.x > transform.position.x && facingDirection == -1
        || player.position.x < transform.position.x && facingDirection == 1)
        {
            flip();
        }

        Vector2 direction = (player.position - transform.position).normalized;
        rb.velocity = direction * speed;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            if(player==null)
            {
                player=collision.gameObject.transform;
            }
            
            // OnTriggerStay2D runs every physics step. Do not overwrite an
            // active attack or it will restart the attack clip continuously.
           
            if(currentState==EnemyState.Attacking)
            {
                return;
            }
            SetEnemyState(EnemyState.Chasing);
            
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            player=null;
            rb.velocity=Vector2.zero;   
            SetEnemyState(EnemyState.Idle);
        }
    }

    private void flip()
    {
        facingDirection*=-1;
        this.transform.localScale=new Vector3(this.transform.localScale.x*-1,this.transform.localScale.y,this.transform.localScale.z);
    }
    
    public void SetEnemyState(EnemyState state)
    {
        switch(currentState)
        {
            case EnemyState.Idle:
                animator.SetBool("isIdle",false);
                break;
            case EnemyState.Chasing:
                animator.SetBool("isChasing",false);
                break;
            case EnemyState.Attacking:
                animator.SetBool("isAttacking",false);
                break;
            case EnemyState.Dead:
                animator.SetBool("isDead",false);
                break;
        }

        currentState=state;

        switch(currentState)
        {
            case EnemyState.Idle:
                animator.SetBool("isIdle",true);
                break;
            case EnemyState.Chasing:
                animator.SetBool("isChasing",true);
                break;
            case EnemyState.Attacking:
                animator.SetBool("isAttacking",true);
                break;
            case EnemyState.Dead:
                animator.SetBool("isDead",true);
                break;
        }
    }

}
