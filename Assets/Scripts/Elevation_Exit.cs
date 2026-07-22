using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevation_Exit : MonoBehaviour
{
    public Collider2D[] monutainColliders;

    public Collider2D[] boundaryColliders;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.tag != "Player") return;
        foreach (var collider in monutainColliders)
        {
            collider.enabled = true;   
        }
        
        foreach (var collider in boundaryColliders)
        {
            collider.enabled = false;   
        }

        collision.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 5;
    }
}
