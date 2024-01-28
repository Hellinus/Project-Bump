using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using _Bump.Scripts.Player;
using UnityEngine;

namespace _Bump.Scripts.Interact
{
    public class InteractCrate : InteractBumpObject
    {
        protected override void Initialization()
        {
            base.Initialization();
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("BumpDetection"))
            {
                Debug.Log("detect");
                var position = this.transform.position;
                Vector2 hitPos = other.bounds.ClosestPoint(position);
                _tempVector.x = position.x - hitPos.x;
                _tempVector.y = position.y - hitPos.y;
                FinalVector.x = _tempVector.normalized.x + ExtraVector.x;
                FinalVector.y = _tempVector.normalized.y + ExtraVector.y;
                _rigidbody.AddForce(FinalVector * FinalMultiplier);
            }
        }
    }
}
