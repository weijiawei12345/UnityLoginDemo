using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerHp : MonoBehaviour
{
    public int curHp=3;
    public int maxHp=3;
    public TMP_Text hpText;
    public Animator animator;

    void Start()
    {
        hpText.text="HP:"+curHp+"/"+maxHp;
    }

    public void GetDamage(int damage)
    {
        curHp-=damage;

        animator.Play("HPTextUpdate");

        if(curHp<=0)
        {
            this.gameObject.SetActive(false);
        }

        hpText.text="HP:"+curHp+"/"+maxHp;
    }
}
