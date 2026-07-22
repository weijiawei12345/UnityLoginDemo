using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAtk : MonoBehaviour
{

    public int atk=1;
    public Transform attackPoint;
    public float attackRange=1f;
    public LayerMask playerLayer;
    public float weaponRange=1f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        
        if(collision.gameObject.CompareTag("Player"))
        {
            collision.gameObject.GetComponent<PlayerHp>().GetDamage(atk);
        }
    }

    public void Attack()
    {
        Debug.Log("Attack");
        Collider2D[] hitPlayers=Physics2D.OverlapCircleAll(attackPoint.position,weaponRange,playerLayer);
        foreach(Collider2D player in hitPlayers)
        {
            player.GetComponent<PlayerHp>().GetDamage(atk);
        }
    }
}
