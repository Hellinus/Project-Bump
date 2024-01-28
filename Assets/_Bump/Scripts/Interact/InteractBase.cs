using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using _Bump.Scripts.Player;
using UnityEngine;

namespace _Bump.Scripts.Interact
{
    public class InteractBase : MonoBehaviour
    {
        protected Collider2D _collider;

        protected virtual void Start()
        {
            Initialization();
        }

        protected virtual void Initialization()
        {
            _collider = GetComponent<Collider2D>();
        }

        protected virtual void Update()
        {
            
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            
        }
    }
}
