using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

[RequireComponent(typeof(PlayerSoundManager))]
public class PlayerController : MonoBehaviour {

    [Header("References")]
    private CameraController cameraController;
    private PlayerSoundManager soundManager;
    private UpgradeDatabase upgradeDatabase;
    private UIManager uiManager;
    private MapManager roomManager;
    private Rigidbody2D rb;
    private HealthBarUI healthBarUI;
    private PopupPlayer popups;
    private WeaponManager weapon;
    private ProjectileManager projectileManager;
    private SpriteRenderer sprite;

    [Header("Movement")]
    [SerializeField] private float baseAgility;
    [SerializeField] private float maxAgility;
    [SerializeField][Tooltip("Used to balance the default large effects of the agility stat")] private float roundAgilityIncrement;
    private Vector2 moveDirection;
    private float horizontalInput;
    private float verticalInput;
    private float agility; // agility is move speed
    private bool isDashing;

    [Header("Dash")]
    [SerializeField][Tooltip("This times moveSpeed equals dashSpeed")] private float moveSpeedDashSpeedRatio;
    [SerializeField] private float dashDuration;
    [SerializeField] private Vector2 defaultDashDir;
    private Vector2 currentDashDestination;
    private float dashSpeed;

    [Header("Dash Arrow")]
    [SerializeField] private Transform dashArrow;
    [SerializeField] private float dashArrowLerpSpeed;

    [Header("Interact")]
    [SerializeField] private float interactRadius;
    private Interactable lastClosestInteractable;

    [Header("Energy")]
    [SerializeField] private int initialEnergy; // initial energy can be used to change the starting number of dashes
    [SerializeField] private float energyRegenTime;
    private int maxEnergy;
    private float currEnergyCooldownProgress;
    private int currentEnergy;

    [Header("Health")]
    [SerializeField] private float maxHealth;
    private float health;
    private bool isDead;

    [Header("Damage Resistance")]
    [SerializeField] private float maxDamageResistance; // damage resistance is capped at this value
    private float damageResistance;

    [Header("Camera Shake")]
    [SerializeField] private float shakeMagnitude;
    [SerializeField] private float shakeDuration;

    [Header("Invulnerability")]
    [SerializeField][Min(0f)] private float hitInvulnerabilityDuration;
    [SerializeField][Range(0f, 1f)] private float invulnerabilityAnimMaxAlpha;
    [SerializeField][Range(0f, 1f)] private float invulnerabilityAnimMinAlpha;
    [SerializeField][Min(0f)] private int invulnerabilityAnimNumFlashes;
    private bool invulnerable;
    private Coroutine hitInvulnerabilityCoroutine;

    [Header("Special Items")]
    [SerializeField] private Sprite emptySlotIcon;
    [SerializeField][Min(0f)] private float healthSmoothieDuration; // duration over which the health is added
    [SerializeField][Min(0f)] private float healthSmoothieTotalHealth; // amount of health to heal when smoothie is consumed
    [SerializeField][Min(0f)] private float rageSmoothieDuration; // duration of rage
    [SerializeField][Min(1f)] private float rageMoveSpeedMult; // move speed is multiplied by this
    [SerializeField][Min(0f)] private float energySmoothieDuration; // duration of energy boost
    [SerializeField] private float energySmoothieChargeFactor; // energy charge rate is divided by this for the duration
    private SpecialItemInventoryStorage specialItemStorage;
    private SpecialItemSlot specialItemSlot;

    [Header("Abilities")]
    [SerializeField] private Projectile playerProjectile;
    [SerializeField] private float playerProjectileSpeedRatio;
    [SerializeField] private float playerProjectileDamageRatio;
    [SerializeField][Range(0f, 1f)][Tooltip("Health must be below this percent to enable coward dash")] private float cowardDashThreshold;
    [SerializeField][Range(1f, 3f)][Tooltip("Move/dash speed ratio multipled by this when coward dash is enabled")] private float cowardDashRatioMult;
    [SerializeField][Range(0f, 1f)] private float thornsDamageRatio;
    [SerializeField][Range(0f, 0.999f)] private float thornsSlowAmount;
    [SerializeField][Min(0f)] private float thornsSlowDuration;
    private bool cowardDashActivated;

    [Header("Inputs")]
    [SerializeField] private KeyCode interactKey;
    [SerializeField] private KeyCode specialItemKey;
    [SerializeField] private KeyCode dashKey;
    [SerializeField] private KeyCode blockKey;
    [SerializeField] private MouseButton playerProjectileButton;

    [Header("Popups")]
    [SerializeField] private Popup healthSmoothiePopup;
    [SerializeField] private Popup rageSmoothiePopup;
    [SerializeField] private Popup energySmoothiePopup;
    [SerializeField] private Popup colaPopup;

    #region Core
    private void Awake() {

        maxEnergy = initialEnergy + GameData.GetUpgradeTier(UpgradeType.Energy); // set max energy here so it is set for the EnergyUIManager
        currentEnergy = maxEnergy; // set current energy to max energy

    }

    private void Start() {

        cameraController = FindFirstObjectByType<CameraController>();
        soundManager = GetComponentInChildren<PlayerSoundManager>();
        upgradeDatabase = FindFirstObjectByType<UpgradeDatabase>();
        roomManager = FindFirstObjectByType<MapManager>();
        rb = GetComponent<Rigidbody2D>();
        uiManager = FindFirstObjectByType<UIManager>();
        healthBarUI = FindFirstObjectByType<HealthBarUI>();
        specialItemSlot = FindFirstObjectByType<SpecialItemSlot>();
        popups = FindAnyObjectByType<PopupPlayer>();
        weapon = FindAnyObjectByType<WeaponManager>();
        projectileManager = FindAnyObjectByType<ProjectileManager>();
        sprite = GetComponent<SpriteRenderer>();

        specialItemStorage = new SpecialItemInventoryStorage(); // initialize the special item storage

        cameraController.Initialize(transform); // set the camera to follow the player
        specialItemSlot.Initialize(emptySlotIcon);

        float roundBaseAgility = baseAgility + (roundAgilityIncrement * (GameData.GetRoundNumber() - 1)); // set base agility based on round number; linear scaling through an increment each round

        if (!GameData.IsInitialPlayerStatsSet()) // if the player stats have not been set yet, set them
            GameData.SetInitialPlayerStats(roundBaseAgility); // set initial player stats

        GameData.SetRoundBaseAgility(roundBaseAgility); // set the round base agility
        GameData.SetRoundAgilityIncrement(roundAgilityIncrement); // set the round agility multiplier

        agility = Mathf.Min(GameData.GetStat(StatType.Agility), maxAgility); // player agility is capped at maxMoveSpeed

        float damageTierInterval = maxDamageResistance / upgradeDatabase.GetUpgradeData(UpgradeType.DamageResistance).GetMaxTier(); // calculate the damage resistance tier interval by dividing the max damage resistance by the max tier of the upgrade
        damageResistance += damageTierInterval * GameData.GetUpgradeTier(UpgradeType.DamageResistance); // set the damage resistance based on the upgrade tier

        SetHealth(maxHealth); // set health to max health

        StartCoroutine(UpdateDashArrow()); // start the coroutine to update the dash arrow direction continuously

        transform.position = roomManager.GetPlayerSpawn(); // spawn player at spawn point

        healthBarUI.SetMaxHealth(maxHealth);

        dashSpeed = agility * moveSpeedDashSpeedRatio;

        playerProjectile.SetDamage(GameData.GetStat(StatType.Damage) * playerProjectileDamageRatio);
        playerProjectile.SetSpeed(agility * playerProjectileSpeedRatio * GameSettings.UNIVERSAL_AGILITY_MULTIPLIER);
        playerProjectile.SetTargetLayer(Projectile.ProjectileTarget.Enemy);

    }

    private void Update() {

        #region Movement
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector2(horizontalInput, verticalInput).normalized;
        #endregion

        #region Abilities
        // energy abilities
        if (currentEnergy > 0) {

            // dash
            if (Input.GetKeyDown(dashKey) && !isDashing) {

                currentDashDestination = GetDashDestination();
                StartCoroutine(Dash());
                currentEnergy--;

            }

            // ability: projectile block 
            if (GameData.IsAbilityUnlocked(AbilityType.Deflection) && Input.GetKeyDown(blockKey)) {

                weapon.NextSwingDestroysProjectiles();
                //projectileBlockCooldownCoroutine = StartCoroutine(projectile);
                currentEnergy--;

            }

            // ability: player projectile
            if (GameData.IsAbilityUnlocked(AbilityType.ChuckingCheese) && Input.GetMouseButtonDown((int) playerProjectileButton)) {

                projectileManager.FireProjectile(playerProjectile, transform.position, (Vector2) weapon.OriginToMouseVector().normalized);
                currentEnergy--;

            }
        }

        // ability: coward dash
        if (GameData.IsAbilityUnlocked(AbilityType.Cowardice)) {

            if (health < maxHealth * cowardDashThreshold && !cowardDashActivated) {

                moveSpeedDashSpeedRatio *= cowardDashRatioMult;
                cowardDashActivated = true;

            } else if (health > maxHealth * cowardDashThreshold && cowardDashActivated) {

                moveSpeedDashSpeedRatio /= cowardDashRatioMult;
                cowardDashActivated = false;

            }
        }

        #endregion

        #region Interact
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactRadius);

        // find the closest interactable object
        Interactable closestInteractable = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D collider in colliders) {

            Interactable interactable = collider.GetComponent<Interactable>();

            if (interactable != null) {

                float distance = Vector2.Distance(transform.position, collider.transform.position);

                if (distance < closestDistance) {

                    closestInteractable = interactable;
                    closestDistance = distance;

                }
            }
        }

        if (lastClosestInteractable != closestInteractable) { // check if the closest interactable object has changed

            lastClosestInteractable?.HideInteractIcon(); // hide the interact icon of the last closest interactable object
            closestInteractable?.ShowInteractIcon(); // show the interact icon of the new closest interactable object

        }

        lastClosestInteractable = closestInteractable;

        // check for interactable objects in the interact radius
        if (Input.GetKeyDown(interactKey)) {

            // interact with the closest interactable object
            if (closestInteractable != null)
                closestInteractable.OnInteract();

        }
        #endregion

        #region Energy
        // energy recharge
        if (currentEnergy < maxEnergy) {

            currEnergyCooldownProgress += Time.deltaTime;
            uiManager.UpdateCooldown(currEnergyCooldownProgress / energyRegenTime); // % completion

            if (currEnergyCooldownProgress >= energyRegenTime) {

                currentEnergy++;
                uiManager.PlayRechargedAnimation();
                currEnergyCooldownProgress = 0;

            }
        }
        #endregion

        #region Special Items
        if (Input.GetKeyDown(specialItemKey) && !specialItemSlot.IsEmpty()) {

            SpecialItemData specialItemData = specialItemSlot.GetSpecialItemData();

            switch (specialItemData.GetSpecialItemType()) {

                case SpecialItemType.HealthSmoothie:

                    StartCoroutine(ApplyHealthSmoothieEffect());
                    popups.Play(healthSmoothiePopup, gameObject, healthSmoothieDuration);
                    specialItemStorage.RemoveSpecialItem(new SpecialItem(specialItemSlot.GetSpecialItemData()), 1); // remove the special item from the storage since it has been used
                    break;

                case SpecialItemType.RageSmoothie:

                    StartCoroutine(ApplyRageSmoothieEffect());
                    popups.Play(rageSmoothiePopup, gameObject, rageSmoothieDuration);
                    specialItemStorage.RemoveSpecialItem(new SpecialItem(specialItemSlot.GetSpecialItemData()), 1); // remove the special item from the storage since it has been used
                    break;

                case SpecialItemType.EnergySmoothie:

                    StartCoroutine(ApplyEnergySmoothieEffect());
                    popups.Play(energySmoothiePopup, gameObject, energySmoothieDuration);
                    specialItemStorage.RemoveSpecialItem(new SpecialItem(specialItemSlot.GetSpecialItemData()), 1); // remove the special item from the storage since it has been used
                    break;

                //case SpecialItemType.Cola:
                //
                //    currentEnergy = maxEnergy;
                //    // TODO: reset all cooldowns
                //    popups.Play(colaPopup, gameObject, false);
                //    break;

                default:

                    print("What did you just eat...?");
                    break;

            }

            specialItemSlot.SetStack(null); // remove the item from the special item slot

        }
        #endregion

    }

    private void FixedUpdate() {

        // only move player if not dashing
        if (!isDashing)
            rb.linearVelocity = agility * GameSettings.UNIVERSAL_AGILITY_MULTIPLIER * Time.fixedDeltaTime * moveDirection; // terms reordered for best performance

    }
    #endregion

    #region Item Behaviors

    private IEnumerator ApplyHealthSmoothieEffect() {

        float currentTime = 0f;
        float healthPerSecond = healthSmoothieTotalHealth / healthSmoothieDuration;

        while (currentTime < healthSmoothieDuration) {

            AddHealth(healthPerSecond * Time.deltaTime);
            currentTime += Time.deltaTime;
            yield return null;

        }
    }

    private IEnumerator ApplyRageSmoothieEffect() {

        float currentTime = 0f;
        float normalMoveSpeed = agility;

        agility *= rageMoveSpeedMult;
        weapon.SetPlayerEnraged(true);

        while (currentTime < rageSmoothieDuration) {

            currentTime += Time.deltaTime;
            yield return null;

        }

        weapon.SetPlayerEnraged(false);
        agility = normalMoveSpeed;

    }

    private IEnumerator ApplyEnergySmoothieEffect() {

        float currentTime = 0f;
        float defaultEnergyChargeRate = energyRegenTime;
        energyRegenTime /= energySmoothieChargeFactor;

        while (currentTime < energySmoothieDuration) {

            currentTime += Time.deltaTime;
            yield return null;

        }

        energyRegenTime = defaultEnergyChargeRate;

    }

    #endregion

    #region Actions
    // dash in the current direction or default direction
    private IEnumerator Dash() {

        // sadly, this seems to overshoot...
        // I think its worse if dash duration is lower. idk how to fix it
        // I'm pretty sure the reason the dash ends late is because of the FixedUpdate interval
        // when making dash depend on Update instead of FixedUpdate, it *usually* Dashes the right distance but sometimes goes a bit too far. I think thats worse

        dashSpeed = agility * moveSpeedDashSpeedRatio;

        uiManager.ConsumeEnergy();
        soundManager.PlaySound(PlayerSoundType.Dash);

        isDashing = true; // wait

        Vector2 dir = rb.linearVelocity.normalized;

        if (dir.Equals(Vector2.zero))
            dir = defaultDashDir.normalized;

        rb.linearVelocity = dir * dashSpeed;

        // lets dash, dash, whatever
        float distRemaining = dashDuration * dashSpeed;

        while (distRemaining > 0) {

            // yes, this is a little unorthodox (could just use a timer)
            // but I tried for a while to fix the innacuracy and simply could not
            yield return new WaitForFixedUpdate();
            distRemaining -= dashSpeed * Time.fixedDeltaTime;

        }

        isDashing = false; // done

        yield return null;

    }

    #endregion

    #region Health
    // can be private because health is only modified by the player itself (for example, collisions with health pickups, taking damage, etc.; this could be changed later)
    private void SetHealth(float health) {

        this.health = health;
        healthBarUI.UpdateHealthBar(this.health);

    }

    // can be private because health is only modified by the player itself (for example, collisions with health pickups, taking damage, etc.; this could be changed later)
    private void AddHealth(float health) {

        this.health = Mathf.Min(this.health + health, maxHealth); // ensure health does not exceed max health
        healthBarUI.UpdateHealthBar(this.health);

    }

    // can be private because health is only modified by the player itself (for example, collisions with health pickups, taking damage, etc.; this could be changed later)
    private void RemoveHealth(float health) {

        if (this.health > 0) {

            this.health -= health;
            healthBarUI.UpdateHealthBar(this.health);

        } else {

            Die();

        }
    }

    public void TakeDamage(float damage, bool playSound) {

        // ability: ninja dash (dash immunity)
        // if ninja dashes are enabled and you arent dashing then you ignore the damage
        if (!invulnerable && (!GameData.IsAbilityUnlocked(AbilityType.NinjaDashes) || !isDashing)) {

            RemoveHealth(damage * (1f - damageResistance)); // damage is reduced by the damage resistance

            if (playSound)
                soundManager.PlaySound(PlayerSoundType.Damaged);

            if (hitInvulnerabilityCoroutine != null) StopCoroutine(hitInvulnerabilityCoroutine); // stop the previous coroutine if it is running
            hitInvulnerabilityCoroutine = StartCoroutine(HandleHitInvulnerability());

            cameraController.ShakeCamera(shakeDuration, shakeMagnitude); // shake the camera

        }
    }

    public void TakeDamage(float damage) => TakeDamage(damage, true);

    public void Heal(float heal) => AddHealth(heal);

    public IEnumerator HandleHitInvulnerability() {

        invulnerable = true;

        float flashDuration = hitInvulnerabilityDuration / invulnerabilityAnimNumFlashes;

        Color start = sprite.color;
        // max alpha
        Color peak = new Color(start.r, start.g, start.b, invulnerabilityAnimMaxAlpha);
        // min alpha
        Color trough = new Color(start.r, start.g, start.b, invulnerabilityAnimMinAlpha);

        // flash the alpha value
        for (int i = 0; i < invulnerabilityAnimNumFlashes; i++) {

            float currentTime = 0f;
            float time; // smoothening

            // lerp alpha from max to min
            while (currentTime < flashDuration / 2f) {

                time = currentTime / (flashDuration / 2f);
                time = 1f - Mathf.Cos(time * Mathf.PI * 0.5f); // ease in
                sprite.color = Color.Lerp(peak, trough, time);
                currentTime += Time.deltaTime;
                yield return new WaitForEndOfFrame();

            }

            sprite.color = peak;

            // lerp alpha from min to max
            currentTime = 0f;

            while (currentTime < flashDuration / 2f) {

                time = currentTime / (flashDuration / 2f);
                time = Mathf.Sin(time * Mathf.PI * 0.5f); // ease out
                sprite.color = Color.Lerp(trough, peak, time);
                currentTime += Time.deltaTime;
                yield return new WaitForEndOfFrame();

            }

            sprite.color = trough;

        }

        sprite.color = start;
        yield return new WaitForSeconds(hitInvulnerabilityDuration);
        invulnerable = false;

        hitInvulnerabilityCoroutine = null;

    }

    private void Die() {

        isDead = true;
        uiManager.ShowDeathScreen();

    }
    #endregion

    #region Util
    // returns the Vector2 coordinate Player would end up at if they were to dash at the time the method was called
    // also takes into account the player's direction of movement AKA the dash direction
    // mathematically, this method is accurate. in practice, it is off. see Dash for more details on why
    // is it viable to just lerp the position for a dash and check for collisions...?
    private Vector2 GetDashDestination() {

        Vector2 target = transform.position;
        float dashDist = dashSpeed * dashDuration;

        if (moveDirection.Equals(Vector2.zero)) { // if player is not moving

            target += defaultDashDir.normalized * dashDist;

        } else if (moveDirection.x == 0 && moveDirection.y != 0) {

            if (moveDirection.y > 0)
                target.y += dashDist;
            else
                target.y -= dashDist;

        } else if (moveDirection.x != 0 && moveDirection.y == 0) {

            if (moveDirection.x > 0)
                target.x += dashDist;
            else
                target.x -= dashDist;

        } else {

            // unit circle time!
            if (moveDirection.x > 0 && moveDirection.y > 0) {

                target.x += dashDist * Mathf.Cos(Mathf.PI / 4);
                target.y += dashDist * Mathf.Sin(Mathf.PI / 4);

            }

            if (moveDirection.x < 0 && moveDirection.y > 0) {

                target.x += dashDist * Mathf.Cos(3 * Mathf.PI / 4);
                target.y += dashDist * Mathf.Sin(3 * Mathf.PI / 4);

            }

            if (moveDirection.x < 0 && moveDirection.y < 0) {

                target.x += dashDist * Mathf.Cos(5 * Mathf.PI / 4);
                target.y += dashDist * Mathf.Sin(5 * Mathf.PI / 4);

            }

            if (moveDirection.x > 0 && moveDirection.y < 0) {

                target.x += dashDist * Mathf.Cos(7 * Mathf.PI / 4);
                target.y += dashDist * Mathf.Sin(7 * Mathf.PI / 4);

            }
        }

        return target;

    }

    private IEnumerator UpdateDashArrow() {

        while (true) {

            Vector2 direction = (GetDashDestination() - (Vector2) transform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float currentAngle = dashArrow.eulerAngles.z;

            float angle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * dashArrowLerpSpeed);
            dashArrow.rotation = Quaternion.Euler(0, 0, angle);

            yield return null;

        }
    }

    public void ConsumeEnergy() {

        if (currentEnergy > 0)
            currentEnergy--;

    }

    private void OnDrawGizmos() {

        // updates the dash arrow direction in the editor when not playing
        if (!Application.isPlaying)
            dashArrow.right = GetDashDestination() - (Vector2) transform.position;

    }

    private void OnDrawGizmosSelected() {

        #region Dash Distance Visualizer
        Gizmos.color = Color.white;

        if (isDashing) // while dashing
            Gizmos.color = Color.yellow;

        if (Application.isPlaying)
            Gizmos.DrawWireCube(currentDashDestination, transform.localScale);
        else
            Gizmos.DrawWireCube(GetDashDestination(), transform.localScale);
        #endregion

    }
    #endregion

    #region Accessors
    public int GetMaxEnergy() => maxEnergy;

    public int GetCurrentEnergy() => currentEnergy;

    public bool IsDashing() => isDashing;

    public float GetMaxHealth() => maxHealth;

    public float GetThornsDamageRatio() => thornsDamageRatio;

    public float GetThornsSlowAmount() => thornsSlowAmount;

    public float GetThornsSlowDuration() => thornsSlowDuration;

    public bool IsDead() => isDead;
    #endregion

}
