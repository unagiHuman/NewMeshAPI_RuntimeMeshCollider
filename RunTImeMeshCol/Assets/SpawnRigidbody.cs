using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnRigidbody : MonoBehaviour
{

    [SerializeField] private GameObject _rigidbody;

    [SerializeField] private int _maxCount = 1000;

    private Queue<GameObject> _cash = new Queue<GameObject>();
    
    private IEnumerator Start()
    {
        var wait = new WaitForSeconds(0.05f);
        while (true)
        {

            if (_cash.Count > _maxCount)
            {
                while (true)
                {
                    if (_cash.Count < _maxCount * 0.8f)
                    {
                        break;
                    }
                    
                    var del = _cash.Dequeue();
                    Destroy(del);

                    yield return null;
                }

                yield return new WaitForSeconds(1f);
            }
            var go = GameObject.Instantiate<GameObject>(_rigidbody);
            go.transform.position = this.transform.position + 0.2f * Random.insideUnitSphere;
            _cash.Enqueue(go);
            
            yield return wait;
        }
    }
}
