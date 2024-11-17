using UnityEngine;
using UnityEngine.Events;
using Weapon;

namespace Main.Player {
    public class PlayerSoldier : Soldier {

        public Gun Gun {
            get => _gun;
            set {
                _gun.UnAimEvent -= OnUnAim;
                _gun.AimedEvent -= OnAimChanged;
                _gun.UnAimedEvent -= OnAimChanged;
                _gun.ShotEvent -= OnShot;
                _gun.ShotSoldierEvent -= OnShotSoldier;

                _gun = value;

                _gun.UnAimEvent += OnUnAim;
                _gun.AimedEvent += OnAimChanged;
                _gun.UnAimedEvent += OnAimChanged;
                _gun.ShotEvent += OnShot;
                _gun.ShotSoldierEvent += OnShotSoldier;
            }
        }

        [SerializeField]
        private Gun _gun;

        /// <summary>
        /// 첫 사격 시작시 플레이어 뷰 로테이션 값
        /// </summary>
        private Vector3? startViewForward = null;

        /// <summary>
        /// 지향사격시 적용되는 최근 뷰 로테이션 값
        /// </summary>
        private Vector3? lastForward = null;

        private Vector3? targetView = null;

        private Vector3 updatedRotation = Vector3.zero;

        /// <summary>
        /// 총으로 적을 쐈을경우 이벤트가 발생합니다.
        /// </summary>
        public event UnityAction<Soldier> ShotSoldierEvent;

        private void Awake() {
        }

        protected override void Start() {
            base.Start();
            Main.Player.Player.instance.behaviour.OnUpdateRotation += OnPlayerUpdateRotation;

            _gun.UnAimEvent += OnUnAim;
            _gun.AimedEvent += OnAimChanged;
            _gun.UnAimedEvent += OnAimChanged;
            _gun.ShotEvent += OnShot;
            _gun.ShotSoldierEvent += OnShotSoldier;
        }

        protected virtual void Update() {
            Gun.dotRay = Camera.main.ViewportPointToRay(new Vector3(0.5F, 0.5F));

            if (Gun.triggered) {
                if (Gun.IsAiming) {
                    if (startViewForward != null) {
                        Main.Player.Player.instance.behaviour.front = Vector3.Lerp(Main.Player.Player.instance.behaviour.front, (Vector3) startViewForward, Time.deltaTime * 10F); // 오류 X
                    }
                } else if (Gun.shot && lastForward != null) { // 계속 쏘고 있을 경우
                    Main.Player.Player.instance.behaviour.front = Vector3.Lerp(Main.Player.Player.instance.behaviour.front, (Vector3) lastForward, Time.deltaTime * 5F);
                }
            }

            if (!Gun.shot && startViewForward != null) {
                if (targetView == null) {
                    if (Gun.IsAiming) {
                        targetView = (Vector3) startViewForward;
                    } else {
                        var update = updatedRotation / 2F;
                        update.y = Mathf.Clamp(update.y, -30, 30);
                        targetView = (Vector3) startViewForward + (updatedRotation - update);
                    }
                }
                Main.Player.Player.instance.behaviour.front = Vector3.Lerp(Main.Player.Player.instance.behaviour.front, (Vector3) targetView, Time.deltaTime * 10F);

                if (Vector3.Distance(Main.Player.Player.instance.behaviour.front, (Vector3) targetView) < 0.1F) {
                    Refresh();
                }
            }

            if (Main.Player.Player.instance.behaviour.isMoving) {
                Gun.multiplyBulletSpread = Gun.multiplyBulletSpreadRange.y;
            }
        }

        private void OnShotSoldier(Soldier soldier) {
            ShotSoldierEvent?.Invoke(soldier);
        }

        public void OnPlayerUpdateRotation(Vector3 rot) {
            if (Gun.pullTrigger) {
                updatedRotation += rot;
                if (Gun.IsAiming) {
                    startViewForward += rot;
                }
            }

            // 원점으로 이동중 회전을 시도한 경우
            if (!Gun.shot && startViewForward != null) {
                Refresh();
            }
        }

        private void Refresh() {
            startViewForward = null;
            targetView = null;
            updatedRotation = Vector3.zero;
        }

        private void OnUnAim() {
            lastForward = null;
        }

        private void OnAimChanged() {
            startViewForward = Main.Player.Player.instance.behaviour.front;
            updatedRotation = Vector3.zero;
        }

        private void OnShot(bool isFirst) {
            if (isFirst) {
                startViewForward = Main.Player.Player.instance.behaviour.front;
            }

            if (Gun.IsAiming) {
                Main.Player.Player.instance.behaviour.front += (Vector3.right * Random.Range(Gun.bulletSpreadAimingVertical.x, Gun.bulletSpreadAimingVertical.y)) + (Vector3.up * Random.Range(Gun.bulletSpreadAimingHorizontal.x, Gun.bulletSpreadAimingHorizontal.y));
            } else {
                lastForward = Main.Player.Player.instance.behaviour.front;
                Main.Player.Player.instance.behaviour.front += (Vector3.right * Random.Range(Gun.bulletSpreadVertical.x, Gun.bulletSpreadVertical.y)) + (Vector3.up * Random.Range(Gun.bulletSpreadHorizontal.x, Gun.bulletSpreadHorizontal.y));

                if (Gun.multiplyBulletSpread < Gun.multiplyBulletSpreadRange.y) {
                    Gun.multiplyBulletSpread += 0.1F;
                }
            }
        }
    }
}
