using System.Collections;
using UnityEngine;

namespace Main.AI {
    public class AISoldierA : AISoldier {

        [SerializeField]
        private GameObject grenadePrefab;

        /// <summary>
        /// 엄폐 여부
        /// </summary>
        public bool inCover = false;

        /// <summary>
        /// 엄폐 지점
        /// </summary>
        [ReadOnly]
        [SerializeField]
        private CoverPoint coverPoint = null;


        /// <summary>
        /// 소지중인 수류탄 개수
        /// </summary>
        private int grenadeAmount = 1;

        protected override void Start() {
            base.Start();
            StartCoroutine(StateHandle());
        }

        protected override void Update() {
            base.Update();

            if (CurrentState == STATE.DEAD) {
                return;
            }

            UpdateView();

            if (CurrentState == STATE.FLINCH) {
                pathFinder.isStopped = true;
            }

            // 엄폐 포인트 이동
            if (coverPoint != null) {
                if (CurrentState == STATE.IDLE || CurrentState == STATE.PATROL) {
                    aiAgent.SetDestination(coverPoint.Position);
                    CurrentState = STATE.TAKING_COVER;
                    pathFinder.isStopped = true;
                }

                if (CurrentState == STATE.TAKING_COVER && Vector3.Distance(transform.position, coverPoint.Position) < 1F) {
                    inCover = true;
                    CurrentState = STATE.HIDE;
                    hidingDuration = Random.Range(0.5F, 1F);
                    aiAgent.isStopped = true;
                }
            }

            if (isFoundEnemy && !inCover) {
                if (coverPoint == null) {
                    coverPoint = FindCoverPoint();
                }
            }
        }


        // 시선 처리
        private void UpdateView() {
            // 파악하고있는 상대가 있는 경우
            if (inCover && target != null) {
                FocusOnTarget();
            } else if (inCover && latestPlayerPosition != null) {
                var lookRot = Quaternion.LookRotation(((Vector3) latestPlayerPosition - transform.position).normalized);
                lookRot.x = lookRot.z = 0F;
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
            }
        }

        protected override void OnSetAnimationParameterByState(STATE state) {
            base.OnSetAnimationParameterByState(state);
            animator.SetBool("isRunning", state == STATE.TAKING_COVER);
        }

        protected override void OnDie(DAMAGE_CAUSE cause) {
            base.OnDie(cause);
            if (coverPoint != null) {
                coverPoint.soldier = null;
            }
        }

        private CoverPoint FindCoverPoint() {
            var coverPoints = ResourceManager.instance.coverPoints;
            CoverPoint coverPoint = null;
            float dist = float.MaxValue;

            foreach (var p in coverPoints) {
                if (p.soldier == null && latestPlayerPosition != null) {
                    var pos = p.transform.position;
                    /* 엄폐지점에섲 적이 보이는지 확인 : 오류땜에 일시 지움
                    if (Physics.Raycast(pos, ((Vector3) latestPlayerPosition - pos).normalized, out var hit, Mathf.Infinity, opaqueMask | enemyMask)) {
                        if (enemyMask == (enemyMask | (1 << hit.transform.gameObject.layer))) {
                            continue;
                        }
                    }*/

                    var d = Vector3.Distance(transform.position, pos);
                    if (d < dist) {
                        coverPoint = p;
                        dist = d;
                    }
                }
            }

            if (coverPoint != null) {
                coverPoint.soldier = this;
            }
            return coverPoint;
        }

        private float shootingDuration;
        private float hidingDuration;

        IEnumerator StateHandle() {
            while (true) {
                yield return new WaitForSeconds(0.1F);
                shootingDuration -= 0.1F;
                hidingDuration -= 0.1F;

                if (shootingDuration <= 0F && hidingDuration <= 0F) {
                    if (CurrentState == STATE.HIDE && target != null) {
                        CurrentState = STATE.SHOOTING;
                        shootingDuration = Random.Range(2F, 3F);
                    } else if (CurrentState == STATE.SHOOTING) {
                        if (target != null) {
                            if (Physics.Raycast(transform.position, (target.CurrentEyePosition - transform.position).normalized, out var hit, Mathf.Infinity, opaqueMask | enemyMask)) {
                                if (enemyMask == (enemyMask | (1 << hit.transform.gameObject.layer))) {
                                    continue;
                                }
                            }
                        }
                        CurrentState = STATE.HIDE;
                        hidingDuration = Random.Range(0.5F, 3F);
                    }
                }
            }
        }
    }
}
