using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GD.MinMaxSlider;
using UnityEngine;
using UnityEngine.AI;
using Weapon;

namespace Main.AI {
    [SelectionBase]
    [RequireComponent(typeof(NavMeshAgent), typeof(PathFinder))]
    public class AISoldier : Soldier {

        /// <summary>
        /// 적군을 발견했는 지의 여부 (true 일 경우, 적군이 있음을 인식한것입니다.)
        /// </summary>
        public static bool isFoundEnemy = false;

        /// <summary>
        /// AI가 파악중인 최근 적들의 위치
        /// </summary>
        //protected static Dictionary<int, Vector3> foundEnemyLatestPositions = new Dictionary<int, Vector3>();

        /// <summary>
        /// AI가 파악중인 최근 플레이어 위치
        /// </summary>
        public static Vector3? latestPlayerPosition = null;

        #region references
        public Gun gun;

        public Transform spine1;

        public Rigidbody pelvis;

        #endregion

        /// <summary>
        /// 적군과, 적군을 발견할 수 없도록 하는 콜라이더 맵의 불투명 마스크입니다.
        /// </summary>
        public LayerMask opaqueMask;

        public float rotationSpeed = 3F;

        [MinMaxSlider(0F, 3F)]
        public Vector2 bulletSpread = new Vector2(0F, 1F);

        /// <summary>
        /// 사격 기준 점
        /// </summary>
        public float aimSetDistance = 10F;

        /// <summary>
        /// 사격 기준 원 크기
        /// </summary>
        public float aimSetRadius = 2F;

        public float SightRangeXZ {
            get => sightRangeXZ;
            set {
                sightRangeXZ = value;
                halfSightRangeXZ = sightRangeXZ * 0.5F;
            }
        }

        public float SightRangeY {
            set {
                sightRangeY = value;
            }
        }

        public GameObject bloodParticlePrefab;

        public SoundPack soundPack;

        protected PathFinder pathFinder;

        protected Animator animator;

        private AudioSource audioSource;

        [SerializeField]
        [Range(0F, 360F)]
        private float sightRangeXZ = 90F;

        [SerializeField]
        [Range(0F, 60F)]
        private float sightRangeY = 60F;

        protected NavMeshAgent aiAgent;

        private float eyeHeight = 1.7F;

        [SerializeField]
        private STATE _currentState;

        public STATE CurrentState {
            get => _currentState;
            set {
                SetCurrentState(value);
            }
        }

        [Range(0F, 50F)]
        public float sightDistance = 30F;

        /// <summary>
        /// 현재 전투 목표로 하고있는 대상
        /// </summary>
        [ReadOnly]
        public Soldier target = null;

        private float halfSightRangeXZ;

        [SerializeField]
        private Soldier[] foundEnemies = new Soldier[0];

        private Vector3 lastTargetPosition;

        /// <summary>
        /// 목표 대상이 엄폐 상태로 경과한 시간
        /// </summary>
        [ReadOnly]
        [SerializeField]
        protected float timeInTargetCurve = 0F;

        public float orgSightRangeXZ;

        private float orgSightRangeY;

        private float shootingDelay = 0.8F;

        /// <summary>
        /// 총알 발포 패턴
        /// </summary>
        private bool[][] shotPatterns = new bool[][] {
            // new bool[] { true, false, true, true, false, false, false, true, false, false, true, true, true, true },
            // new bool[] { true, true, true, true, false, false, true, false, true, true, false, false, true, false },
            // new bool[] { false, true, true, false, false, true, true, false, true, false, false, true, true, true }
            new bool[] { true, false, false, false, false, true, false, false, false, false, true, false, false, false, false },
            new bool[] { true, false, false, false, true, false, false, false, false, false, false, true, false, false, false },
            new bool[] { true, false, false, true, false, false, false, false, false, false, false, false, true, false, false },
            new bool[] { true, false, false, false, false, false, false, false, false, false, false, false, true, false },
            new bool[] { true, false, false, false, false, false, false, false, false, false, false, true, false, false },
            new bool[] { true, false, false, false, false, false, false, false, false, false, true, false, false, false },
            new bool[] { true, false, false, false, false, false, false, false, false, true, false, false, false, false },
            new bool[] { true, false, false, false, false, false, false, false, true, false, false, false, false, false },
            new bool[] { true, false, false, false, false, false, false, true, false, false, false, false, false, false },
            new bool[] { true, false, false, false, false, false, true, false, false, false, false, false, false, false },
            new bool[] { true, false, false, false, false, true, false, false, false, false, false, false, false, false },
            new bool[] { true, false, false, false, true, false, false, false, false, false, false, false, false, false },
            new bool[] { true, false, false, true, false, false, false, false, false, false, false, false, false, false },
            new bool[] { true, false, true, false, false, false, false, false, false, false, false, false, false, false },
        };

        private int currentShotPatternKey = 0;

        private int shotPatternIndex = 0;

        /// <summary>
        /// 적군이 발견 된 것을 알아차린지의 여부 (총소리가 들리면 바로 true)
        /// </summary>
        public bool isKnowFoundEnemy = false;

        /// <summary>
        /// Rag Doll 처리를 위한 Rigidbodies
        /// </summary>
        private Rigidbody[] jointRigidbodies;

        protected virtual void Awake() {
            aiAgent = GetComponent<NavMeshAgent>();
            pathFinder = GetComponent<PathFinder>();
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();

            jointRigidbodies = GetComponentsInChildren<Rigidbody>();
            foreach (var r in jointRigidbodies) {
                r.isKinematic = true;
                r.GetComponent<Collider>().enabled = false;
            }

            CurrentState = STATE.PATROL;
            halfSightRangeXZ = sightRangeXZ / 2F;
            orgSightRangeXZ = sightRangeXZ;
            orgSightRangeY = sightRangeY;

            Gun.GunShotEvent += OnGunShot;

            currentEyeHeight = eyeHeight;
        }

        protected override void Start() {
            base.Start();
            StartCoroutine(FindEnemyRoutine());
            StartCoroutine(ShootingHandle());
            StartCoroutine(PlayVoiceSFX());
            //StartCoroutine(AnimationHandle());
        }

        protected virtual void Update() {
            // 엄폐중
            /*if (inCover) {
                var dir = (lastTargetPosition - transform.position);
                dir.y = 0F;
                transform.forward = Vector3.Lerp(transform.forward, dir.normalized, Time.deltaTime * 10F);
            }*/

            // 총알 부족시
            if (gun.CurrentAmmo == 0 && CurrentState != STATE.RELOADING) {
                CurrentState = STATE.RELOADING;
                StartCoroutine(Reload());
            }

            audioSource.pitch = Time.timeScale;
        }

        private void LateUpdate() {
            if (CurrentState == STATE.DEAD) {
                return;
            }

            if (target != null) {
                var dir = target.transform.position - spine1.transform.position;
                var y = Quaternion.LookRotation(transform.InverseTransformDirection(dir.normalized)).eulerAngles.y;

                if (y >= 180) {
                    y -= 360F;
                }

                y = Mathf.Clamp(y, -75F, 75F);
                spine1.localRotation = Quaternion.Euler(-y, 0F, -5F);
            }
        }

        private void OnDestroy() {
            Gun.GunShotEvent -= OnGunShot;
        }

        private IEnumerator PlayVoiceSFX() {
            yield return new WaitUntil(() => isFoundEnemy);
            while (true) {
                yield return new WaitForSeconds(3F);
                if (Random.Range(0, 3) == 0) {
                    audioSource.PlayOneShot(soundPack.voice.GetRandom(), soundPack.voice.volume);
                }
            }
        }

        private IEnumerator ShootingHandle() {
            while (CurrentState != STATE.DEAD) {
                // Calculate Shooting Delay
                yield return new WaitUntil(() => CurrentState == STATE.SHOOTING);
                if (shootingDelay > 0F) {
                    yield return new WaitForSeconds(shootingDelay);
                    shootingDelay = 0F;
                }

                // Wait For Gun Period
                yield return new WaitForSeconds(gun.period);

                // Calculate Shot Pattern
                if (shotPatternIndex == shotPatterns[currentShotPatternKey].Length - 1) {
                    shotPatternIndex = 0;
                    currentShotPatternKey = Random.Range(0, 3);
                }
                if (!shotPatterns[currentShotPatternKey][shotPatternIndex++]) {
                    gun.UnTrigger();
                    continue;
                }

                if (target != null && !target.isDead && CurrentState == STATE.SHOOTING && gun.CurrentAmmo > 0) {
                    if (Physics.Raycast(CurrentEyePosition, (target.CurrentEyePosition - CurrentEyePosition).normalized, out var hit, 1000F)) {
                        if (hit.collider.gameObject != target.gameObject) { continue; }
                    }

                    var aimPoint = target.CurrentEyePosition + ((target.transform.position - transform.position).normalized * 15F); // 15F: 기준거리
                    var rand = Random.insideUnitSphere * Random.Range(bulletSpread.x, bulletSpread.y);

                    aimPoint += rand;

                    gun.dotRay = new Ray(CurrentEyePosition, (aimPoint - CurrentEyePosition).normalized);
                    gun.Trigger();
                    animator.SetTrigger("shoot");
                }
            }
        }

        private float timeInFinding = 0F;
        private Coroutine lookAtTargetCoroutine = null;

        protected void FocusOnTarget() {
            timeInTargetCurve += Time.deltaTime;
            if (Physics.Raycast(CurrentEyePosition, (target.CurrentEyePosition - CurrentEyePosition).normalized, out var hit, 1000F, opaqueMask)) {
                if (hit.collider.gameObject == target.gameObject) {
                    lastTargetPosition = target.transform.position;
                    timeInTargetCurve = 0F;

                    if (target == Main.Player.Player.instance) {
                        latestPlayerPosition = target.transform.position;
                    }
                }
            }

            // 플레이어쪽으로 회전
            if (lookAtTargetCoroutine == null) {
                var dir = (new Vector3(target.transform.position.x, 0, target.transform.position.z) - new Vector3(transform.position.x, 0, transform.position.z)).normalized;
                float angle = Vector3.Angle(new Vector3(transform.forward.x, 0, transform.forward.z), dir);

                if (angle >= 75F) {
                    lookAtTargetCoroutine = StartCoroutine(LookAtTargetTask());
                }
            }

            // Target 이 3초 이상 보2지 않을 경고, 수류탄이 있을 경우
            /*if (grenadeAmount >= 1 && timeInTargetCurve > 3F) {
                var obj = Instantiate(grenadePrefab, EyePosition, Quaternion.identity).GetComponent<Grenade>();
                obj.GetComponent<Rigidbody>().AddForce(((lastTargetPosition + (Vector3.up * eyeHeight)) - EyePosition) / 2F * 100F);
                obj.RemovePin();
                grenadeAmount--;
            }*/
        }

        /// <summary>
        /// 시야에 들어오는 모든 보이는 적을 반환합니다.
        /// </summary>
        private Soldier[] FindEnemies() {
            var origin = CurrentEyePosition;
            var colliders = Physics.OverlapSphere(origin, sightDistance, enemyMask);
            var enemies = new Queue<Soldier>();

            for (int i = 0; i < colliders.Length; i++) {
                var c = colliders[i];

                // Horizontal
                var dir = (new Vector3(c.transform.position.x, 0, c.transform.position.z) - new Vector3(origin.x, 0, origin.z)).normalized;
                float angle = Vector3.Angle(new Vector3(transform.forward.x, 0, transform.forward.z), dir);

                // Vertical
                var diff = (c.transform.position - transform.position);
                var deg = Mathf.Abs(Mathf.Tan(diff.y / diff.magnitude) * Mathf.Rad2Deg);

                if (angle > halfSightRangeXZ || deg > sightRangeY / 2F) {
                    continue;
                }

                if (c.TryGetComponent<Soldier>(out var soldier)) {
                    // Enemy 가 확실히 보이는지 Check
                    if (!Physics.Raycast(CurrentEyePosition, (soldier.CurrentEyePosition - CurrentEyePosition).normalized, out var hit, 1000F) || hit.collider.gameObject != c.gameObject) {
                        continue;
                    }

                    if (soldier == Main.Player.Player.instance) {
                        latestPlayerPosition = soldier.transform.position;
                    }
                    //foundEnemyLatestPositions[c.gameObject.GetInstanceID()] = c.gameObject.transform.position;
                    enemies.Enqueue(soldier);
                }
            }

            return enemies.ToArray();
        }

        /// <summary>
        /// 총에 맞았을 경우
        /// </summary>
        public override bool GetShot(Soldier shooter, Vector3 hitPoint) {
            var result = base.GetShot(shooter, hitPoint);
            // 적 레이어일 경우
            if (IsEnemy(shooter)) {
                // 적군을 인지하지 못한 상태라면, 인근에 적군이 있음과, 그 위치를 알려준다.
                if (!isFoundEnemy) {
                    isFoundEnemy = true;
                    latestPlayerPosition = Main.Player.Player.instance.transform.position;
                }

                if (timeInTargetCurve > 5F && SightRangeXZ != 180F) {
                    orgSightRangeXZ = SightRangeXZ;
                    SightRangeXZ = 180F;
                }

                if (CurrentState != STATE.HIT && CurrentState != STATE.DEAD) {
                    hitTaskCoroutine = StartCoroutine(HitTask(hitPoint));
                }

                target = shooter;
            }
            return result;
        }

        /// <summary>
        /// 적군 여부 확인
        /// </summary>
        private bool IsEnemy(Soldier soldier) => (soldier.enemyMask == (soldier.enemyMask | (1 << gameObject.layer)));

        private void SetCurrentState(STATE state) {
            // 이미 죽은 상태에선 어떠한 상태로도 변하지 않음
            if (_currentState == STATE.DEAD) {
                return;
            }

            _currentState = state;
            OnSetAnimationParameterByState(state);

            if (state == STATE.SHOOTING) {
                shootingDelay = 0.8F;
            } else {
                gun.UnTrigger();
            }
        }

        protected virtual void OnSetAnimationParameterByState(STATE state) {
            animator.SetBool("isWalking", state == STATE.PATROL);
            animator.SetBool("isHiding", state == STATE.HIDE || state == STATE.RELOADING);
            animator.SetBool("isShooting", state == STATE.SHOOTING);
        }

        protected override void OnDie(DAMAGE_CAUSE cause) {
            base.OnDie(cause);
            CurrentState = STATE.DEAD;
            aiAgent.isStopped = true;
            pathFinder.isStopped = true;
            spine1.localRotation = Quaternion.Euler(0, 0, 0);

            aiAgent.enabled = false;
            pathFinder.enabled = false;
            animator.enabled = false;

            Destroy(GetComponent<Collider>());

            // Stop Coroutines
            /*if (hitTaskCoroutine != null) {
                StopCoroutine(hitTaskCoroutine);
            }*/
            StopAllCoroutines();

            // SFX
            if (cause == DAMAGE_CAUSE.GUN_SHOT) {
                audioSource.PlayOneShot(soundPack.bullet_large_flesh_head_cls_np.GetRandom(), soundPack.bullet_large_flesh_head_cls_np.volume);
            }

            // 총기 떨어뜨림
            gun.GetComponent<Collider>().enabled = true;
            gun.transform.parent = null;
            gun.gameObject.AddComponent<Rigidbody>().AddForce(transform.forward * Random.Range(1F, 3F), ForceMode.Impulse);
            gun.UnTrigger();

            // Rag Doll Rigidbodies 활성화
            foreach (var r in jointRigidbodies) {
                r.isKinematic = false;
                r.GetComponent<Collider>().enabled = true;
                r.gameObject.layer = LayerMask.NameToLayer("Ignore Soldier");
            }
            spine1.GetComponent<Rigidbody>().AddForce(spine1.forward * Random.Range(-100F, 100F), ForceMode.Impulse);

            StartCoroutine(Die());
        }

        IEnumerator Die() {
            yield return new WaitForSeconds(10F);
            foreach (var r in jointRigidbodies) {
                Destroy(r.GetComponent<CharacterJoint>());
                Destroy(r);
            }
        }

        protected override void OnGrenadeExploded(Grenade grenade) {
            base.OnGrenadeExploded(grenade);

            if (HP <= 0) {
                StartCoroutine(OnGrenadeExplodedTask(grenade));
            }

            // 50m 이상 떨어져있을 경우 무시합니다.
            if (Vector3.Distance(grenade.transform.position, transform.position) >= 50F) {
                return;
            }
            if (CurrentState == STATE.DEAD) {
                return;
            }
            if (grenade.owner is Main.Player.Player) {
                isFoundEnemy = true;
                latestPlayerPosition = Main.Player.Player.instance.transform.position;
            }
        }

        IEnumerator OnGrenadeExplodedTask(Grenade grenade) {
            if (spine1.TryGetComponent<Rigidbody>(out var rigidbody)) {
                yield return new WaitWhile(() => rigidbody.isKinematic);
                rigidbody.AddExplosionForce(30000F, grenade.transform.position, grenade.damageRange * 2F);
            }
        }

        private void OnGunShot(Gun gun, Vector3 origin, Vector3 hitPoint) {
            // 50m 이상 떨어져있을 경우 무시합니다.
            if (Vector3.Distance(origin, transform.position) >= 50F) {
                return;
            }

            // 죽었을 경우 무시
            if (CurrentState == STATE.DEAD) {
                return;
            }

            if (gun.owner is Main.Player.Player) {
                //print("isKnowFoundEnemy:" + isKnowFoundEnemy);
                if (!isKnowFoundEnemy && Vector3.Distance(hitPoint, transform.position) <= 20F) {
                    StartCoroutine(FlinchTask());
                }
                latestPlayerPosition = Main.Player.Player.instance.transform.position;
            }

            isFoundEnemy = true;
            isKnowFoundEnemy = true;
        }

        IEnumerator FlinchTask() {
            animator.SetTrigger("flinch");
            CurrentState = STATE.FLINCH;
            yield return new WaitForSeconds(2F);
            CurrentState = STATE.IDLE;
        }

        private Coroutine hitTaskCoroutine;
        IEnumerator HitTask(Vector3 hitPoint) {
            audioSource.PlayOneShot(soundPack.mrk_impact_flesh.GetRandom(), soundPack.mrk_impact_flesh.volume);
            animator.SetTrigger("hitHead");
            var tmp = aiAgent.isStopped;
            aiAgent.isStopped = true;

            Instantiate(bloodParticlePrefab, transform.position + hitPoint, Random.rotation);

            CurrentState = STATE.HIT;
            yield return new WaitForSeconds(1.33F);
            CurrentState = STATE.IDLE;

            aiAgent.isStopped = tmp;
            hitTaskCoroutine = null;
        }

        IEnumerator Reload() {
            yield return new WaitForSeconds(3F);
            gun.Reload();
            CurrentState = STATE.SHOOTING;
        }

        IEnumerator FindEnemyRoutine() {
            while (CurrentState != STATE.DEAD) {
                foundEnemies = FindEnemies();

                // 시야 범위 내에 적군이 있을 경우
                if (foundEnemies.Length != 0) {
                    isFoundEnemy = true;

                    // Enemy 를 Distance 순으로 Sort 후, 가장 가까운 Enemy 를 Target으로
                    var enemiesSortByDist = foundEnemies.OrderBy(x => Vector3.Distance(transform.position, x.transform.position)).ToArray();
                    var target = enemiesSortByDist[0];

                    // 타겟이 바뀐 경우
                    if (this.target != target) {
                        this.target = target;
                        if (SightRangeXZ != orgSightRangeXZ) {
                            SightRangeXZ = orgSightRangeXZ;
                        }
                    }
                }
                yield return new WaitForSeconds(0.5F);
            }
        }

        IEnumerator LookAtTargetTask() {
            while (true) {
                var dir = (new Vector3(target.transform.position.x, 0, target.transform.position.z) - new Vector3(transform.position.x, 0, transform.position.z)).normalized;
                float angle = Vector3.Angle(new Vector3(transform.forward.x, 0, transform.forward.z), dir);
                if (angle <= 5F) {
                    break;
                }
                var normalized = (target.transform.position - transform.position).normalized;
                var lookRot = Quaternion.LookRotation(normalized);
                lookRot.x = lookRot.z = 0F;
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
                yield return new WaitForEndOfFrame();
            }
            lookAtTargetCoroutine = null;
        }

        /*IEnumerator AnimationHandle() {
            while (true) {
                if (CurrentState == STATE.PATROL) {
                }
                animator.SetBool("isWalking", CurrentState == STATE.PATROL);
                yield return new WaitForSeconds(0.1F);
            }
        }*/

        private void OnDrawGizmos() {
            var origin = transform.position + (Vector3.up * currentEyeHeight);
            var forward = transform.forward;

            float rad = (transform.eulerAngles.y - halfSightRangeXZ) * Mathf.Deg2Rad;
            var left = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
            rad = (transform.eulerAngles.y + halfSightRangeXZ) * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));


            Gizmos.DrawWireSphere(origin, sightDistance);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin, forward * sightDistance);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, left * sightDistance);
            Gizmos.DrawRay(origin, right * sightDistance);

            Gizmos.color = Color.red;
            /*foreach (var e in foundEnemies) {
                Gizmos.DrawLine(origin, e.transform.position);
            }*/
            if (target != null) {
                if (Physics.Raycast(CurrentEyePosition, (target.CurrentEyePosition - CurrentEyePosition).normalized, out var hit, 1000F)) {
                    Gizmos.DrawRay(CurrentEyePosition, (target.CurrentEyePosition - CurrentEyePosition).normalized * hit.distance);
                } else {
                    Gizmos.DrawRay(CurrentEyePosition, (target.CurrentEyePosition - CurrentEyePosition).normalized * 1000F);
                }
            }

            /*if (SoldierWindow.drawSoliderAimSet) {
                Gizmos.color = Color.cyan;
                var pos = transform.position;
                UnityEditor.Handles.DrawWireDisc(transform.position + (transform.forward * aimSetDistance), transform.forward, aimSetRadius);
                Gizmos.DrawRay(pos, ((transform.forward * aimSetDistance) + (transform.up * aimSetRadius)).normalized * 100F);
                Gizmos.DrawRay(pos, ((transform.forward * aimSetDistance) + (-transform.up * aimSetRadius)).normalized * 100F);
                Gizmos.DrawRay(pos, ((transform.forward * aimSetDistance) + (transform.right * aimSetRadius)).normalized * 100F);
                Gizmos.DrawRay(pos, ((transform.forward * aimSetDistance) + (-transform.right * aimSetRadius)).normalized * 100F);
            }*/
        }


        public enum STATE {
            IDLE, // 가만히 있습니다.
            PATROL, // 주위를 멤돌며 순찰합니다.
            FLINCH, // 움찔합니다.
            HIT, // 공격을 받아 어쩔줄 몰라합니다.
            FOLLOWING_TO_ENEMY, // 적군을 따라갑니다
            TAKING_COVER, // 몸을 엄폐하러 이동합니다.
            HIDE, // 몸을 숨깁니다
            SHOOTING, // 사격을 합니다.
            RELOADING, // 장전을 합니다.
            DEAD // 죽었습니다.
        }

        [System.Serializable]
        public class SoundPack {
            public SoundLibrary mrk_impact_flesh; // 총알에 맞았을 때
            public SoundLibrary bullet_large_flesh_head_cls_np; // 총알에 죽었을 때
            public SoundLibrary voice; // 목소리
        }
    }
}
