using System.Collections;
using Main;
using UnityEngine;
using UnityEngine.Events;

namespace Main.Player {

    [RequireComponent(typeof(Rigidbody), typeof(PlayerCollider))]
    public class PlayerBehaviour : MonoBehaviour {

        public Transform center;

        public Camera view;

        [HideInInspector]
        public Player player;

        public bool isControllable = true;

        public float eyeHeight = 1.67F;

        public float eyeHeightOnCrouching = 0.8F;

        public float walkingSpeed = 3F;

        public float runningSpeed = 5F;

        public float crouchingSpeed = 10F;

        public float crouchWalkingSpeed = 2F;

        public float jumpPower = 5F;

        public float mouseSensitivityX = 16F;

        public float mouseSensitivityY = 21F;

        public float jumpCooltime = 0.05F;

        public float gravityScale = 0.03F;

        [HideInInspector]
        public bool run = false;

        [HideInInspector]
        public bool crouch = false;

        [HideInInspector]
        public bool jump = false;

        public bool isRunning { get; private set; } = false;

        [SerializeField, ReadOnly]
        private bool isJumping = false;

        public bool isCrouching { get; private set; } = false;

        [HideInInspector]
        public bool isMoving = false;

        public LayerMask colliderMask = -1;

        [HideInInspector]
        public Vector3 position;

        [HideInInspector]
        public Vector3 groundNormal;

        internal Vector3 velocity;

        private Rigidbody _rigidbody;

        private PlayerCollider _collider;

        private CapsuleCollider capsuleCollider;

        private Quaternion rotation;

        [HideInInspector]
        public Vector3 front;

        [SerializeField, ReadOnly]
        private bool onGround = true;

        private Vector2 mouseVelocity;

        private float CurrentEyeHeight {
            get => player.currentEyeHeight;
            set {
                player.currentEyeHeight = value;

                var headHeight = player.currentEyeHeight + 0.1F;

                _collider.center.y = headHeight / 2F;
                _collider.height = headHeight;

                capsuleCollider.center = _collider.center;
                capsuleCollider.height = _collider.height;
            }
        }

        #region Events

        /// <summary>
        /// 업데이트 된 회전값을 포함합니다.
        /// float: 속도
        /// </summary>
        public event UnityAction<float> RunningEvent;

        public event UnityAction<float> WalkingEvent;

        public event UnityAction IdleEvent;

        public event UnityAction<UpdateMovementEvent> OnUpdateMovement;

        public event UnityAction MovedEvent;

        ///<summary>
        ///업데이트 된 회전값을 포함합니다.
        ///</summary>
        public event UnityAction<Vector3> OnUpdateRotation;

        public event UnityAction JumpedEvent;

        public event UnityAction LandedEvent;

        public event UnityAction CrouchedEvent;

        public event UnityAction UncrouchedEvent;

        #endregion

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<PlayerCollider>();
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();

            position = _rigidbody.position;
            capsuleCollider.radius = _collider.radius;
            front = transform.rotation.eulerAngles;
        }

        private void Start() {
            CurrentEyeHeight = eyeHeight;
            StartCoroutine(JumpRoutine());
        }

        void Update() {
            float x = -Input.GetAxisRaw("Mouse Y") / 10F * mouseSensitivityY;
            x = Mathf.Clamp(x, -60, 50);
            float y = Input.GetAxisRaw("Mouse X") / 10 * mouseSensitivityX;
            var vector = new Vector3(x, y) * Time.timeScale;

            OnUpdateRotation?.Invoke(vector);

            front += vector;
            front.x = Mathf.Clamp(front.x, -60, 50);
        }

        private Vector3 safePoint;
        private bool isSafe = false;

        private Vector3 velocityInFrame = Vector3.zero;

        private void FixedUpdate() {
            isSafe = true;

            UpdateView();
            velocity = UpdateMovement(velocity);

            Vector3 pos = position;
            Vector3 _pos = pos;

            var localVelocity = transform.InverseTransformDirection(velocity) * Time.fixedDeltaTime;

            //Horizontal
            var hv = transform.TransformDirection(new Vector3(localVelocity.x, 0, localVelocity.z));
            pos = CalculateSafeHorizontal(pos, hv);

            //Vertical
            var vv = transform.TransformDirection(new Vector3(0, localVelocity.y, 0));
            pos = CalculateSafeVertical(pos, vv);

            pos = CalculateOverlap(pos);
            ApplyTransform(pos);

            if ((pos - _pos).sqrMagnitude > 0.001F || !onGround) {
                isMoving = true;
                MovedEvent?.Invoke();
            } else {
                isMoving = false;
            }

            velocityInFrame = hv + vv;
        }

        public float speed { get; private set; } = 0F;

        private Vector3 UpdateMovement(Vector3 velocity) {
            if (onGround) {
                var x = isControllable ? Input.GetAxis("Horizontal") : 0F;
                var z = isControllable ? Input.GetAxis("Vertical") : 0F;

                var to = (transform.forward * z) + (transform.right * x);
                isRunning = false;
                var isCrouching = false;
                speed = 0F;

                if (crouch && onGround) {
                    isCrouching = true;
                    if (to.sqrMagnitude > 0) {
                        speed = this.crouchWalkingSpeed;
                    }
                } else if (run && z > 0) {
                    isRunning = true;
                    speed = this.runningSpeed;
                } else if (to.sqrMagnitude > 0) {
                    speed = walkingSpeed;
                    if (player.CurrentState == Player.STATE.RELOADING) {
                        speed /= 2F;
                    }
                }

                if (isCrouching != this.isCrouching) { // isCrouching에 변화가 있을 경우
                    this.isCrouching = isCrouching;
                    if (isCrouching) {
                        CrouchedEvent?.Invoke();
                    } else {
                        UncrouchedEvent?.Invoke();
                    }
                }


                if (isRunning) {
                    RunningEvent?.Invoke(speed);
                } else if (to.sqrMagnitude > 0) {
                    WalkingEvent?.Invoke(speed);
                } else {
                    IdleEvent?.Invoke();
                }

                velocity = (to.normalized * speed) + new Vector3(0, velocity.y);

                if (isControllable && jump && !isJumping) {
                    velocity.y = jumpPower;
                    isJumping = true;
                    JumpedEvent?.Invoke();
                } else { velocity.y = 0F; }
            }

            velocity += Physics.gravity * gravityScale;
            var ev = new UpdateMovementEvent(velocity);
            OnUpdateMovement?.Invoke(ev);

            return ev.velocity;
        }

        private void ApplyVelocity(Vector3 velocity) {
            position += velocity;

            if (!isSafe) {
                position.y = safePoint.y;
            }
            _rigidbody.MovePosition(position);
            _rigidbody.MoveRotation(rotation);
        }

        private void ApplyTransform(Vector3 position) {
            _rigidbody.MovePosition(this.position = position);
            _rigidbody.MoveRotation(rotation);
        }

        private void UpdateView() {
            if (!isControllable) {
                return;
            }

            if (isCrouching) {
                CurrentEyeHeight = Mathf.Lerp(CurrentEyeHeight, eyeHeightOnCrouching, Time.deltaTime * crouchingSpeed);
            } else {
                CurrentEyeHeight = Mathf.Lerp(CurrentEyeHeight, eyeHeight, Time.deltaTime * crouchingSpeed);
            }

            var pos = center.localPosition;
            pos.y = CurrentEyeHeight;

            center.localPosition = pos;
            rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, front.y, 0), Time.fixedDeltaTime * 20F);

            var rot = center.localRotation;
            center.localRotation = Quaternion.Lerp(rot, Quaternion.Euler(front.x + 10, rot.y, rot.z), Time.fixedDeltaTime * 20F);
        }

        private Vector3 CalculateSafeHorizontal(Vector3 pos, Vector3 velocity) {
            Vector3 dir = velocity.normalized;
            float dist = velocity.magnitude;

            // 여러번 체크해주어 여러번의 면이 닿는 구석 같은 부분에서 뚫림 현상 커버
            for (var i = 0; i < 5; i++) {

                var origin = pos - (dir * 0.1F);

                if (Physics.CapsuleCast(origin + _collider.localPoint1, origin + _collider.localPoint2, _collider.radius, dir, out var hit, dist + 0.1F, colliderMask)) {

                    var safeDist = (hit.distance - 0.1F);
                    pos += dir * safeDist;

                    // 벽인 경우
                    if (Vector3.Angle(transform.up, hit.normal) < 145) {
                        dist -= safeDist;
                        dir = Vector3.ProjectOnPlane(dir, hit.normal);
                    } else {
                        break;
                    }
                } else {
                    pos += dir * dist;
                    break;
                }
            }
            return pos;
        }

        private Vector3 CalculateSafeVertical(Vector3 pos, Vector3 velocity) {
            var dir = velocity.normalized;
            var dist = velocity.magnitude;

            var onGround = Physics.CapsuleCast(pos + _collider.localPoint1, pos + _collider.localPoint2, _collider.radius - 0.1F, dir, out var hit, dist + 0.1F, colliderMask);
            if (onGround) {
                groundNormal = hit.normal;
                if (Vector3.Angle(transform.up, groundNormal) < 45F) {
                    pos += dir * (hit.distance - 0.1F);
                } else { // 미끄러짐
                    pos += (Vector3.ProjectOnPlane(dir, groundNormal) * dist);
                }
                
                UI.UI.isPlayerOnGround = true;

                if (this.onGround != onGround) { // && isJumping
                    LandedEvent?.Invoke();
                }
            } else {
                pos.y += velocity.y;
                //pos += vv;\
                UI.UI.isPlayerOnGround = false;
            }

            this.onGround = onGround;

            // 바닥 인식
            if (this.velocity.y < 0) {
                var hits = Physics.CapsuleCastAll(_collider.point1 + (Vector3.up * 0.1F), _collider.point2 + (Vector3.down * 0.1F), _collider.radius - 0.01F, Vector3.down, 0.2F + Mathf.Max(-this.velocity.y * Time.fixedDeltaTime, 0F), gameObject.layer);
                UI.UI.debugText = "\nHit Count : " + hits.Length.ToString();

                foreach (var h in hits) {
                    if (Vector3.Angle(transform.up, h.normal) <= 45F) {
                        if (h.point.y - transform.position.y < 1F) {
                            isSafe = false;
                            safePoint = h.point;
                        }
                    }
                }
            }

            if (!isSafe) {
                pos.y = safePoint.y;
            }

            return pos;
        }

        private Vector3 CalculateOverlap(Vector3 position) {
            var colliders = Physics.OverlapCapsule(position + _collider.localPoint1, position + _collider.localPoint2, _collider.radius - 0.1F, colliderMask);
            if (colliders.Length > 0) {
                foreach (var c in colliders) {
                    if (c.gameObject != gameObject && Physics.ComputePenetration(capsuleCollider, position, rotation, c, c.transform.position, c.transform.rotation, out var dir, out float dist)) {
                        position += (dir * (dist));
                    }
                }
            }
            return position;
        }

        IEnumerator JumpRoutine() {
            while (true) {
                yield return new WaitUntil(() => isJumping);
                yield return new WaitForSeconds(jumpCooltime);
                isJumping = false;
            }
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.blue;

            var distance = 1F + Mathf.Max(-velocity.y * Time.fixedDeltaTime, 0F);
            if (Physics.BoxCast(position + Vector3.up, new Vector3(0.2F, 0.01F, 0.2F), Vector3.down, out var hit, transform.rotation, distance, gameObject.layer)) {
                Gizmos.DrawRay(position + Vector3.up, Vector3.down * hit.distance);
            } else {
                Gizmos.DrawRay(position + Vector3.up, Vector3.down * distance);
            }

            if (Application.isPlaying) {
                var origin = transform.position + _collider.center;

                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(origin, velocityInFrame);
                PlayerCollider.DrawWireCapsule(origin + velocityInFrame, Quaternion.identity, _collider.radius, _collider.height);
            }
        }

        public class UpdateMovementEvent {

            public Vector3 velocity;

            public UpdateMovementEvent(Vector3 velocity) {
                this.velocity = velocity;
            }
        }
    }
}
