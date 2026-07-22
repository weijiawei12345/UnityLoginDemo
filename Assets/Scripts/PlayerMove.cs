using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
    public Rigidbody2D rb;
    private float moveX;
    private float moveY;
    public Animator animator;
    public int faceDirection = 1;
    
    void FixedUpdate()//物理更新，确保移动速度不受帧率影响
    {
        moveX = Input.GetAxis("Horizontal");
        moveY = Input.GetAxis("Vertical");
        animator.SetFloat("vertical", Mathf.Abs(moveY));
        animator.SetFloat("horizontal", Mathf.Abs(moveX));
        if(moveX>0&&this.transform.localScale.x<0||
           moveX<0&&this.transform.localScale.x>0)//判断是否需要翻转
        {
            Flip();
        }
        
    }

    void Update()//更新，处理输入
    {
        rb.velocity = new Vector2(moveX * speed, moveY * speed);
    }

    void Flip()//翻转角色方向
    {
        faceDirection *= -1;
        this.transform.localScale = new Vector3(this.transform.localScale.x * -1, this.transform.localScale.y, this.transform.localScale.z);
    }
}
