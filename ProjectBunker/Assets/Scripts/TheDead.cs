using UnityEngine;

public class TheDead : MonoBehaviour
{
    private static TheDead _instance;
    public static TheDead Instance;

    public void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public static void AddParent(GameObject gameObject)
    {
        gameObject.transform.SetParent(Instance.transform, true);
    }

    public static void ClearAllCorpses()
    {
        foreach (Transform child in Instance.transform)
        {
            Destroy(child.gameObject);
        }
    }

    public static int Counter()
    {
        return Instance.transform.childCount;
    }
}
