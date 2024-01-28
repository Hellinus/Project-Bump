using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using _Bump.Scripts.Player;
using UnityEngine;

namespace _Bump.Scripts.Interact
{
    public class InteractBumpObject : InteractBase
    {
        [Header("Property")]
        [MMReadOnly]public float Mass;
        [MMReadOnly] public Vector2 FinalVector;

        [Header("BumpInteract")]
        [Tooltip("Extra force to apply.")]
        public Vector2 ExtraVector;
        [Tooltip("FinalMultiplier = BaseMultiplier * Mass * ... .")]
        public float BaseMultiplier = 500;
        [MMReadOnly]public float FinalMultiplier;
        
        protected Rigidbody2D _rigidbody;
        protected Vector2 _tempVector;
        
        
        protected override void Initialization()
        {
            base.Initialization();
            _rigidbody = GetComponent<Rigidbody2D>();
            if (_rigidbody == null)
            {
                Debug.LogError("Can't find Rigidbody2D on " + this.name);
            }
            Mass = _rigidbody.mass;
            // TODO: bump value;
            FinalMultiplier = BaseMultiplier / Mass;
        }
        
    }
}
