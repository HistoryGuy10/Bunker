using System;
using System.Collections.Generic;
using UnityEngine;

public class GetUnits : MonoBehaviour
{
    [SerializeField] private List<GameObject> list = new List<GameObject>();
    [SerializeField] private List<SpriteRenderer> listS = new List<SpriteRenderer>();
    [SerializeField] private GameObject deadTroopPrefab;
    [SerializeField] private float corpseTiltAmount = 12f;

    [SerializeField] private int formationMaxHealth = 100;

    private int nextTroopIndex = 0;
    private int killedTroopCount = 0;

    private void Awake()
    {
        ShuffleHelper.Shuffle(list);
    }

    private Color color;
    public void SetColors(Color color)
    {
        this.color = color;
        foreach (SpriteRenderer s in listS)
        {
            s.color = color;
        }
    }

    public void SyncTroopsToHealth(int currentHealth)
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, formationMaxHealth);

        int totalTroops = list.Count;
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

        while (killedNow < amount && nextTroopIndex < list.Count)
        {
            GameObject troop = list[nextTroopIndex];
            nextTroopIndex++;

            if (troop == null) continue;
            if (!troop.activeSelf) continue;

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

                corpse.GetComponent<SpriteRenderer>().color = color;
                corpse.transform.eulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-corpseTiltAmount, corpseTiltAmount));
            }

            troop.SetActive(false);
            killedNow++;
            killedTroopCount++;
        }
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
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}