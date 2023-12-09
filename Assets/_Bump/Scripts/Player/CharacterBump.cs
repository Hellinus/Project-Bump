using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using Unity.VisualScripting;
using UnityEngine;

namespace _Bump.Scripts.Player
{
	[MMHiddenProperties("AbilityStopFeedbacks")]
	[AddComponentMenu("Corgi Engine/Character/Abilities/Character Bump")] 
    public class CharacterBump : CharacterAbility
    {
        /// the possible bump restrictions
        public enum BumpBehavior
        {
            CanBumpOnGround,
            CanBumpOnGroundAndFromLadders,
            CanBumpAnywhere,
            CantBump
        }

        [Header("Bump Behaviour")]
        
        public CharacterBumpDetection BumpDetection;
        public BumpBehavior BumpRestrictions = BumpBehavior.CanBumpAnywhere;
        public int NumberOfBumps = 2;
        public float ShrinkSpeed = 1f;
        [Tooltip("if this is true, camera offset will be reset on bump")]
        public bool ResetCameraOffsetOnBump = false;
        [Tooltip("if this is true, this character can bump down one way platforms by doing down + bump")]
        public bool CanBumpDownOneWayPlatforms = true;
        [MMReadOnly]
        [Tooltip("the number of bumps left to the character")]
        public int NumberOfBumpsLeft;
        
        
        [Header("Bump Force")]
        
        public float BumpForceMin = 8f;
        public float BumpForceMax = 20f;
        
        [Header("Bump Press Down Time")]
        
        public float BumpPressDownTimeMin = 0.2f;
        public float BumpPressDownTimeMax = 0.7f;
        
        [Header("Bump Detect Radius")]
        
        public float BumpDetectRadiusMin = 0.7f;
        public float BumpDetectRadiusMax = 1.5f;
        public float LerpValue = 0.5f;
        
        [Header("Collisions")]
        
        [Tooltip("duration (in seconds) we need to disable collisions when bumping down a 1 way platform")]
        public float OneWayPlatformsBumpCollisionOffDuration = 0.3f;
        [Tooltip("duration (in seconds) we need to disable collisions when bumping off a moving platform")]
        public float MovingPlatformsBumpCollisionOffDuration = 0.05f;

        [Header("Debug")]
        [MMReadOnly] public float _bumpFactor = 0f;
        [MMReadOnly] public float _bumpRadius = 0f;
        [MMReadOnly] public float _bumpForce = 0f;
        
        protected float _bumpPressDownTime = 0f;
        protected bool _bumpButtonReleased = false;
        protected CharacterCrouch _characterCrouch = null;
        // protected CharacterHorizontalMovement _characterHorizontalMovement;
        protected CharacterButtonActivation _characterButtonActivation = null;
        protected CharacterLadder _characterLadder = null;
        protected int _initialNumberOfBumps;
        protected bool _bumpingDownFromOneWayPlatform = false;
        protected bool _bumpDetecting = false;
        
        

        
        /// <summary>
        /// On Start() we reset our number of bumps
        /// </summary>
        protected override void Initialization()
        {
            base.Initialization();
            ResetNumberOfBumps();
            // _characterWallJump = _character?.FindAbility<CharacterWalljump>();
            _characterHorizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
            _characterCrouch = _character?.FindAbility<CharacterCrouch>();
            _characterButtonActivation = _character?.FindAbility<CharacterButtonActivation>();
            _characterLadder = _character?.FindAbility<CharacterLadder>();
            ResetInitialNumberOfBumps();

            if (BumpDetection == null)
            {
	            Debug.LogWarning("Bump Detection not found.");
            }
        }

        
        /// <summary>
        /// At the beginning of each cycle we check if we've just pressed or released the jump button
        /// </summary>
        protected override void HandleInput()
        {
	        // we handle regular button presses
	        if (_inputManager.BumpButton.State.CurrentState == MMInput.ButtonStates.ButtonPressed)
	        {
		        _bumpPressDownTime += Time.deltaTime;
		        _movement.ChangeState(CharacterStates.MovementStates.Shrinking);
		        if (_characterHorizontalMovement != null)
		        {
			        if (BumpAuthorized)
			        {
				        _characterHorizontalMovement.MovementSpeed = ShrinkSpeed;
			        }
		        }
	        }
	        
	        // we handle button release
	        if (_inputManager.BumpButton.State.CurrentState == MMInput.ButtonStates.ButtonUp)
	        {
		        
		        _movement.ChangeState(CharacterStates.MovementStates.Idle);
		        
		        if (_characterHorizontalMovement != null)
		        {
			        _characterHorizontalMovement.ResetHorizontalSpeed();
		        }
		        
		        if (_bumpPressDownTime >= BumpPressDownTimeMax)
		        {
			        _bumpPressDownTime = BumpPressDownTimeMax;
		        }
		        else if(_bumpPressDownTime <= BumpPressDownTimeMin)
		        {
			        _bumpPressDownTime = BumpPressDownTimeMin;
		        }
			
		        _bumpFactor = (_bumpPressDownTime - BumpPressDownTimeMin) / (BumpPressDownTimeMax - BumpPressDownTimeMin);
		        _bumpRadius = _bumpFactor * (BumpDetectRadiusMax - BumpDetectRadiusMin) + BumpDetectRadiusMin;
		        _bumpForce = _bumpFactor * (BumpForceMax - BumpForceMin) + BumpForceMin;
		        
		        _bumpDetecting = true;
		        Debug.Log(_bumpPressDownTime);
		        _bumpPressDownTime = 0f;
	        }
        }

        /// <summary>
        /// Every frame we perform a number of checks related to bump
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
			HandleDetection();
			UpdateController();
        }

        protected void HandleDetection()
        {
	        if (_bumpDetecting == false) return;
	        
	        if (BumpDetection.CircleCollider.radius < _bumpRadius - 0.05f)
	        {
		        BumpDetection.CircleCollider.radius = Mathf.Lerp(BumpDetection.CircleCollider.radius, _bumpRadius, LerpValue);
	        }
	        else
	        {
		        BumpStart();
		        _bumpDetecting = false;
	        }
	        
	        
        }
        
        /// <summary>
		/// Causes the character to start bumping.
		/// </summary>
		public virtual void BumpStart()
		{
			if (!EvaluateBumpConditions())
			{
				return;
			}
			
			// we reset our walking speed
			if ((_movement.CurrentState == CharacterStates.MovementStates.Crawling)
			    || (_movement.CurrentState == CharacterStates.MovementStates.Crouching)
			    || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing))
			{
				_characterHorizontalMovement.ResetHorizontalSpeed();
			}
			
			// if (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
			// {
			// 	_characterLadder.GetOffTheLadder();
			// 	_characterLadder.BumpFromLadder();
			// }
			
			Vector2 v = BumpDetection.FinalVector.normalized * _bumpForce;
			BumpDetection.Reset();
			_controller.AddHorizontalForce(v.x);
			if (v.y >= 0.1f)
			{
				_controller.SetVerticalForce(v.y);
			}
			else
			{
				_controller.AddVerticalForce(v.y);
			}
		}
        
	    /// <summary>
		/// Evaluates the jump conditions to determine whether or not a jump can occur
		/// </summary>
		/// <returns><c>true</c>, if jump conditions was evaluated, <c>false</c> otherwise.</returns>
		protected virtual bool EvaluateBumpConditions()
		{
			bool onAOneWayPlatform = false;
			if (_controller.StandingOn != null)
			{
				onAOneWayPlatform = (_controller.OneWayPlatformMask.MMContains(_controller.StandingOn.layer)
				                     || _controller.MovingOneWayPlatformMask.MMContains(_controller.StandingOn.layer));
			}

			if ( !AbilityAuthorized  // if the ability is not permitted
			     || !BumpAuthorized // if jumps are not authorized right now
			     || (!_controller.CanGoBackToOriginalSize() && !onAOneWayPlatform)
			     || ((_condition.CurrentState != CharacterStates.CharacterConditions.Normal) // or if we're not in the normal stance
			         && (_condition.CurrentState != CharacterStates.CharacterConditions.ControlledMovement))
			     || (_movement.CurrentState == CharacterStates.MovementStates.Jetpacking) // or if we're jetpacking
			     || (_movement.CurrentState == CharacterStates.MovementStates.Dashing) // or if we're dashing
			     || (_movement.CurrentState == CharacterStates.MovementStates.Pushing) // or if we're pushing                
			     // || ((_movement.CurrentState == CharacterStates.MovementStates.WallClinging) && (_characterWallBump != null)) // or if we're wallclinging and can walljump
			     || (_controller.State.IsCollidingAbove && !onAOneWayPlatform)) // or if we're colliding with the ceiling
			{
				return false;
			}

			// if we're in a button activated zone and can interact with it
			if (_characterButtonActivation != null)
			{
				if (_characterButtonActivation.AbilityAuthorized
				    && _characterButtonActivation.PreventBumpWhenInZone
				    && _characterButtonActivation.InButtonActivatedZone
				    && !_characterButtonActivation.InButtonAutoActivatedZone)
				{
					return false;
				}
				if (_characterButtonActivation.InBumpPreventingZone)
				{
					return false;
				}
			}

			// if we're crouching and don't have enough space to stand we do nothing and exit
			if ((_movement.CurrentState == CharacterStates.MovementStates.Crouching) || (_movement.CurrentState == CharacterStates.MovementStates.Crawling))
			{				
				if (_characterCrouch != null)
				{
					if (_characterCrouch.InATunnel && (_verticalInput >= -_inputManager.Threshold.y))
					{
						return false;
					}
				}
			}

			// if we're not grounded, not on a ladder, and don't have any jumps left, we do nothing and exit
			if ((!_controller.State.IsGrounded)
			    && (_movement.CurrentState != CharacterStates.MovementStates.LadderClimbing)
			    && (NumberOfBumpsLeft <= 0))
			{
				return false;
			}

			if (_controller.State.IsGrounded
			    && (NumberOfBumpsLeft <= 0))
			{
				return false;
			}

			if (_inputManager != null)
			{
				if (_bumpingDownFromOneWayPlatform)
				{
					if ((_verticalInput > -_inputManager.Threshold.y) || (_bumpButtonReleased))
					{
						_bumpingDownFromOneWayPlatform = false;
					}
				}
				
				// if the character is standing on a one way platform and is also pressing the down button,
				if (_verticalInput < -_inputManager.Threshold.y && _controller.State.IsGrounded)
				{
					if (BumpDownFromOneWayPlatform())
					{
						return false;
					}
				}

				// if the character is standing on a moving platform and not pressing the down button,
				if (_controller.State.IsGrounded)
				{
					BumpFromMovingPlatform();
				}
			}	

			return true;
		}
        
        /// <summary>
        /// Handles bumping down from a one way platform.
        /// </summary>
        public virtual bool BumpDownFromOneWayPlatform()
        {
	        if (!CanBumpDownOneWayPlatforms || _bumpingDownFromOneWayPlatform)
	        {
		        return false;
	        }
        
	        // we go through all the colliders we're standing on, and if all of them are 1way, we're ok to bump down
	        bool canBumpDown = true;
	        foreach (GameObject obj in _controller.StandingOnArray)
	        {
		        if (obj == null)
		        {
			        continue;
		        }
		        if (!_controller.OneWayPlatformMask.MMContains(obj.layer) &&
		            !_controller.MovingOneWayPlatformMask.MMContains(obj.layer) &&
		            !_controller.StairsMask.MMContains(obj.layer))
		        {
			        canBumpDown = false;	
		        }
	        }
			     
	        if (canBumpDown)
	        {
		        _movement.ChangeState(CharacterStates.MovementStates.Jumping);
		        _characterHorizontalMovement.ResetHorizontalSpeed();
		        // we turn the boxcollider off for a few milliseconds, so the character doesn't get stuck mid platform
		        StartCoroutine(_controller.DisableCollisionsWithOneWayPlatforms(OneWayPlatformsBumpCollisionOffDuration));
		        _controller.DetachFromMovingPlatform();
		        _bumpingDownFromOneWayPlatform = true;
		        return true;
	        }
	        else
	        {
		        return false;
	        }
        }
        
        /// <summary>
        /// Handles bumping from a moving platform.
        /// </summary>
        protected virtual void BumpFromMovingPlatform()
        {
	        if (_controller.StandingOn != null)
	        {
		        if ( _controller.MovingPlatformMask.MMContains(_controller.StandingOn.layer)
		             || _controller.MovingOneWayPlatformMask.MMContains(_controller.StandingOn.layer) )
		        {
			        // we turn the boxcollider off for a few milliseconds, so the character doesn't get stuck mid air
			        StartCoroutine(_controller.DisableCollisionsWithMovingPlatforms(MovingPlatformsBumpCollisionOffDuration));
			        _controller.DetachFromMovingPlatform();
		        }	
	        }
        }
        
                
        /// Evaluates the bump restrictions
        public virtual bool BumpAuthorized 
        { 
            get 
            {
                if (_movement.CurrentState == CharacterStates.MovementStates.SwimmingIdle) 
                {
                    return false;
                }
        
                if ( (BumpRestrictions == BumpBehavior.CanBumpAnywhere))
                {
                    return true;
                }					
        
                if (BumpRestrictions == BumpBehavior.CanBumpOnGround)
                {
                    if (_controller.State.IsGrounded
                        || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
                    {
                        return true;
                    }
                    else
                    {
                        // if we've already made a bump and that's the reason we're in the air, then yes we can bump
                        if (NumberOfBumpsLeft < NumberOfBumps)
                        {
                            return true;
                        }
                    }
                }				
        
                if (BumpRestrictions == BumpBehavior.CanBumpOnGroundAndFromLadders)
                {
                    if ((_controller.State.IsGrounded)
                        || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
                    {
                        return true;
                    }
                    else
                    {
                        // if we've already made a bump and that's the reason we're in the air, then yes we can bump
                        if (NumberOfBumpsLeft < NumberOfBumps)
                        {
                            return true;
                        }
                    }
                }
        				
                return false; 
            }
        }
        
        
        /// <summary>
        /// Stores the current NumberOfBumps
        /// </summary>
        protected virtual void ResetInitialNumberOfBumps()
        {        
	        _initialNumberOfBumps = NumberOfBumps;
        }
        
        
        /// <summary>
        /// Resets the number of bumps.
        /// </summary>
        public virtual void ResetNumberOfBumps()
        {
	        NumberOfBumpsLeft = NumberOfBumps;
        }
        
        
        /// <summary>
        /// Updates the controller state based on our current bumping state
        /// </summary>
        protected virtual void UpdateController()
        {
	        _controller.State.IsShrinking = (_movement.CurrentState == CharacterStates.MovementStates.Shrinking);
        }
        
        /// <summary>
        /// Sets the number of bumps left.
        /// </summary>
        /// <param name="newNumberOfBumps">New number of bumps.</param>
        public virtual void SetNumberOfBumpsLeft(int newNumberOfBumps)
        {
	        NumberOfBumpsLeft = newNumberOfBumps;
        }
        
        /// <summary>
        /// Resets parameters in anticipation for the Character's respawn.
        /// </summary>
        public override void ResetAbility()
        {
	        base.ResetAbility ();
	        NumberOfBumps = _initialNumberOfBumps;
	        NumberOfBumpsLeft = _initialNumberOfBumps;
        }
    }
}