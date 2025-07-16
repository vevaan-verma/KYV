using System.Collections;
using TMPro;
using UnityEngine;

public class WeaponManager : MonoBehaviour {

    [Header("References")]
    [SerializeField] private GameObject weaponSprite;
    [SerializeField][Tooltip("Track mouse position relative to this object. Default to Player, or alternatively, use the main camera")] private GameObject origin;
    [SerializeField] private SpriteRenderer rangeAreaIndicator;
    [SerializeField] private TextMeshProUGUI speedDisplay;
    private CameraController cameraController;
    private TrailRenderer trail;
    private PopupPlayer popups;
    private PlayerController playerController;
    private float currentCharge; // max charge not needed, this is stored as a % 

    // these stats do change throughout a game
    [Header("Stats (Scaling)")]
    [SerializeField] private float baseDamage;
    [SerializeField] private float roundDamageIncrement;
    [SerializeField][Tooltip("Technically kb initial velocity")] private float baseKbPower; // aka knockback for actual stat names
    [SerializeField] private float roundKbPowerIncrement;
    [SerializeField][Tooltip("Time in seconds for sword to fully charge")] private float baseSwingChargeTime; // aka handling for actual stat names
    [SerializeField] private float roundHandlingIncrement;
    [SerializeField][Range(0, 100)] private int baseCritChance;
    [SerializeField] private int roundCritChanceIncrement;

    // these stats do not change 
    [Header("Stats (Constant)")]
    [SerializeField][Tooltip("Used for kb direction calculations only")] private float idealSwingSpeed;
    [SerializeField][Range(1f, 3f)] private float critDamageMult; // doesn't change through upgrades
    [SerializeField][Range(0.01f, 1f)][Tooltip("Multiplied kbPower to get kb distance")] private float kbPowerDistRatio; // doesn't change through upgrades
    [SerializeField][Range(1f, 3f)] private float critKbMult; // doesn't change through upgrades
    [SerializeField][Range(0f, 10f)][Tooltip("Set to 0 for kb to apply directly away")] private float kbSwingDirModifier;
    [SerializeField] private bool dashAttackEnabled;

    [Header("Mouse Tracking")]
    [SerializeField][Range(0f, 1f)][Tooltip("How many seconds to update peak speed")] private float peakSpeedInterval;
    [SerializeField][Range(0f, 2f)][Tooltip("Mouse must be outside this radius for weapon rotation to update")] private float minTrackingRadius;
    [SerializeField][Range(2f, 15f)][Tooltip("Mouse must be inside this radius for weapon rotation to update. Must be greater than min, duh")] private float maxTrackingRadius;
    [SerializeField][Range(0f, 2f)][Tooltip("Increases tracking max radius based on camera distance to player to offset the loss in area caused by camera lerp.\nChange this to modify the impact of the effect:\n0 to disable, 1 for \"ideal adjustment.\"")] private float dynamicMaxRadiusMult;
    [SerializeField][Range(0.1f, 5f)][Tooltip("A higher value means the mouse must be closer to the range indicator for it to start showing up")] private float rangeIndicatorModifier;
    [SerializeField][Range(0f, 1f)][Tooltip("Percent (of 255) max alpha value of the range indicator")] private float rangeIndicatorMaxAlpha;
    [SerializeField][Tooltip("For when mouse is in the zone")] private Color rangeIndicatorValidColor;
    [SerializeField][Tooltip("For when mouse is out of the zone")] private Color rangeIndicatorInvalidColor;

    [Header("Swing Charge")]
    [SerializeField][Tooltip("Instead of an uncharged range of 0 to 99 percent, the range becomes 0 to [this value] percent. Used to create a larger \"jump\" in damage from when the bar is not red to red")][Range(0f, 1f)] private float maxUnchargedCharge;
    [SerializeField][Tooltip("Lower bound of the range mentioned in the tooltip for the variable above")][Range(0f, 1f)] private float minUnchargedCharge;
    [SerializeField][Tooltip("Prevents one long permanent swing")] private bool swingDecaysCharge;
    [SerializeField][Tooltip("Number of ticks before the current swing's charge gets cut for the first time in a swing")][Min(0)] private int initialChargeDecayInterval;
    [SerializeField][Tooltip("Number of ticks before the current swing's charge gets cut")][Min(0)] private int chargeDecayInterval;
    [SerializeField][Tooltip("During a swing, every swingChargeDecayInterval seconds, the charge is multiplied by this")][Range(0f, 1f)] private float chargeDecay;
    [SerializeField][Tooltip("Will not show trail when swinging but charge has fallen below this amount")][Range(0f, 1f)] private float chargeCutoff;

    [Header("Swing Tracking")]
    [SerializeField][Tooltip("Speed must be at least this much to register as a swing")] private float minSwingSpeed;
    [SerializeField][Min(0)][Tooltip("ticks you can miss and still have the weapon count you as being in a swing")] private int swingTickCushion;
    [SerializeField] private Gradient unchargedSwingColor;
    [SerializeField] private Gradient chargedSwingColor;

    [Header("Sprite")]
    [SerializeField][Tooltip("Make weapon point cw or ccw according to the mouse")] private bool shouldFlipWeapon;
    [SerializeField][Tooltip("Flip weapon whenever it is moving faster than minWeaponFlipSpeed. Set to false to only flip when at minSwingSpeed")] private bool alwaysFlipWeapon;
    [SerializeField][Range(0f, 100f)] private float minWeaponFlipSpeed;
    [SerializeField] private bool spriteFacingCounterclockwise; // if it rotating "forward" would cause into the screen motion

    [Header("Popups")]
    [SerializeField] private Popup critPopup;
    [SerializeField] private Popup hitPopup;
    [SerializeField] private Popup spinKbPopup;

    [Header("Ability and Special Item stuff")]
    [SerializeField][Tooltip("For the upgrade that causes attacks while dashing to be stronger")][Min(1f)] private float dashDamageMult;
    [SerializeField][Range(0, 100)] private int onKillHealChance;
    [SerializeField][Tooltip("As a percentage of max health")][Range(0f, 1f)] private float onKillHealAmount;
    [SerializeField] private Color projectileBlockingKnifeColor;
    [SerializeField][Tooltip("This much % damage will be added for each enemy hit in a swing")][Min(0f)] private float strongSweepBonus;
    [SerializeField][Tooltip("Sweeping damage bonus will stop increasing after this many enemies have been sweeped. So max sweeping damage is this times the bonus")][Min(0f)] private float strongSweepMaxEnemies;
    [SerializeField][Range(0f, 1f)] private float critSlowAmount;
    [SerializeField][Min(0f)] private float critSlowDuration;
    [SerializeField][Min(0f)] private float spinKbApplicationRadius;
    [SerializeField][Tooltip("Rotation in degrees required to activate spin kb ability")][Min(0f)] private float spinKbRotAmount;
    [SerializeField][Min(0f)] private float spinKbPowerMult;
    [SerializeField][Min(0f)] private float spinKbPowerDistRatio;
    [SerializeField][Range(0f, 0.999f)] private float spinKbSlow;
    [SerializeField][Min(0f)] private float spinKbSlowDuration;
    private int enemiesSweeped;
    private bool enraged;
    private bool swingDestroysProjectiles;

    [Header("Debug")]
    [SerializeField][Tooltip("Display speed in UI")] private bool displaySpeed;
    private float weaponScale;

    [Header("Internal")]
    [SerializeField, ReadOnly] private const float TPS = 25;
    [SerializeField, ReadOnly] private bool isTrackingSpeed;
    [SerializeField, ReadOnly][Tooltip("Weapon speed as of last tick")] private float speed;
    [SerializeField, ReadOnly] private float peakSpeed;
    [SerializeField, ReadOnly] private float swingSpeed;
    [SerializeField, ReadOnly][Tooltip("Number of ticks counted for tracking peakSpeedInterval")] private int tickCounter;
    private float peakSpeedTracker;

    private IEnumerator speedTracker; // ?
    private Coroutine swing;

    /// ***************************** NOTES *****************************
    /// 
    /// there are a few different speed variables: 
    ///  
    ///     speed:              used merely to keep track of swing speed every tick (instantaneous)
    ///    
    ///     peakSpeed:          used primarily for display purposes but can often be used interchangably with speed.
    ///                         using peakSpeed instead of speed for something makes that thing only update every peakSpeedInterval seconds.  
    ///                     
    ///     swingSpeed:         this is used when applying damage, knockback, or other on-enemy-hit effects
    ///                         because it is more consistent and reliable than speed, which is super variable. 
    ///                         it feels a lot better to use this for on-hit-effects than speed because of the aforementioned reasons.
    ///                         NOTE: combat has been reworked and swingSpeed doesnt effect combat anymore, but is still used to measure speed during a swing
    ///                     
    ///     peakSpeedTracker:   has no use as a measure of speed, is just a helper variable for peakSpeed
    ///                     
    /// Think of TPS as the tick rate for a third update method which is exclusive to this class and used only for weapon tracking.
    /// TPS is a constant because changing it strongly changes the speeds that are read/calculated:
    /// 
    ///     too low:            speed will lag behind and not increase/decrease fast enough
    ///     
    ///     too high:           snappy movements and hard flicking movements record absurdly high speeds (i've seen 80k+)
    ///                         and speeds recorded are overall higher due to the smaller interval picking up more of the
    ///                         highly variable and naturally imperfect motions of a computer mouse
    ///                         
    ///     don't change TPS! all related stat values (ex: handling) will need to be rebalanced and restandardized!
    /// 
    /// *****************************************************************

    private void Start() {

        if (minUnchargedCharge >= maxUnchargedCharge)
            Debug.LogError("WeaponManager minUnchargedCharge must be less than maxUnchargedCharge.");

        popups = FindAnyObjectByType<PopupPlayer>();
        cameraController = Camera.main.GetComponent<CameraController>();
        trail = weaponSprite.GetComponent<TrailRenderer>();
        playerController = FindAnyObjectByType<PlayerController>();
        trail.emitting = false;

        if (!spriteFacingCounterclockwise)
            weaponSprite.transform.localScale = new Vector3(-weaponSprite.transform.localScale.x, weaponSprite.transform.localScale.y, 1);

        weaponScale = weaponSprite.transform.localScale.x;

        if (!origin)
            origin = FindAnyObjectByType<CameraController>().gameObject;

        PointAtMouse(true);

        speedTracker = TrackWeaponAngularSpeed();
        StartCoroutine(speedTracker);
        isTrackingSpeed = true;

        // apply linear scaling to the weapon stats based on the round number
        float roundBaseDamage = baseDamage + (roundDamageIncrement * (GameData.GetRoundNumber() - 1)); // set base damage based on round number
        float roundBaseKbPower = baseKbPower + (roundKbPowerIncrement * (GameData.GetRoundNumber() - 1)); // set base kb power based on round number
        float roundBaseHandling = baseSwingChargeTime + (roundHandlingIncrement * (GameData.GetRoundNumber() - 1)); // set base handling based on round number
        int roundBaseCritChance = baseCritChance + (roundCritChanceIncrement * (GameData.GetRoundNumber() - 1)); // set base crit chance based on round number

        if (!GameData.IsInitialWeaponStatsSet()) // if the weapon stats have not been set yet, set them
            GameData.SetInitialWeaponStats(roundBaseDamage, roundBaseKbPower, roundBaseHandling, roundBaseCritChance); // set initial weapon stats

        GameData.SetRoundBaseWeaponStats(roundBaseDamage, roundBaseKbPower, roundBaseHandling, roundBaseCritChance); // set round base weapon stats
        GameData.SetRoundWeaponIncrements(roundDamageIncrement, roundKbPowerIncrement, roundHandlingIncrement, roundCritChanceIncrement); // set round weapon multipliers

        UpdateTrackingIndicator();

    }

    #region Updating & Tracking
    private void Update() {

        if (GameData.IsGamePaused()) return; // don't update if game is paused to prevent swords from moving around

        PointAtMouse();

        if (alwaysFlipWeapon && shouldFlipWeapon && Mathf.Abs(speed) > minWeaponFlipSpeed)
            FlipWeapon();

        UpdateSpeedDisplay();

        UpdateCharge();

        UpdateTrackingIndicator();

    }

    private void UpdateCharge() {

        float chargeTime = GameData.GetStat(StatType.Handling); // handling aka chargeTIme

        if (enraged) {

            currentCharge = 1f;

        } else if (currentCharge < 1f && swing == null) {

            float dt = Time.deltaTime;
            currentCharge += dt / chargeTime;

        }
    }

    private IEnumerator TrackWeaponAngularSpeed() {

        float rotPrev = transform.rotation.eulerAngles.z;

        while (true) {

            yield return new WaitForSeconds(1 / TPS);

            float rot = transform.rotation.eulerAngles.z;
            float dAngle = Mathf.DeltaAngle(rotPrev, rot);
            rotPrev = rot;

            speed = dAngle * TPS;

            // update peak speed

            if (Mathf.Abs(speed) >= Mathf.Abs(peakSpeed))
                peakSpeedTracker = Mathf.Abs(speed);

            if (tickCounter >= peakSpeedInterval * TPS) {

                peakSpeed = peakSpeedTracker;
                peakSpeedTracker = 0f;
                tickCounter = 0;

            }

            if (swing == null && Mathf.Abs(speed) > minSwingSpeed || (playerController.IsDashing() && dashAttackEnabled)) // begin a new swing
                swing = StartCoroutine(TrackSwing());

            tickCounter++;

        }
    }

    private IEnumerator TrackSwing() {

        trail.emitting = true;

        if (currentCharge < 1f)
            trail.colorGradient = unchargedSwingColor;
        else
            trail.colorGradient = chargedSwingColor;

        SpriteRenderer weaponRenderer = null;
        if (swingDestroysProjectiles) {

            weaponRenderer = weaponSprite.GetComponent<SpriteRenderer>();
            weaponRenderer.color = projectileBlockingKnifeColor;

        }


        // when player dashes, their knife can still stab (or slice)
        // if they just dash and dont swing like normal, then no weapon charge is consumed at the end of the dash
        bool isDashAttack = playerController.IsDashing();

        //float swingSpeed = Mathf.Abs(speed); //peak speed during current swing
        float dir = speed; // speed at the start gives us direction

        int failedTicks = 0;
        int decayTickCounter = 0;
        bool hasDoneInitialDecay = false;

        // ability: spin sword in a circle
        float rotPrev = transform.rotation.eulerAngles.z;
        float totalRot = 0f;
        bool hasCircled = false;
        //int circleTickCounter = 0;

        swingSpeed = speed;

        if (shouldFlipWeapon && Mathf.Abs(speed) > minWeaponFlipSpeed)
            FlipWeapon();

        // while we are registered as swinging, with some breathing room
        // or, while dashing, for dash attacks
        while (failedTicks < swingTickCushion || playerController.IsDashing()) {

            // this isn't really being used for combat anymore, but im keeping it in just in case
            swingSpeed = Mathf.Max(Mathf.Abs(speed), swingSpeed);

            // so... now we have two tickers... i just hope they are synced...
            yield return new WaitForSeconds(1 / TPS);
            decayTickCounter++;

            // charge decay for long swings
            if (swingDecaysCharge && !enraged && !isDashAttack) {

                if (!hasDoneInitialDecay && decayTickCounter == initialChargeDecayInterval) {

                    currentCharge *= chargeDecay;
                    decayTickCounter = 0;
                    trail.colorGradient = unchargedSwingColor;

                    hasDoneInitialDecay = true;

                } else if (hasDoneInitialDecay && decayTickCounter == chargeDecayInterval) {

                    currentCharge *= chargeDecay;
                    decayTickCounter = 0;

                }

            }

            // ability: spin sword in a circle
            if (GameData.IsAbilityUnlocked(AbilityType.Whirlwind) && playerController.GetCurrentEnergy() > 0) {

                totalRot += Mathf.DeltaAngle(transform.rotation.eulerAngles.z, rotPrev);
                rotPrev = transform.rotation.eulerAngles.z;
                if (Mathf.Abs(totalRot) >= spinKbRotAmount && !hasCircled) {

                    hasCircled = true;
                    playerController.ConsumeEnergy();
                    SpinKnockbackAbility();

                }

            } else
                totalRot = 0;


            // if it was originally a dash attack but then player began to swing, 
            // its no longer a dash attack
            if (isDashAttack && swingSpeed > minSwingSpeed)
                isDashAttack = false;

            // allowed some ticks where you can slip below the min speed or leave range
            // not counted during dash attack
            if (Mathf.Abs(speed) < minSwingSpeed && !isDashAttack)
                failedTicks++;

            //instantly break end when u swap directions, also rotate the weapon
            if ((dir < 0 && speed > 0) || (dir > 0 && speed < 0))
                break;

        }

        // dont reset charge for dash attacks
        if (!isDashAttack)
            currentCharge = 0;

        trail.emitting = false;

        // ability: strong sweeping
        enemiesSweeped = 0;

        // this ability lasts for 1 swing
        if (swingDestroysProjectiles) {

            swingDestroysProjectiles = false;
            weaponRenderer.color = Color.white;

        }


        swing = null;

    }

    private void PointAtMouse() => PointAtMouse(false);

    // point weapon to mouse. forceTrack does not care if mouse is in tracking zone, it will always point
    private void PointAtMouse(bool forceTrack) {

        // find player-to-mouse vector

        Vector3 forward = OriginToMouseVector();

        // point weapon at mouse
        Quaternion lookRot = transform.rotation;
        if (IsMouseInRange() || forceTrack)
            transform.rotation = Quaternion.Euler(0, 0,
                -Quaternion.LookRotation(forward, new Vector3(0, 0, -1)).eulerAngles.z - 90);

        // im not quite sure how exactly this code above works either so... yea
        // we don't modify x and y because then it makes the weapon appear to squish along its long axis as dist increases

    }

    // updates mouse tracking zone indicator. size will increase based on tracking radius and opacity will change base on mouse's distance to the indicator
    private void UpdateTrackingIndicator() {

        // size of tracking area (outer radius adjusted)
        float radius = maxTrackingRadius + OriginToCameraDistance();

        // set position and size of indicator
        rangeAreaIndicator.transform.position = new Vector3(origin.transform.position.x, origin.transform.position.y, 0);
        rangeAreaIndicator.transform.localScale = Vector2.one * radius;

        // calculate and apply alpha value
        float alpha = rangeIndicatorMaxAlpha;

        float mouseToCenterDist = ((Vector2) OriginToMouseVector()).magnitude;

        if (radius - mouseToCenterDist > 0f) { // mouse is inside tracking area

            // when radius = mouseToCenterDist,
            // alpha = rangeIndicatorMaxAlpha

            //https://www.desmos.com/calculator/zwraq56xpp
            alpha = -(2f * rangeIndicatorMaxAlpha / (1f + Mathf.Exp(-rangeIndicatorModifier * (radius - mouseToCenterDist)))) + 2f * rangeIndicatorMaxAlpha;

            rangeAreaIndicator.color = new Color(rangeIndicatorValidColor.r, rangeIndicatorValidColor.g, rangeIndicatorValidColor.b, alpha);

        } else { // mouse is outside tracking area

            rangeAreaIndicator.color = new Color(rangeIndicatorInvalidColor.r, rangeIndicatorInvalidColor.g, rangeIndicatorInvalidColor.b, alpha);

        }
    }

    // makes the weapon point cw or ccw depending on what direction it's moving in
    private void FlipWeapon() {

        if (speed < 0f)
            weaponSprite.transform.localScale = new Vector3(-weaponScale, weaponSprite.transform.localScale.y);
        else if (speed > 0f)
            weaponSprite.transform.localScale = new Vector3(weaponScale, weaponSprite.transform.localScale.y);

    }

    private void UpdateSpeedDisplay() {

        /* if (isTrackingSpeed)
             speedDisplay.text = "Weapon Peak Speed Last " + peakSpeedInterval + " Seconds: " + peakSpeed;
         else
             speedDisplay.text = ""; // im lazy*/

    }
    #endregion

    #region Combat
    private void OnTriggerEnter2D(Collider2D collision) {

        // you need to be swinging to hit anything
        if (swing == null)
            return;

        GameObject hit = collision.gameObject;

        // is swinging and hit enemy
        if (hit.GetComponent<Enemy>() != null) {

            Enemy enemy = hit.GetComponent<Enemy>();

            // enemy can be hit
            if (!enemy.IsInvulnerable() || enraged) {

                // ability: strong sweeping
                if (GameData.IsAbilityUnlocked(AbilityType.StrongSweeping))
                    enemiesSweeped++;

                // used for printing the information of each hit
                // TODO: can be removed...
                string toLog = "Strike on " + enemy.name + ":\n\t\t";

                float damage = GameData.GetStat(StatType.Damage);
                float kbPower = GameData.GetStat(StatType.Knockback); // knockback aka kbPower
                int critChance = (int) GameData.GetStat(StatType.CritChance);

                float velToApply = kbPower;
                float distToApply = kbPower * kbPowerDistRatio;
                float damageToApply = damage;
                bool crit = false;

                // ability: ninja dash (buffed dash attacks)
                if (playerController.IsDashing() && GameData.IsAbilityUnlocked(AbilityType.NinjaDashes))
                    damageToApply *= dashDamageMult;

                // ability: strong sweeping
                if (enemiesSweeped <= strongSweepMaxEnemies)
                    damageToApply *= 1 + (strongSweepBonus * enemiesSweeped);
                else
                    damageToApply *= 1 + (strongSweepBonus * strongSweepMaxEnemies);

                // if not fully charged then you get a hit with reduced effectiveness
                if (currentCharge < 1) {

                    // OLD COMBAT SYSTEM: 
                    //float speedRatio = swingSpeed / idealSwingSpeed;

                    // new combat system,
                    // based on weapon charge
                    //https://www.desmos.com/calculator/evgjtw4p4a
                    float charge = ((maxUnchargedCharge - minUnchargedCharge) * currentCharge) + minUnchargedCharge;

                    velToApply *= charge;
                    distToApply *= charge;
                    damageToApply *= charge;

                    toLog += "Weak Hit. ";

                } else {

                    toLog += "Ideal Hit! ";

                }

                // decide wether to crit, then apply it
                // NOTE: add function to increase crit chance when swingSpeed > idealSpeed
                int critDice = Random.Range(0, 101);

                if (critDice <= critChance || (GameData.IsAbilityUnlocked(AbilityType.WakeUpCall) && enemy.GetState() == Enemy.EnemyState.Exhausted)) {

                    crit = true;

                    velToApply *= critKbMult;
                    distToApply *= critKbMult;
                    damageToApply *= critDamageMult;

                    toLog += " MAXIMUM PULSE!";

                }

                //Debug.Log(toLog);

                Vector3 targetDir = (enemy.transform.position - transform.position).normalized;
                Vector3 weaponSwingDir = weaponSprite.transform.right * -GetWeaponSwingDirScalar();

                // now will adjust the dir vector magnitude based on swing speed and the modifier set in the inspector
                // see the fun math here (only took 4 hours baby)
                // https://www.desmos.com/calculator/yf6fqn6bld

                float swingSpeedAbs = Mathf.Abs(swingSpeed);

                float adjustedSwingSpeed = minSwingSpeed * (1 + ((swingSpeedAbs - minSwingSpeed)
                                                            / (idealSwingSpeed - minSwingSpeed)));

                float kbMod = kbSwingDirModifier * (3 / 2) * ((Mathf.Pow(adjustedSwingSpeed, 2) +
                            (minSwingSpeed * adjustedSwingSpeed) - (2 * Mathf.Pow(minSwingSpeed, 2))) /
                            (adjustedSwingSpeed * (adjustedSwingSpeed + minSwingSpeed)));

                Vector3 kbDir = targetDir + (weaponSwingDir * kbMod);

                if (enemy is not DefensiveMeleeEnemy) {

                    enemy.TakeDamage(damageToApply);

                } else

                    ((DefensiveMeleeEnemy) enemy).TakeDamage(damageToApply);

                enemy.ApplyKnockback(velToApply, distToApply, kbDir.normalized);

                if (crit && critPopup)
                    popups.Play(critPopup, enemy.transform.position, false);
                else if (hitPopup)
                    popups.Play(hitPopup, enemy.transform.position, false);

                // ability: crits slow enemies
                if (GameData.IsAbilityUnlocked(AbilityType.SprainedStems) && crit)
                    enemy.ApplySlow(critSlowAmount, critSlowDuration);

                //ability:  on kill heal chance
                if (enemy.CurrentHealth() < 0 && GameData.IsAbilityUnlocked(AbilityType.Leftovers)) {

                    int healDice = Random.Range(0, 101);
                    if (healDice <= onKillHealChance)
                        playerController.Heal(playerController.GetMaxHealth() * onKillHealAmount);

                }
            }
        }

        // is swinging and hit projectile
        // ability: projectile block
        // no need to check for GameData.projectileBlockEnabled,
        // that is checked in PlayerController where the ability is cast. 
        else if (hit.GetComponent<Projectile>() != null && swingDestroysProjectiles) {

            Projectile projectile = hit.GetComponent<Projectile>();
            projectile.Deactivate();

        }
    }

    private void SpinKnockbackAbility() {

        RaycastHit2D[] hits = Physics2D.CircleCastAll(playerController.transform.position, spinKbApplicationRadius, Vector2.zero, 0);
        popups.Play(spinKbPopup, playerController.gameObject, false);

        foreach (RaycastHit2D hit in hits) {

            if (hit.transform.gameObject.layer == 7) {

                Enemy enemy = hit.transform.gameObject.GetComponent<Enemy>();

                float kbVel = GameData.GetStat(StatType.Knockback) * spinKbPowerMult;
                float kbDist = kbVel * spinKbPowerDistRatio;

                enemy.ApplyKnockback(kbVel, kbDist);
                enemy.ApplySlow(spinKbSlow, spinKbSlowDuration);

            }
        }
    }
    #endregion

    #region Util
    // returns the resultant vector of the origin's position minus the mouse's world position
    // please note that this must be a Vector3 since rotation is on the z-axis
    public Vector3 OriginToMouseVector() {

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));
        Vector3 originPos = new Vector3(origin.transform.position.x, origin.transform.position.y, 0);

        return mouseWorldPos - originPos;

    }

    // returns the distance of the origin to the main camera. used for dynamic radius
    // if dynamicMaxRadiusMult = 0, this returns zero.
    private float OriginToCameraDistance() {

        float dist = Vector2.Distance(origin.transform.position, cameraController.transform.position);
        return dist * dynamicMaxRadiusMult;

    }

    // start/stop ticking the speed tracker coroutine
    public void ShouldTrackSpeed(bool should) {

        if (should) {

            if (!isTrackingSpeed) {

                StartCoroutine(speedTracker);
                isTrackingSpeed = true;
                print("Began tracking weapon again");

            } else
                print("Can't start tracking weapon speed, we are already tracking it you drone");

        } else if (!should && isTrackingSpeed) {

            if (isTrackingSpeed) {

                StopCoroutine(speedTracker);
                isTrackingSpeed = false;
                print("Stopped weapon tracking");

            } else {

                print("Can't stop tracking weapon speed, its already stopped you drone");

            }
        }
    }

    // change origin GameObject (transform)
    public void SetOrigin(GameObject origin) => this.origin = origin;

    // check if the mouse is inside the tracking area
    private bool IsMouseInRange() {

        Vector3 forward = OriginToMouseVector();

        // dist = length = magnitude 
        float dist = Mathf.Sqrt(Mathf.Pow(forward.x, 2) + Mathf.Pow(forward.y, 2));
        float adjust = OriginToCameraDistance();

        return (dist > minTrackingRadius) && (dist < maxTrackingRadius + adjust);

    }

    private float GetWeaponSwingDirScalar() => speed > 0f ? 1f : -1f; // positive if clockwise, negative for counterclockwise

    // returns the current speed of the weapon
    public void ShowWeaponSpeed(bool show) => displaySpeed = show;

    private void OnDrawGizmosSelected() {

        #region Rotation Vector
        if (swing == null)
            Gizmos.color = Color.white;
        else
            Gizmos.color = Color.green;

        Vector3 direction = weaponSprite.transform.right * -GetWeaponSwingDirScalar() * 3f;
        Gizmos.DrawLine(weaponSprite.transform.position, weaponSprite.transform.position + direction);
        #endregion

    }

    public bool IsSwinging() => swing != null;

    public void NextSwingDestroysProjectiles() => swingDestroysProjectiles = true;
    #endregion

    #region Accessors
    public float GetSwingCharge() => currentCharge;

    public void SetPlayerEnraged(bool enraged) => this.enraged = enraged;
    #endregion

}