using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

public class GetUnits : MonoBehaviour
{
    [Serializable]
    private class TroopState
    {
        public GameObject gameObject;
        public Transform transform;
        public Transform parent;
        public SpriteRenderer renderer;

        public Vector3 baseLocalPosition;
        public Vector3 microOffsetTarget;
        public Vector3 velocity;

        public Vector2 disorderDirection;
        public Vector2 sortPosition;

        public float nextRetargetTime;
        public float responseScale;
    }

    [Header("Troops")]
    [SerializeField] private List<GameObject> list = new List<GameObject>();
    [SerializeField] private List<SpriteRenderer> listS = new List<SpriteRenderer>();

    [Header("World Follow")]
    [SerializeField] private Transform targetRoot;
    [SerializeField] private Transform formationSpaceRoot;
    [SerializeField] private bool snapToTargetOnStart = true;
    [SerializeField] private float rootPositionSmoothTime = 0.08f;
    [SerializeField] private float rootRotationSharpness = 12f;
    [SerializeField] private float maxSnapDistance = 3f;

    [Header("Death")]
    [SerializeField] private GameObject deadTroopPrefab;
    [SerializeField] private float corpseTiltAmount = 12f;
    [SerializeField] private int formationMaxHealth = 100;

    [Header("Base Formation Disorder")]
    [SerializeField] private float baseDisorderAmount = 0.08f;
    [SerializeField] private float maxDisorderOffsetX = 0.08f;
    [SerializeField] private float maxDisorderOffsetY = 0.03f;

    [Header("Human Motion - Calm")]
     private float calmShiftAmountX = 0.12f;
     private float calmShiftAmountY = 0.07f;
    [SerializeField] private float calmRetargetIntervalMin = 1.0f;
    [SerializeField] private float calmRetargetIntervalMax = 3f;

    [Header("Human Motion - Panic")]
     private float panicShiftAmountX = 0.8f;
     private float panicShiftAmountY = 0.5f;
    [SerializeField] private float panicRetargetIntervalMin = 0.5f;
    [SerializeField] private float panicRetargetIntervalMax = 0.8f;

    [Header("Troop Slot Smoothing")]
    private float troopMoveSmoothTime = 2f;

    [Header("Morale")]
    [SerializeField, Range(0f, 100f)] private float currentMorale = 100f;
    [SerializeField] private float moraleSmoothingSpeed = 8f;

    [Header("Morale Panic")]
    [SerializeField] private float maxExtraDisorderFromPanic = 0.22f;
    [SerializeField] private float panicBuildSpeed = 2.2f;
    [SerializeField] private float panicRecoverSpeed = 0.9f;
    [SerializeField]
    private AnimationCurve moraleToPanicCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.08f),
        new Keyframe(0.55f, 0.35f),
        new Keyframe(0.8f, 0.75f),
        new Keyframe(1f, 1f)
    );

    [Header("Visual Scripting Morale")]
    [SerializeField] private bool readMoraleFromScriptMachine = false;
    [SerializeField] private GameObject moraleVariableObject;
    [SerializeField] private string moraleVariableName = "Morale";
    [SerializeField] private float moralePollInterval = 0.10f;

    [Header("Default Style")]
    [SerializeField] private Color defaultColorA = Color.red;
    [SerializeField] private Color defaultColorB = Color.red;
    [SerializeField] private float defaultFactionDisorderAmount = 0.08f;
    [SerializeField, Range(0f, 100f)] private float defaultMorale = 100f;

    private readonly List<TroopState> troopStates = new List<TroopState>();
    private readonly List<TroopState> renderOrder = new List<TroopState>();
    private readonly List<TroopState> killOrder = new List<TroopState>();

    private int nextKillOrderIndex = 0;
    private int killedTroopCount = 0;

    private Color colorA = Color.white;
    private Color colorB = Color.white;

    private float displayedMorale = 100f;
    private float displayedPanic = 0f;
    private float nextMoralePollTime = 0f;

    private Vector3 rootFollowVelocity;

    private void Awake()
    {
        if (formationSpaceRoot == null)
        {
            formationSpaceRoot = transform;
        }

        if (snapToTargetOnStart && targetRoot != null)
        {
            SnapVisualRootToTarget();
        }

        CacheTroopData();
        BuildKillOrder();

        baseDisorderAmount = Mathf.Max(0f, defaultFactionDisorderAmount);
        currentMorale = Mathf.Clamp(defaultMorale, 0f, 100f);
        displayedMorale = currentMorale;

        float startMorale01 = Mathf.Clamp01(currentMorale / 100f);
        float startPanicInput = 1f - startMorale01;
        displayedPanic = Mathf.Clamp01(moraleToPanicCurve.Evaluate(startPanicInput));

        SetColors(defaultColorA, defaultColorB);

        transform.parent = targetRoot.parent;
    }

    private void LateUpdate()
    {
        UpdateMoraleFromScriptMachine();

        displayedMorale = Mathf.Lerp(
            displayedMorale,
            currentMorale,
            1f - Mathf.Exp(-moraleSmoothingSpeed * Time.deltaTime)
        );

        float morale01 = Mathf.Clamp01(displayedMorale / 100f);
        float panicInput = 1f - morale01;
        float targetPanic = Mathf.Clamp01(moraleToPanicCurve.Evaluate(panicInput));

        float panicSpeed = targetPanic > displayedPanic
            ? panicBuildSpeed
            : panicRecoverSpeed;

        displayedPanic = Mathf.Lerp(
            displayedPanic,
            targetPanic,
            1f - Mathf.Exp(-panicSpeed * Time.deltaTime)
        );

        UpdateVisualRootFollow();
        UpdateTroopLocalMotion();
    }

    private void UpdateVisualRootFollow()
    {
        if (targetRoot == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, targetRoot.position);
        if (distance > maxSnapDistance)
        {
            transform.position = targetRoot.position;
            transform.rotation = targetRoot.rotation;
            rootFollowVelocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetRoot.position,
            ref rootFollowVelocity,
            rootPositionSmoothTime
        );

        float t = 1f - Mathf.Exp(-rootRotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRoot.rotation,
            t
        );
    }

    private void UpdateTroopLocalMotion()
    {
        float time = Time.time;
        float panic01 = displayedPanic;
        float totalDisorder = Mathf.Clamp01(baseDisorderAmount + (maxExtraDisorderFromPanic * panic01));

        int count = troopStates.Count;
        for (int i = 0; i < count; i++)
        {
            TroopState troop = troopStates[i];

            if (troop.gameObject == null) continue;
            if (!troop.gameObject.activeSelf) continue;

            if (time >= troop.nextRetargetTime)
            {
                RetargetMicroOffset(troop, panic01, time);
            }

            Vector3 disorderOffset = new Vector3(
                troop.disorderDirection.x * maxDisorderOffsetX * totalDisorder,
                troop.disorderDirection.y * maxDisorderOffsetY * totalDisorder,
                0f
            );

            Vector3 targetLocal = troop.baseLocalPosition + disorderOffset + troop.microOffsetTarget;

            float actualSmoothTime = Mathf.Lerp(
                troopMoveSmoothTime,
                troopMoveSmoothTime * 0.65f,
                panic01
            ) * troop.responseScale;

            troop.transform.localPosition = Vector3.SmoothDamp(
                troop.transform.localPosition,
                targetLocal,
                ref troop.velocity,
                actualSmoothTime
            );
        }
    }

    private void RetargetMicroOffset(TroopState troop, float panic01, float time)
    {
        float intervalMin = Mathf.Lerp(calmRetargetIntervalMin, panicRetargetIntervalMin, panic01);
        float intervalMax = Mathf.Lerp(calmRetargetIntervalMax, panicRetargetIntervalMax, panic01);
        troop.nextRetargetTime = time + UnityEngine.Random.Range(intervalMin, intervalMax);

        float amountX = Mathf.Lerp(calmShiftAmountX, panicShiftAmountX, panic01);
        float amountY = Mathf.Lerp(calmShiftAmountY, panicShiftAmountY, panic01);

        int choiceMax = panic01 > 0.45f ? 8 : 5;
        int choice = UnityEngine.Random.Range(0, choiceMax);

        Vector2 localOffset;
        switch (choice)
        {
            default:
            case 0:
                localOffset = Vector2.zero;
                break;
            case 1:
                localOffset = new Vector2(-1f, 0f);
                break;
            case 2:
                localOffset = new Vector2(1f, 0f);
                break;
            case 3:
                localOffset = new Vector2(0f, 0.25f);
                break;
            case 4:
                localOffset = new Vector2(0f, -0.30f);
                break;
            case 5:
                localOffset = new Vector2(-0.85f, -0.25f);
                break;
            case 6:
                localOffset = new Vector2(0.85f, -0.25f);
                break;
            case 7:
                localOffset = new Vector2(0f, -0.60f);
                break;
        }

        float scale = UnityEngine.Random.Range(0.55f, 1f);

        troop.microOffsetTarget = new Vector3(
            localOffset.x * amountX * scale,
            localOffset.y * amountY * scale,
            0f
        );
    }

    private void UpdateMoraleFromScriptMachine()
    {
        if (!readMoraleFromScriptMachine) return;
        if (Time.time < nextMoralePollTime) return;

        nextMoralePollTime = Time.time + moralePollInterval;

        GameObject source = moraleVariableObject != null ? moraleVariableObject : gameObject;

        try
        {
            object raw = Variables.Object(source).Get(moraleVariableName);

            if (raw is float f)
            {
                SetMorale(f);
            }
            else if (raw is int i)
            {
                SetMorale(i);
            }
        }
        catch
        {
        }
    }

    private void CacheTroopData()
    {
        troopStates.Clear();
        renderOrder.Clear();
        listS.Clear();

        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            GameObject troopObject = list[i];
            if (troopObject == null) continue;

            TroopState troop = new TroopState();
            troop.gameObject = troopObject;
            troop.transform = troopObject.transform;
            troop.parent = troop.transform.parent;
            troop.renderer = troopObject.GetComponent<SpriteRenderer>();
            troop.baseLocalPosition = troop.transform.localPosition;
            troop.microOffsetTarget = Vector3.zero;
            troop.velocity = Vector3.zero;
            troop.responseScale = UnityEngine.Random.Range(0.92f, 1.08f);
            troop.nextRetargetTime = Time.time + UnityEngine.Random.Range(0.05f, 0.6f);

            Vector2 randomDir2D = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir2D == Vector2.zero)
            {
                randomDir2D = Vector2.right;
            }
            troop.disorderDirection = randomDir2D;

            Vector3 sortPos3 = formationSpaceRoot != null
                ? formationSpaceRoot.InverseTransformPoint(troop.transform.position)
                : troop.transform.localPosition;

            troop.sortPosition = new Vector2(sortPos3.x, sortPos3.y);

            troopStates.Add(troop);

            if (troop.renderer != null)
            {
                renderOrder.Add(troop);
            }
        }

        renderOrder.Sort((a, b) =>
        {
            int xCompare = a.sortPosition.x.CompareTo(b.sortPosition.x);
            if (xCompare != 0) return xCompare;
            return a.sortPosition.y.CompareTo(b.sortPosition.y);
        });

        for (int i = 0; i < renderOrder.Count; i++)
        {
            listS.Add(renderOrder[i].renderer);
        }
    }

    private void BuildKillOrder()
    {
        killOrder.Clear();
        for (int i = 0; i < troopStates.Count; i++)
        {
            killOrder.Add(troopStates[i]);
        }

        ShuffleHelper.Shuffle(killOrder);
        nextKillOrderIndex = 0;
    }

    public void SetColors(Color color)
    {
        colorA = color;
        colorB = color;
        ApplyColors();
    }

    public void SetColors(Color newColorA, Color newColorB)
    {
        colorA = newColorA;
        colorB = newColorB;
        ApplyColors();
    }

    public void SetFactionDisorder(float amount)
    {
        baseDisorderAmount = Mathf.Max(0f, amount);
    }

    public void SetMorale(float morale)
    {
        currentMorale = Mathf.Clamp(morale, 0f, 100f);
    }

    public void SetStyle(Color newColorA, Color newColorB, float factionDisorder, float morale)
    {
        colorA = newColorA;
        colorB = newColorB;
        baseDisorderAmount = Mathf.Max(0f, factionDisorder);
        currentMorale = Mathf.Clamp(morale, 0f, 100f);

        ApplyColors();
    }

    private void ApplyColors()
    {
        int count = listS.Count;
        if (count == 0) return;

        if (count == 1)
        {
            if (listS[0] != null)
            {
                listS[0].color = colorA;
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = listS[i];
            if (sr == null) continue;

            float t = i / (float)(count - 1);
            sr.color = Color.Lerp(colorA, colorB, t);
        }
    }

    public void SyncTroopsToHealth(int currentHealth)
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, formationMaxHealth);

        int totalTroops = troopStates.Count;
        if (totalTroops == 0) return;

        float healthPercent = currentHealth / (float)formationMaxHealth;

        int desiredAliveTroops = currentHealth <= 0
            ? 0
            : Mathf.CeilToInt(healthPercent * totalTroops);

        desiredAliveTroops = Mathf.Clamp(desiredAliveTroops, 0, totalTroops);

        int desiredDeadTroops = totalTroops - desiredAliveTroops;
        int additionalKillsNeeded = desiredDeadTroops - killedTroopCount;

        if (additionalKillsNeeded > 0)
        {
            KillTroopsInternal(additionalKillsNeeded);
        }
    }

    public void KillOneTroop()
    {
        KillTroopsInternal(1);
    }

    private void KillTroopsInternal(int amount)
    {
        if (amount <= 0) return;

        int killedNow = 0;

        while (killedNow < amount && nextKillOrderIndex < killOrder.Count)
        {
            TroopState troop = killOrder[nextKillOrderIndex];
            nextKillOrderIndex++;

            if (troop == null) continue;
            if (troop.gameObject == null) continue;
            if (!troop.gameObject.activeSelf) continue;

            Color corpseColor = colorA;
            if (troop.renderer != null)
            {
                corpseColor = troop.renderer.color;
            }

            if (deadTroopPrefab != null)
            {
                Quaternion corpseRotation =
                    troop.transform.rotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        UnityEngine.Random.Range(-corpseTiltAmount, corpseTiltAmount)
                    );

                GameObject corpse = Instantiate(
                    deadTroopPrefab,
                    troop.transform.position,
                    corpseRotation
                );

                SpriteRenderer corpseRenderer = corpse.GetComponent<SpriteRenderer>();
                if (corpseRenderer != null)
                {
                    corpseRenderer.color = corpseColor;
                }

                corpse.transform.eulerAngles = new Vector3(
                    0f,
                    0f,
                    UnityEngine.Random.Range(-corpseTiltAmount, corpseTiltAmount)
                );

                TheDead.AddParent(corpse);
            }

            troop.gameObject.SetActive(false);
            killedNow++;
            killedTroopCount++;
        }
    }

    [ContextMenu("Rebuild Formation Slots")]
    public void RebuildFormationSlots()
    {
        CacheTroopData();
        BuildKillOrder();
    }

    [ContextMenu("Snap Visual Root To Target")]
    public void SnapVisualRootToTarget()
    {
        if (targetRoot == null) return;

        transform.position = targetRoot.position;
        transform.rotation = targetRoot.rotation;
        rootFollowVelocity = Vector3.zero;
    }
}

public static class ShuffleHelper
{
    private static readonly System.Random rng = new System.Random();

    public static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}